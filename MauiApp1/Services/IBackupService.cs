using MauiApp1.Models;
namespace MauiApp1.Services;
public interface IBackupService
{
 Task<BackupRecordSummary> GetCurrentSummaryAsync(CancellationToken cancellationToken = default);
 Task<(string Path, BackupMetadata Metadata)> CreateBackupAsync(CancellationToken cancellationToken = default, IProgress<double>? progress = null);
 Task<BackupValidationResult> ValidateBackupAsync(string sourcePath, CancellationToken cancellationToken = default);
 Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default, IProgress<double>? progress = null);
 Task<(string Path, BackupMetadata Metadata)> CreateSelectedMonthsBackupAsync(int year, IReadOnlyCollection<int> months, CancellationToken cancellationToken = default, IProgress<double>? progress = null);
 Task<SelectedMonthsRestoreResult> RestoreSelectedMonthsBackupAsync(string backupPath, CancellationToken cancellationToken = default, IProgress<double>? progress = null);
}
