using FinTracker.Utilities;

namespace FinTracker.Models;

public sealed class DashboardSearchResult
{
    public int Id { get; init; }
    public int ShopId { get; init; }
    public string ShopName { get; init; } = string.Empty;
    public string? BillReferenceNo { get; init; }
    public DateTime Date { get; init; }
    public decimal Amount { get; init; }
    public FinancialTransactionType TransactionType { get; init; }
    public string IconFileName { get; init; } = "default_shop.png";
    public bool IsReference => BillReferenceNo is not null;
    public string Name => IsReference ? $"{BillReferenceNo} · {ShopName}" : ShopName;
    public string Detail => IsReference ? $"{TransactionType} · {Date:dd/MM/yyyy} · {EuroFormatter.Format(Amount)}" : "Shop · Bill analysis";
}
