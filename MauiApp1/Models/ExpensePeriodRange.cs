using System.Globalization;
namespace MauiApp1.Models;
public sealed record ExpensePeriodRange(DateTime StartDate, DateTime EndExclusive, string DisplayText);
public static class ExpensePeriodCalculator
{
    public static ExpensePeriodRange Calculate(DateTime referenceDate, ExpensePeriodType type)
    {
        var date = referenceDate.Date;
        return type switch
        {
            ExpensePeriodType.Daily => new(date, date.AddDays(1), date.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)),
            ExpensePeriodType.Weekly => Weekly(date),
            ExpensePeriodType.Monthly => Monthly(date),
            ExpensePeriodType.Yearly => Yearly(date),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }
    private static ExpensePeriodRange Weekly(DateTime date)
    { var start = date.AddDays(-(((int)date.DayOfWeek + 6) % 7)); return new(start, start.AddDays(7), $"{start:dd-MMM-yyyy} → {start.AddDays(6):dd-MMM-yyyy}"); }
    private static ExpensePeriodRange Monthly(DateTime date)
    { var start = new DateTime(date.Year, date.Month, 1); return new(start, start.AddMonths(1), start.ToString("MMMM yyyy", CultureInfo.CurrentCulture)); }
    private static ExpensePeriodRange Yearly(DateTime date)
    { var start = new DateTime(date.Year, 1, 1); return new(start, start.AddYears(1), start.Year.ToString(CultureInfo.InvariantCulture)); }
}
