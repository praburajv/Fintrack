using FinTracker.Utilities;

namespace FinTracker.Models;

public enum HistorySearchMode { Category, Shop }
public enum HistoryBillOrder { DateNewest, DateOldest, AmountHighest, AmountLowest }

public sealed record HistoryBill(int Id, int ShopId, string ShopName, int CategoryId, DateTime Date,
    string PaymentMode, string? Reference, string Note, decimal Amount);

public sealed record HistoryGroup(string Title, IReadOnlyList<HistoryBill> Bills, IReadOnlyList<HistoryGroup> Children)
{
    public decimal Total => Bills.Sum(x => x.Amount) + Children.Sum(x => x.Total);
    public int Count => Bills.Count + Children.Sum(x => x.Count);
}

public sealed record HistoryReport(int AccountId, string Company, string CurrencyCode, FinancialTransactionType Type,
    HistorySearchMode Mode, string Selection, IReadOnlyList<int> Years, HistoryBillOrder Order, IReadOnlyList<HistoryGroup> Groups)
{
    public decimal Total => Groups.Sum(x => x.Total);
    public int Count => Groups.Sum(x => x.Count);
    public string Title => $"{Type} by {Mode}";
    public string FileName => $"{Type}-History-{Mode}-{DateTime.Now:yyyyMMdd-HHmmssfff}.pdf";
    public string TotalText => EuroFormatter.Format(Total);
    public string OrderText => Order switch
    {
        HistoryBillOrder.DateNewest => "Date newest first",
        HistoryBillOrder.DateOldest => "Date oldest first",
        HistoryBillOrder.AmountHighest => "Amount highest first",
        _ => "Amount lowest first"
    };
}
