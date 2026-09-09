using MauiApp1.Models;
namespace MauiApp1.Services;
public interface IBackupService
{
 Task<BackupRecordSummary> GetCurrentSummaryAsync(CancellationToken cancellationToken = default);
 Task<(string Path, BackupMetadata Metadata)> CreateBackupAsync(CancellationToken cancellationToken = default);
 Task<BackupValidationResult> ValidateBackupAsync(string sourcePath, CancellationToken cancellationToken = default);
 Task RestoreBackupAsync(string backupPath, CancellationToken cancellationToken = default);
}