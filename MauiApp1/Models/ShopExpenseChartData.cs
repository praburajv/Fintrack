namespace MauiApp1.Models;

using Microsoft.Maui.Controls;

public class ShopExpenseChartData
{
    public int ShopId { get; init; }
    public int SerialNumber { get; init; }
    public string ShopName { get; init; } = string.Empty;
    public string ShopDisplayName { get; init; } = string.Empty;
    public decimal TotalValue { get; init; }
    public int VisitCount { get; init; }
    public string VisitCountText => VisitCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
    public Brush Color { get; init; } = new SolidColorBrush(Colors.Gray);
}
