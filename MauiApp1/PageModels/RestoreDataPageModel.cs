using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.PageModels;

public partial class RestoreDataPageModel(IBackupService backupService, ModalErrorHandler errors) : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _processingMessage = string.Empty;
    [ObservableProperty] private double _progress;
    public string ProgressText => $"{Progress:0}%";
    [ObservableProperty] private string _selectedFile = string.Empty;
    [ObservableProperty] private BackupValidationResult? _validation;
    [ObservableProperty] private BackupRecordSummary _currentData = new(0, 0, 0);
    [ObservableProperty] private bool _restoreCompleted;
    public bool HasValidBackup => Validation?.IsValid == true;
    public string BackupFileName => Path.GetFileName(SelectedFile);
    public BackupMetadata? Metadata => Validation?.Metadata;
    public BackupRecordSummary BackupData => Validation?.Summary ?? new(0, 0, 0);
    public string CreatedText => Metadata?.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
    public bool IsSelectedMonthsBackup => string.Equals(Metadata?.BackupType, "SelectedMonths", StringComparison.OrdinalIgnoreCase);
    public string SelectedMonthsText => Metadata?.SelectedYear is { } year ? $"{year}: {string.Join(", ", Metadata.SelectedMonths.Select(month => new DateTime(2000, month, 1).ToString("MMMM")))}" : string.Empty;
    [RelayCommand] private async Task SelectBackup()
    {
        if (IsBusy) return;
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a Household Expense Tracker backup" });
            if (file is null) return;
            Progress = 0; OnPropertyChanged(nameof(ProgressText)); IsBusy = true; ProcessingMessage = "Validating backup...";
            await Task.Yield();
            var staged = Path.Combine(FileSystem.CacheDirectory, $".selected-{Guid.NewGuid():N}-{file.FileName}");
            await using (var source = await file.OpenReadAsync())
            await using (var destination = File.Create(staged))
                await source.CopyToAsync(destination);
            SelectedFile = staged; Progress = 35; OnPropertyChanged(nameof(ProgressText)); Validation = await backupService.ValidateBackupAsync(staged); Progress = 100; OnPropertyChanged(nameof(ProgressText));
            if (!HasValidBackup) await Shell.Current.DisplayAlertAsync("Backup Cannot Be Restored", Validation?.ErrorMessage ?? "The selected file is invalid, corrupted, or incompatible.", "OK");
            else CurrentData = await backupService.GetCurrentSummaryAsync();
            OnPropertyChanged(nameof(HasValidBackup)); OnPropertyChanged(nameof(BackupFileName)); OnPropertyChanged(nameof(Metadata)); OnPropertyChanged(nameof(BackupData)); OnPropertyChanged(nameof(CreatedText));
            OnPropertyChanged(nameof(IsSelectedMonthsBackup)); OnPropertyChanged(nameof(SelectedMonthsText));
        }
        catch (Exception ex) { errors.HandleError(ex); } finally { IsBusy = false; ProcessingMessage = string.Empty; }
    }
    [RelayCommand] private async Task ChooseAnotherFile() { if (!IsBusy) await SelectBackup(); }
    [RelayCommand] private async Task Restore()
    {
        if (IsBusy || !HasValidBackup) return;
        var backup = BackupData;
        var message = IsSelectedMonthsBackup
            ? $"This will permanently replace all destination bills for {SelectedMonthsText}. Other months and setup data will be preserved.\n\nImported bills: {Metadata?.SelectedMonthBillCount ?? 0}"
            : string.Join(Environment.NewLine, "This will replace your current data.", "", "CURRENT DATA", $"Shops: {CurrentData.ShopCount}", $"Payment Modes: {CurrentData.PaymentModeCount}", $"Bills: {CurrentData.BillCount}", "", "BACKUP DATA", $"Shops: {backup.ShopCount}", $"Payment Modes: {backup.PaymentModeCount}", $"Bills: {backup.BillCount}", "", "A safety backup will be created automatically.");
        if (!await Shell.Current.DisplayAlertAsync("Restore Backup?", message, "Restore", "Cancel")) return;
        Progress = 0; OnPropertyChanged(nameof(ProgressText)); IsBusy = true; ProcessingMessage = "Protecting current data...";
        await Task.Yield();
        try
        {
            ProcessingMessage = IsSelectedMonthsBackup ? "Replacing selected-month expense data..." : "Restoring data...";
            if (IsSelectedMonthsBackup)
            {
                var result = await backupService.RestoreSelectedMonthsBackupAsync(SelectedFile, progress: new Progress<double>(value => { Progress = value; OnPropertyChanged(nameof(ProgressText)); }));
                Validation = null; RestoreCompleted = true;
                await Shell.Current.DisplayAlertAsync("Selected Months Restored", $"Replaced: {result.Replaced} month(s)\nImported: {result.Imported} bill(s)", "OK");
            }
            else
            {
                await backupService.RestoreBackupAsync(SelectedFile, progress: new Progress<double>(value => { Progress = value; OnPropertyChanged(nameof(ProgressText)); }));
            }
            if (!IsSelectedMonthsBackup)
            {
            var restored = await backupService.GetCurrentSummaryAsync(); Validation = null; RestoreCompleted = true; CurrentData = restored;
            OnPropertyChanged(nameof(HasValidBackup)); OnPropertyChanged(nameof(Metadata));
            var success = string.Join(Environment.NewLine, $"Shops: {restored.ShopCount}", $"Payment Modes: {restored.PaymentModeCount}", $"Bills: {restored.BillCount}");
            await Shell.Current.DisplayAlertAsync("Restore Completed Successfully", success, "OK");
            }
        }
        catch (Exception ex) { errors.HandleError(ex); } finally { IsBusy = false; ProcessingMessage = string.Empty; }
    }
    [RelayCommand]
    private async Task Back()
    {
        // Use the absolute dashboard route. A restore can replace the active
        // database while this page is on a nested Shell route, so attempting
        // to pop that route can leave Shell with an invalid navigation entry.
        await Shell.Current.GoToAsync("//main");
    }

    [RelayCommand] private Task ReturnToDashboard() => Shell.Current.GoToAsync("//main");
}
