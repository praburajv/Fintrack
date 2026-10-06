namespace MauiApp1.Models;

public sealed record MonthlyDeletionRecord(FinancialTransactionType Type, int Id, DateTime Date,
    int ShopId, int PaymentModeId, decimal Amount, string Note, string ShopName, string? ImportId)
{
    public string StoredDate { get; init; } = Date.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
}

public sealed record MonthlyDeletionPlan(int Year, int Month, string? ImportId,
    IReadOnlyList<MonthlyDeletionRecord> Records)
{
    public IEnumerable<MonthlyDeletionRecord> Expenses => Records.Where(row => row.Type == FinancialTransactionType.Expense);
    public IEnumerable<MonthlyDeletionRecord> ImportedIncome => Records.Where(row => row.Type == FinancialTransactionType.Income && row.ImportId is not null);
    public IEnumerable<MonthlyDeletionRecord> UntrackedIncome => Records.Where(row => row.Type == FinancialTransactionType.Income && row.ImportId is null);
    public string[] ImportIds => Records.Where(row => row.ImportId is not null).Select(row => row.ImportId!).Distinct().Order(StringComparer.OrdinalIgnoreCase).ToArray();
}

public sealed record MonthlyDeletionResult(int ExpenseCount, int IncomeCount, int ProtectedIncomeCount);
