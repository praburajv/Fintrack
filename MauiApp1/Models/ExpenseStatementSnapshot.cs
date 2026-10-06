namespace MauiApp1.Models;

public sealed class ExpenseStatementSnapshot
{
    public ExpensePeriodType PeriodType { get; init; }
    public string PeriodTitle { get; init; } = string.Empty;
    public string StatementTitle { get; init; } = "Expense Statement";
    public DateTime StartDate { get; init; }
    public DateTime EndExclusive { get; init; }
    public string CurrencyCode { get; init; } = "EUR";
    public string CurrencySymbol { get; init; } = "€";
    public IReadOnlyList<ExpenseStatementCategory> Categories { get; init; } = [];
    public IReadOnlyList<ShopExpenseChartData> ShopVisits { get; init; } = [];
    public string OverallTotalText { get; init; } = string.Empty;
    public int BillCount => Categories.Sum(category => category.Rows.Count);
    public bool HasShopVisits => ShopVisits.Count > 0;
    public ExpenseStatementSnapshot WithShopVisits(IReadOnlyList<ShopExpenseChartData> visits) => new()
    {
        PeriodType = PeriodType, PeriodTitle = PeriodTitle, StatementTitle = StatementTitle,
        StartDate = StartDate, EndExclusive = EndExclusive, CurrencyCode = CurrencyCode,
        CurrencySymbol = CurrencySymbol, Categories = Categories, ShopVisits = visits,
        OverallTotalText = OverallTotalText
    };
    public string FileName => $"Expense-Statement-{StartDate:yyyy-MM-dd}-{EndExclusive.AddDays(-1):yyyy-MM-dd}.pdf";
}

public sealed class ExpenseStatementCategory
{
    public string CategoryName { get; init; } = string.Empty;
    public IReadOnlyList<ExpenseStatementRow> Rows { get; init; } = [];
    public string CategoryTotalText { get; init; } = string.Empty;
    public int BillCount => Rows.Count;
    public string BillCountText => $"Bills: {BillCount}";
}

public sealed class ExpenseStatementRow
{
    public int SerialNumber { get; init; }
    public DateTime BillDate { get; init; }
    public string DateText => BillDate.ToString("dd/MM/yyyy");
    public string ShopName { get; init; } = string.Empty;
    public string PaymentModeName { get; init; } = string.Empty;
    public decimal Value { get; init; }
    public string ValueText { get; init; } = string.Empty;
}
