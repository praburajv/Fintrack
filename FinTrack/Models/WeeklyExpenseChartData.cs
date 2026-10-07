namespace FinTracker.Models;

public class WeeklyExpenseChartData
{
    public int DayNumber { get; init; }
    public string DayName { get; init; } = string.Empty;
    public DateTime Date { get; init; }
    public string DisplayLabel => $"{Date.ToString("ddd", System.Globalization.CultureInfo.InvariantCulture)} ({Date.Day})";
    public decimal TotalValue { get; init; }
}
