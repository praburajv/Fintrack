using Microsoft.Data.Sqlite;
using FinTracker.Services;

namespace FinTracker.Data;

public sealed class MonthlyBudgetRepository
{
    private readonly FinancialAccountContext _accounts;
    public MonthlyBudgetRepository(FinancialAccountContext accounts) => _accounts = accounts;
    private bool _initialized;
    public void InvalidateAfterDatabaseRestore() => _initialized = false;

    private async Task InitAsync()
    {
        if (_initialized) return;
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "CREATE TABLE IF NOT EXISTS MonthlyBudgetIncome (FinancialAccountId INTEGER NOT NULL, Year INTEGER NOT NULL, Month INTEGER NOT NULL, Income REAL NOT NULL DEFAULT 0, PRIMARY KEY (FinancialAccountId, Year, Month));";
        await command.ExecuteNonQueryAsync();
        _initialized = true;
    }

    public async Task<decimal> GetIncomeAsync(DateTime month)
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT Income FROM MonthlyBudgetIncome WHERE FinancialAccountId=@account AND Year=@year AND Month=@month";
        command.Parameters.AddWithValue("@account", _accounts.RequireId());
        command.Parameters.AddWithValue("@year", month.Year); command.Parameters.AddWithValue("@month", month.Month);
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? 0 : Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task SaveIncomeAsync(DateTime month, decimal income)
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO MonthlyBudgetIncome(FinancialAccountId,Year,Month,Income) VALUES(@account,@year,@month,@income) ON CONFLICT(FinancialAccountId,Year,Month) DO UPDATE SET Income=excluded.Income";
        command.Parameters.AddWithValue("@account", _accounts.RequireId());
        command.Parameters.AddWithValue("@year", month.Year); command.Parameters.AddWithValue("@month", month.Month); command.Parameters.AddWithValue("@income", income);
        await command.ExecuteNonQueryAsync();
    }
}
