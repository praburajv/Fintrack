namespace FinTracker.Utilities;

public enum FinancialDashboardPeriod
{
    Month,
    LastThreeMonths,
    ThisYear
}

public sealed record FinancialDashboardRange(
    DateTime Start,
    DateTime EndExclusive,
    DateTime PreviousStart,
    DateTime PreviousEndExclusive);

public static class FinancialDashboardDates
{
    public static FinancialDashboardRange For(FinancialDashboardPeriod period, DateTime today)
    {
        var day = today.Date;
        var month = new DateTime(day.Year, day.Month, 1);
        return period switch
        {
            FinancialDashboardPeriod.Month => new(month, month.AddMonths(1), month.AddMonths(-1), month),
            FinancialDashboardPeriod.LastThreeMonths => new(month.AddMonths(-2), month.AddMonths(1), month.AddMonths(-5), month.AddMonths(-2)),
            FinancialDashboardPeriod.ThisYear => new(new DateTime(day.Year, 1, 1), day.AddDays(1), new DateTime(day.Year - 1, 1, 1), day.AddDays(1).AddYears(-1)),
            _ => throw new ArgumentOutOfRangeException(nameof(period))
        };
    }

    public static string Greeting(DateTime localTime) => localTime.Hour switch
    {
        >= 5 and < 12 => "Good morning",
        >= 12 and < 18 => "Good afternoon",
        _ => "Good night"
    };

    public static string Change(decimal current, decimal previous)
    {
        if (previous <= 0) return "No prior data";
        var percentage = (current - previous) / previous * 100;
        return $"{(percentage >= 0 ? "↑" : "↓")} {Math.Abs(percentage):0.#}%";
    }
}
