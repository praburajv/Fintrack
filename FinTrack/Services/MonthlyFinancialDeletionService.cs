using System.Globalization;
using FinTracker.Data;
using FinTracker.Models;
using Microsoft.Data.Sqlite;

namespace FinTracker.Services;

/// <summary>Preserves whole-month expense removal; Income requires provenance or individual user review.</summary>
public sealed class MonthlyFinancialDeletionService(BillTransactionRepository bills,
    IncomeTransactionRepository income, ICriticalOperationState criticalOperations)
{
    private async Task InitializeAsync(int year)
    {
        await bills.GetMonthlyBillCountsAsync(year);
        await income.GetMonthlyTotalsAsync(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));
    }

    public async Task<Dictionary<int, (int Imported, int Untracked)>> GetIncomeCountsAsync(int year)
    {
        await InitializeAsync(year);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await EtlImportSchema.EnsureAsync(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT CAST(substr(Transaction_date,6,2) AS INTEGER),
SUM(CASE WHEN EtlImportId IS NOT NULL THEN 1 ELSE 0 END),
SUM(CASE WHEN EtlImportId IS NULL THEN 1 ELSE 0 END)
FROM income_transaction WHERE Transaction_date >= @from AND Transaction_date < @to
GROUP BY substr(Transaction_date,6,2)";
        AddRange(command, new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));
        var counts = new Dictionary<int, (int, int)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) counts[reader.GetInt32(0)] = (reader.GetInt32(1), reader.GetInt32(2));
        return counts;
    }

    public async Task<MonthlyDeletionPlan> GetPlanAsync(int year, int month, string? importId = null)
    {
        var from = new DateTime(year, month, 1);
        await InitializeAsync(year);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await EtlImportSchema.EnsureAsync(connection);
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT 0,b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,
b.Bill_value,COALESCE(b.Note,''),COALESCE(s.Name,''),b.EtlImportId
FROM Bill_transcation b LEFT JOIN Shop s ON s.Id=b.Shop_Id
WHERE b.Bill_date >= @from AND b.Bill_date < @to AND (@import IS NULL OR b.EtlImportId=@import)
UNION ALL
SELECT 1,i.Id,i.Transaction_date,i.Shop_Id,i.Payment_Mode_Id,
i.Amount,COALESCE(i.Note,''),COALESCE(s.Name,''),i.EtlImportId
FROM income_transaction i LEFT JOIN Shop s ON s.Id=i.Shop_Id
WHERE i.Transaction_date >= @from AND i.Transaction_date < @to AND (@import IS NULL OR i.EtlImportId=@import)
ORDER BY 3,1,2";
        AddRange(command, from, from.AddMonths(1));
        command.Parameters.AddWithValue("@import", (object?)importId ?? DBNull.Value);
        var records = new List<MonthlyDeletionRecord>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) records.Add(new MonthlyDeletionRecord(
            reader.GetInt32(0) == 0 ? FinancialTransactionType.Expense : FinancialTransactionType.Income,
            reader.GetInt32(1), DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture),
            reader.GetInt32(3), reader.GetInt32(4), Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
            reader.GetString(6), reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8)) { StoredDate = reader.GetString(2) });
        return new MonthlyDeletionPlan(year, month, importId, records);
    }

    public async Task<MonthlyDeletionResult> DeleteAsync(MonthlyDeletionPlan plan,
        IReadOnlyCollection<int> reviewedLegacyIncomeIds, IProgress<double>? progress = null)
    {
        var from = new DateTime(plan.Year, plan.Month, 1);
        var untracked = plan.UntrackedIncome.ToArray();
        if (reviewedLegacyIncomeIds.Any(id => !untracked.Any(row => row.Id == id)))
            throw new InvalidOperationException("Only individually reviewed Income records can be included.");
        var rows = plan.Expenses.Concat(plan.ImportedIncome)
            .Concat(untracked.Where(row => reviewedLegacyIncomeIds.Contains(row.Id))).ToArray();
        if (rows.Any(row => row.Date < from || row.Date >= from.AddMonths(1) ||
            (plan.ImportId is not null && row.ImportId != plan.ImportId)))
            throw new InvalidOperationException("The deletion plan is outside the selected month/import.");

        using var operation = criticalOperations.Begin();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        try
        {
            await using (var scope = connection.CreateCommand())
            {
                scope.Transaction = transaction;
                scope.CommandText = @"SELECT 0,Id FROM Bill_transcation
WHERE Bill_date >= @from AND Bill_date < @to AND (@import IS NULL OR EtlImportId=@import)
UNION ALL SELECT 1,Id FROM income_transaction
WHERE Transaction_date >= @from AND Transaction_date < @to AND EtlImportId IS NOT NULL
AND (@import IS NULL OR EtlImportId=@import)";
                AddRange(scope, from, from.AddMonths(1));
                scope.Parameters.AddWithValue("@import", (object?)plan.ImportId ?? DBNull.Value);
                var expected = plan.Expenses.Concat(plan.ImportedIncome).Select(row => (row.Type, row.Id)).ToHashSet();
                var actual = new HashSet<(FinancialTransactionType, int)>();
                await using var reader = await scope.ExecuteReaderAsync();
                while (await reader.ReadAsync()) actual.Add((reader.GetInt32(0) == 0 ? FinancialTransactionType.Expense : FinancialTransactionType.Income, reader.GetInt32(1)));
                if (!actual.SetEquals(expected))
                    throw new InvalidOperationException("Monthly/import data changed during review. Nothing was deleted. Reload and review again.");
            }
            progress?.Report(0);
            for (var index = 0; index < rows.Length; index++)
            {
                var row = rows[index];
                var isIncome = row.Type == FinancialTransactionType.Income;
                var table = isIncome ? "income_transaction" : "Bill_transcation";
                var date = isIncome ? "Transaction_date" : "Bill_date";
                var amount = isIncome ? "Amount" : "Bill_value";
                await using var delete = connection.CreateCommand();
                delete.Transaction = transaction;
                // Compare the reviewed snapshot as well as ID: edits made during confirmation abort atomically.
                delete.CommandText = $@"DELETE FROM {table} WHERE Id=@id AND {date}=@date
AND Shop_Id=@shop AND Payment_Mode_Id=@mode AND CAST({amount} AS NUMERIC)=CAST(@amount AS NUMERIC)
AND COALESCE(Note,'')=@note AND COALESCE(EtlImportId,'')=@import";
                delete.Parameters.AddWithValue("@id", row.Id);
                delete.Parameters.AddWithValue("@date", row.StoredDate);
                delete.Parameters.AddWithValue("@shop", row.ShopId);
                delete.Parameters.AddWithValue("@mode", row.PaymentModeId);
                delete.Parameters.AddWithValue("@amount", row.Amount.ToString(CultureInfo.InvariantCulture));
                delete.Parameters.AddWithValue("@note", row.Note);
                delete.Parameters.AddWithValue("@import", row.ImportId ?? string.Empty);
                if (await delete.ExecuteNonQueryAsync() != 1)
                    throw new InvalidOperationException("A selected transaction changed during review. Nothing was deleted. Reload and review again.");
                progress?.Report((index + 1) * 100d / Math.Max(1, rows.Length));
            }
            await transaction.CommitAsync();
            progress?.Report(100);
            return new MonthlyDeletionResult(rows.Count(row => row.Type == FinancialTransactionType.Expense),
                rows.Count(row => row.Type == FinancialTransactionType.Income), untracked.Length - reviewedLegacyIncomeIds.Distinct().Count());
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    private static void AddRange(SqliteCommand command, DateTime from, DateTime to)
    {
        command.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
    }
}
