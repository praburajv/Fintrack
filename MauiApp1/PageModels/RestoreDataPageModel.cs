using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.PageModels;

public partial class RestoreDataPageModel(IBackupService backupService, ModalErrorHandler errors) : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _processingMessage = string.Empty;
    [ObservableProperty] private string _selectedFile = string.Empty;
    [ObservableProperty] private BackupValidationResult? _validation;
    [ObservableProperty] private BackupRecordSummary _currentData = new(0, 0, 0);
    [ObservableProperty] private bool _restoreCompleted;
    public bool HasValidBackup => Validation?.IsValid == true;
    public string BackupFileName => Path.GetFileName(SelectedFile);
    public BackupMetadata? Metadata => Validation?.Metadata;
    public BackupRecordSummary BackupData => Validation?.Summary ?? new(0, 0, 0);
    public string CreatedText => Metadata?.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
    [RelayCommand] private async Task SelectBackup()
    {
        if (IsBusy) return;
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select a Household Expense Tracker backup" });
            if (file is null) return;
            IsBusy = true; ProcessingMessage = "Validating backup...";
            var staged = Path.Combine(FileSystem.CacheDirectory, $".selected-{Guid.NewGuid():N}-{file.FileName}");
            await using (var source = await file.OpenReadAsync())
            await using (var destination = File.Create(staged))
                await source.CopyToAsync(destination);
            SelectedFile = staged; Validation = await backupService.ValidateBackupAsync(staged);
            if (!HasValidBackup) await Shell.Current.DisplayAlertAsync("Backup Cannot Be Restored", Validation?.ErrorMessage ?? "The selected file is invalid, corrupted, or incompatible.", "OK");
            else CurrentData = await backupService.GetCurrentSummaryAsync();
            OnPropertyChanged(nameof(HasValidBackup)); OnPropertyChanged(nameof(BackupFileName)); OnPropertyChanged(nameof(Metadata)); OnPropertyChanged(nameof(BackupData)); OnPropertyChanged(nameof(CreatedText));
        }
        catch (Exception ex) { errors.HandleError(ex); } finally { IsBusy = false; ProcessingMessage = string.Empty; }
    }
    [RelayCommand] private async Task ChooseAnotherFile() { if (!IsBusy) await SelectBackup(); }
    [RelayCommand] private async Task Restore()
    {
        if (IsBusy || !HasValidBackup) return;
        var backup = BackupData;
        var message = string.Join(Environment.NewLine, "This will replace your current data.", "", "CURRENT DATA", $"Shops: {CurrentData.ShopCount}", $"Payment Modes: {CurrentData.PaymentModeCount}", $"Bills: {CurrentData.BillCount}", "", "BACKUP DATA", $"Shops: {backup.ShopCount}", $"Payment Modes: {backup.PaymentModeCount}", $"Bills: {backup.BillCount}", "", "A safety backup will be created automatically.");
        if (!await Shell.Current.DisplayAlertAsync("Restore Backup?", message, "Restore", "Cancel")) return;
        IsBusy = true; ProcessingMessage = "Protecting current data...";
        try
        {
            ProcessingMessage = "Restoring data..."; await backupService.RestoreBackupAsync(SelectedFile);
            var restored = await backupService.GetCurrentSummaryAsync(); Validation = null; RestoreCompleted = true; CurrentData = restored;
            OnPropertyChanged(nameof(HasValidBackup)); OnPropertyChanged(nameof(Metadata));
            var success = string.Join(Environment.NewLine, $"Shops: {restored.ShopCount}", $"Payment Modes: {restored.PaymentModeCount}", $"Bills: {restored.BillCount}");
            await Shell.Current.DisplayAlertAsync("Restore Completed Successfully", success, "OK");
        }
        catch (Exception ex) { errors.HandleError(ex); } finally { IsBusy = false; ProcessingMessage = string.Empty; }
    }
    [RelayCommand] private Task ReturnToDashboard() => Shell.Current.GoToAsync("//main");
}