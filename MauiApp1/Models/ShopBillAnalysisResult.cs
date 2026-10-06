using MauiApp1.Utilities;
using Microsoft.Maui.Controls;

namespace MauiApp1.Models;

public sealed class ShopBillAnalysisResult
{
    public int ShopId { get; init; }
    public string ShopName { get; init; } = string.Empty;
    public int Year { get; init; }
    public int VisitCount { get; init; }
    public decimal TotalSpent { get; init; }
    public decimal AverageSpendPerVisit => VisitCount > 0 ? TotalSpent / VisitCount : 0m;
    public IReadOnlyList<MonthlyVisitData> MonthlyVisits { get; init; } = [];
}

public sealed class MonthlyVisitData
{
    public int MonthNumber { get; init; }
    public string MonthName { get; init; } = string.Empty;
    public int VisitCount { get; init; }
    public decimal TotalSpent { get; init; }
    public Brush Color { get; init; } = new SolidColorBrush(Colors.Gray);
}

public sealed class ShopTransactionDateSummary
{
    public DateTime Date { get; init; }
    public int BillCount { get; init; }
    public decimal TotalSpent { get; init; }
    public string DateText => Date.ToString("dd MMMM yyyy");
    public string TotalSpentText => EuroFormatter.Format(TotalSpent);
    public string BillCountText => $"{BillCount} bill{(BillCount == 1 ? string.Empty : "s")}";
}
