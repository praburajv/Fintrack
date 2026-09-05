namespace MauiApp1.Models;

public class MonthlyExpenseChartData
{
    public int MonthNumber { get; init; }
    public string MonthName { get; init; } = string.Empty;
    public decimal TotalValue { get; init; }
}
