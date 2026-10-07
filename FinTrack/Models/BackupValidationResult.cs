namespace FinTracker.Models;
public sealed class BackupValidationResult
{
 public bool IsValid { get; init; }
 public string ErrorMessage { get; init; } = string.Empty;
 public string FilePath { get; init; } = string.Empty;
 public string FileName => Path.GetFileName(FilePath);
 public BackupMetadata? Metadata { get; init; }
 public BackupRecordSummary Summary => Metadata is null ? new(0, 0, 0) : new(Metadata.ShopCount, Metadata.PaymentModeCount, Metadata.BillCount);
}