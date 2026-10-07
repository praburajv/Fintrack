namespace FinTracker.Models;

public sealed class ExpenseMonthMaintenanceItem
{
    public int Year { get; init; }
    public int MonthNumber { get; init; }
    public string MonthName { get; init; } = string.Empty;
    public int BillCount { get; init; }
    public bool HasBills => BillCount > 0;
    public int ImportedIncomeCount { get; init; }
    public int UntrackedIncomeCount { get; init; }
    public bool HasDeletableData => HasBills || ImportedIncomeCount > 0 || UntrackedIncomeCount > 0;
    public string CountsText => $"{BillCount} Expense Bills · {ImportedIncomeCount} imported Income\n{UntrackedIncomeCount} untracked Income (review only)";
}
