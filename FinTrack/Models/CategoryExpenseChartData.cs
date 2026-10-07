namespace FinTracker.Models;

public sealed class CategoryExpenseChartData
{
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public string CategoryDisplayName => CategoryName;
    public decimal TotalValue { get; init; }
    public Brush Color { get; init; } = new SolidColorBrush(Colors.Gray);
}
