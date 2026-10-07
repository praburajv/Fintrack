namespace FinTracker.Models;

public sealed class BudgetReportSnapshot
{
    public string PeriodTitle { get; init; } = string.Empty;
    public DateTime Month { get; init; }
    public string CurrencyCode { get; init; } = "EUR";
    public string MonthlyIncomeText { get; init; } = string.Empty;
    public string MonthlyBudgetText { get; init; } = string.Empty;
    public string ActualExpenseText { get; init; } = string.Empty;
    public string RemainingBudgetText { get; init; } = string.Empty;
    public string BudgetUsedPercentText { get; init; } = string.Empty;
    public string BudgetHealthText { get; init; } = string.Empty;
    public IReadOnlyList<BudgetReportCategory> Categories { get; init; } = [];
    public string FileName => $"Budget-Report-{Month:yyyy-MM}.pdf";
}

public sealed class BudgetReportCategory
{
    public string CategoryName { get; init; } = string.Empty;
    public string BudgetedText { get; init; } = string.Empty;
    public string ActualExpenseText { get; init; } = string.Empty;
    public string RemainingText { get; init; } = string.Empty;
    public string StatusText { get; init; } = string.Empty;
}
