namespace MauiApp1.Models;

public static class ExpenseReportDescription
{
    public static string For(ExpensePeriodType period) => period switch
    {
        ExpensePeriodType.Daily => "A summary of your expenses for the selected day, grouped by category.",
        ExpensePeriodType.Weekly => "A summary of your expenses for the current Monday-to-Sunday operating week.",
        ExpensePeriodType.Monthly => "A summary of your expenses for the current calendar month, grouped by category.",
        _ => "A summary of your expenses for the selected period, grouped by category."
    };
}
