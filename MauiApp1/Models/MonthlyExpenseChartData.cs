namespace MauiApp1.Models;

using Microsoft.Maui.Controls;

public class MonthlyExpenseChartData
{
    public int MonthNumber { get; init; }
    public string MonthName { get; init; } = string.Empty;
    public decimal TotalValue { get; init; }
    public Brush Color { get; init; } = new SolidColorBrush(Colors.Gray);
}
