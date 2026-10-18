namespace FinTracker.Models;
public sealed class BackupMetadata
{
 public string ApplicationName { get; set; } = "Household Expense Tracker";
 public string ApplicationId { get; set; } = "com.ooimaco.crochuntmpluto";
 public int BackupFormatVersion { get; set; } = 1;
 public int DatabaseSchemaVersion { get; set; } = 1;
 public DateTime CreatedAtUtc { get; set; }
 public string ApplicationVersion { get; set; } = string.Empty;
 public int ShopCount { get; set; }
 public int PaymentModeCount { get; set; }
 public int BillCount { get; set; }
 public string DatabaseSha256 { get; set; } = string.Empty;
 public string BackupType { get; set; } = "Complete";
 public int? SelectedYear { get; set; }
 public List<int> SelectedMonths { get; set; } = [];
 public int SelectedMonthBillCount { get; set; }
 public int? FinancialAccountId { get; set; }
 public string? FinancialAccountName { get; set; }
}
