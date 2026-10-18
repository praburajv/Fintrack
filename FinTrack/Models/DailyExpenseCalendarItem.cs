using Microsoft.Maui.Graphics;
using FinTracker.Utilities;

namespace FinTracker.Models;

public class DailyExpenseCalendarItem
{
    public DateTime Date { get; init; }
    public int DayNumber { get; init; }
    public int GridRow { get; init; }
    public int GridColumn { get; init; }
    public decimal TotalValue { get; init; }
    public bool IsIncome { get; init; }
    public Color AmountColor => IsIncome ? (Application.Current?.Resources["Success"] as Color ?? Colors.Green) : (Application.Current?.Resources["TextPrimary"] as Color ?? Colors.Black);
    public string FormattedTotalValue => IsPlaceholder ? string.Empty : EuroFormatter.Format(TotalValue);
    public double AmountFontSize => FormattedTotalValue.Length switch
    {
        <= 6 => 10,
        <= 8 => 9.5,
        <= 10 => 9,
        _ => 8
    };
    public string WeekdayShortName => IsPlaceholder ? string.Empty : Date.DayOfWeek switch
    {
        DayOfWeek.Monday => "Mo",
        DayOfWeek.Tuesday => "Tu",
        DayOfWeek.Wednesday => "We",
        DayOfWeek.Thursday => "Th",
        DayOfWeek.Friday => "Fr",
        DayOfWeek.Saturday => "Sa",
        DayOfWeek.Sunday => "Su",
        _ => string.Empty
    };
    public bool IsPlaceholder { get; init; }
    public bool IsHighestExpenseDay { get; init; }
    public string TileDescription => IsPlaceholder ? string.Empty : $"{Date:dd/MM/yyyy}, {WeekdayShortName}, total {(IsIncome ? "income" : "expenses")} {EuroFormatter.Format(TotalValue)}";
    public Color BorderColor => IsHighestExpenseDay
        ? (Application.Current?.Resources["Danger"] as Color ?? Colors.Red)
        : Colors.Transparent;
    public double BorderThickness => IsHighestExpenseDay ? 2 : 0;
}
