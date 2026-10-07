namespace FinTracker.Models;

public sealed class FinancialFlowBarData
{
    public string MonthName { get; init; } = string.Empty;
    public decimal Income { get; init; }
    public decimal Expense { get; init; }
}
