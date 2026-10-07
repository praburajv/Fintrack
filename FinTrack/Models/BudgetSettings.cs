namespace FinTracker.Models;

public sealed record BudgetSettings(decimal MonthlyIncome, decimal MonthlyBudget);

public sealed record MonthlyBudgetSummary(decimal MonthlyIncome, decimal MonthlyBudget, decimal ActualExpense)
{
    public decimal RemainingBudget => MonthlyBudget - ActualExpense;
    public decimal? BudgetUsedPercent => MonthlyBudget > 0 ? ActualExpense / MonthlyBudget * 100 : null;
    public double BudgetProgress => MonthlyBudget > 0 ? (double)Math.Min(ActualExpense / MonthlyBudget, 1m) : 0d;
    public string BudgetHealth => MonthlyBudget <= 0
        ? "No budget configured"
        : BudgetUsedPercent > 100
            ? "Over Budget"
            : BudgetUsedPercent >= 80
                ? "Approaching Budget"
                : "Within Budget";
}
