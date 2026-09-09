using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;

namespace MauiApp1.PageModels;

public partial class BackupDataPageModel(IBackupService backupService, ModalErrorHandler errors, IBackupFileExporter exporter) : ObservableObject
{
    [ObservableProperty] private BackupRecordSummary _currentData = new(0, 0, 0);
    [ObservableProperty] private BackupMetadata? _backupMetadata;
    [ObservableProperty] private string _backupPath = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _processingMessage = string.Empty;
    public bool HasBackup => BackupMetadata is not null;
    public string BackupFileName => Path.GetFileName(BackupPath);
    public string CreatedText => BackupMetadata?.CreatedAtUtc.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? string.Empty;
    [RelayCommand] private async Task Appearing() { if (IsBusy) return; try { CurrentData = await backupService.GetCurrentSummaryAsync(); } catch (Exception ex) { errors.HandleError(ex); } }
    [RelayCommand] private async Task CreateBackup()
    {
        if (IsBusy) return; IsBusy = true; ProcessingMessage = "Creating backup...";
        try { var result = await backupService.CreateBackupAsync(); BackupPath = result.Path; BackupMetadata = result.Metadata; OnPropertyChanged(nameof(HasBackup)); OnPropertyChanged(nameof(BackupFileName)); OnPropertyChanged(nameof(CreatedText)); }
        catch (Exception ex) { errors.HandleError(ex); } finally { IsBusy = false; ProcessingMessage = string.Empty; }
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