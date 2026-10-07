namespace FinTracker.Models;

public class BillTransaction
{
    public int Id { get; set; }
    public DateTime BillDate { get; set; } = DateTime.Today;
    public int ShopId { get; set; }
    public int PaymentModeId { get; set; }
    public decimal BillValue { get; set; }
    public string Note { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public ShopCategory ShopCategory { get; set; } = ShopCategory.DefaultShop;
    public int ShopCategoryId { get; set; }
    public string ShopCategoryName { get; set; } = "Default Shop";
    public string ShopCategoryIconKey { get; set; } = "default_shop";
    public bool IsEtlDt => ShopCategoryName.Equals("ETL_DT", StringComparison.OrdinalIgnoreCase);
    public string ShopIconFileName => CategoryIconCatalog.FileNameFor(ShopCategoryIconKey);
    public string PaymentModeName { get; set; } = string.Empty;
    public int DisplayNumber { get; set; }
    public int? RecurringBillId { get; set; }
    public string? RecurringPeriod { get; set; }
    public FinancialTransactionType TransactionType { get; set; } = FinancialTransactionType.Expense;
    public bool IsIncome => TransactionType == FinancialTransactionType.Income;
    public Color AmountTextColor => IsIncome ? (Application.Current?.Resources["Success"] as Color ?? Colors.Green) : (Application.Current?.Resources["TextPrimary"] as Color ?? Colors.Black);
}
