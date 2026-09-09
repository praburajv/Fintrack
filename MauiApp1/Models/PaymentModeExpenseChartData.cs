namespace MauiApp1.Models;

public sealed class PaymentModeExpenseChartData
{
    public int PaymentModeId { get; init; }
    public string PaymentModeName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public decimal TotalValue { get; init; }
    public Brush Color { get; init; } = Colors.Gray;
}
