using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;
using FinTracker.Services;
using System.Collections.ObjectModel;

namespace FinTracker.PageModels;

public partial class BackupDataPageModel(IBackupService backupService, ModalErrorHandler errors, IBackupFileExporter exporter) : ObservableObject
{
    [ObservableProperty] private BackupRecordSummary _currentData = new(0, 0, 0);
    [ObservableProperty] private BackupMetadata? _backupMetadata;
    [ObservableProperty] private string _backupPath = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _processingMessage = string.Empty;
    [ObservableProperty] private double _progress;
    [ObservableProperty] private int _selectedYear = DateTime.Today.Year;
    public IReadOnlyList<int> Years { get; } = Enumerable.Range(DateTime.Today.Year - 5, 11).ToArray();
    public ObservableCollection<BackupMonthSelection> Months { get; } = new(Enumerable.Range(1, 12).Select(month => new BackupMonthSelection { MonthNumber = month, MonthName = new DateTime(2000, month, 1).ToString("MMMM") }));
    public string ProgressText => $"{Progress:0}%";
    public bool HasBackup => BackupMetadata is not null;
    public string BackupFileName => Path.GetFileName(BackupPath);
    public string CreatedText => BackupMetadata?.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
    [RelayCommand] private async Task Appearing() { if (IsBusy) return; try { CurrentData = await backupService.GetCurrentSummaryAsync(); } catch (Exception ex) { errors.HandleError(ex); } }
    [RelayCommand] private async Task CreateBackup()
    {
        if (IsBusy) return; Progress = 0; OnPropertyChanged(nameof(ProgressText)); IsBusy = true; ProcessingMessage = "Creating backup...";
        await Task.Yield();
        try { var result = await backupService.CreateBackupAsync(progress: new Progress<double>(value => { Progress = value; OnPropertyChanged(nameof(ProgressText)); })); BackupPath = result.Path; BackupMetadata = result.Metadata; OnPropertyChanged(nameof(HasBackup)); OnPropertyChanged(nameof(BackupFileName)); OnPropertyChanged(nameof(CreatedText)); }
        catch (Exception ex) { errors.HandleError(ex); } finally { IsBusy = false; ProcessingMessage = string.Empty; }
    }
    [RelayCommand]
    private async Task CreateSelectedMonthsBackup()
    {
        if (IsBusy) return;
        var selected = Months.Where(month => month.IsSelected).Select(month => month.MonthNumber).ToArray();
        if (selected.Length == 0)
        {
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Select Months", "Select at least one month to back up.", "OK");
            return;
        }
        Progress = 0; OnPropertyChanged(nameof(ProgressText)); IsBusy = true; ProcessingMessage = "Creating selected-month backup...";
        await Task.Yield();
        try
        {
            var result = await backupService.CreateSelectedMonthsBackupAsync(SelectedYear, selected, progress: new Progress<double>(value => { Progress = value; OnPropertyChanged(nameof(ProgressText)); }));
            BackupPath = result.Path; BackupMetadata = result.Metadata;
            OnPropertyChanged(nameof(HasBackup)); OnPropertyChanged(nameof(BackupFileName)); OnPropertyChanged(nameof(CreatedText));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; ProcessingMessage = string.Empty; }
    }
    [RelayCommand] private async Task SaveBackup()
    {
        if (IsBusy || !HasBackup) return; IsBusy = true; ProcessingMessage = "Saving backup...";
        try { await exporter.SaveAsync(BackupPath, BackupFileName); } catch (Exception ex) { errors.HandleError(ex); } finally { IsBusy = false; ProcessingMessage = string.Empty; }
    }
    [RelayCommand] private async Task ShareBackup()
    {
        if (IsBusy || !HasBackup || !File.Exists(BackupPath)) return;
        try { await Share.Default.RequestAsync(new ShareFileRequest { Title = "Share Backup", File = new ShareFile(BackupPath) }); } catch (Exception ex) { errors.HandleError(ex); }
    }
}
