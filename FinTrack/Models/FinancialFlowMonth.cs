namespace FinTracker.Models;

public sealed class FinancialFlowMonth
{
    public int Year { get; init; }
    public int MonthNumber { get; init; }
    public int SerialNumber { get; init; }
    public string MonthName => new DateTime(2000, MonthNumber, 1).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture);
    public decimal Income { get; init; }
    public decimal Expense { get; init; }
    public string IncomeText => Utilities.EuroFormatter.Format(Income);
    public string ExpenseText => Utilities.EuroFormatter.Format(Expense);
}
