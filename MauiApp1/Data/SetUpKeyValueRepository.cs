using System.Globalization;
using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Data.Sqlite;

namespace MauiApp1.Data;

public sealed class SetUpKeyValueRepository(ICriticalOperationState criticalOperations)
{
    public const string MonthlyIncomeKey = "Monthly Income";
    public const string MonthlyBudgetKey = "Monthly Budget";
    private const decimal DefaultMonthlyIncome = 3000m;
    private const decimal DefaultMonthlyBudget = 2000m;
    private readonly SemaphoreSlim _schemaGate = new(1, 1);

    public async Task<BudgetSettings> GetBudgetSettingsAsync()
    {
        await EnsureRequiredSettingsAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT \"Key\", Value FROM SetUpKeyValue WHERE \"Key\" IN (@incomeKey, @budgetKey)";
        command.Parameters.AddWithValue("@incomeKey", MonthlyIncomeKey);
        command.Parameters.AddWithValue("@budgetKey", MonthlyBudgetKey);

        var income = DefaultMonthlyIncome;
        var budget = DefaultMonthlyBudget;
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0);
            var value = ParseStoredDecimal(reader.GetString(1), key);
            if (key.Equals(MonthlyIncomeKey, StringComparison.OrdinalIgnoreCase)) income = value;
            else if (key.Equals(MonthlyBudgetKey, StringComparison.OrdinalIgnoreCase)) budget = value;
        }

        return new BudgetSettings(income, budget);
    }

    public Task UpdateMonthlyIncomeAsync(decimal value) => UpdateDecimalAsync(MonthlyIncomeKey, value);
    public Task UpdateMonthlyBudgetAsync(decimal value) => UpdateDecimalAsync(MonthlyBudgetKey, value);

    public async Task EnsureRequiredSettingsAsync()
    {
        await _schemaGate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();
            await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
            try
            {
                await using (var create = connection.CreateCommand())
                {
                    create.Transaction = transaction;
                    create.CommandText = "CREATE TABLE IF NOT EXISTS SetUpKeyValue (Id INTEGER PRIMARY KEY AUTOINCREMENT, \"Key\" TEXT NOT NULL COLLATE NOCASE UNIQUE, Value TEXT NOT NULL);";
                    await create.ExecuteNonQueryAsync();
                }

                await InsertDefaultAsync(connection, transaction, MonthlyIncomeKey, DefaultMonthlyIncome);
                await InsertDefaultAsync(connection, transaction, MonthlyBudgetKey, DefaultMonthlyBudget);
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
        finally
        {
            _schemaGate.Release();
        }
    }

    private async Task UpdateDecimalAsync(string key, decimal value)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(nameof(value), "The setting value cannot be negative.");
        using var operation = criticalOperations.Begin();
        await EnsureRequiredSettingsAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE SetUpKeyValue SET Value=@value WHERE \"Key\"=@key COLLATE NOCASE";
        command.Parameters.AddWithValue("@value", value.ToString("0.00############################", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@key", key);
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException($"The required setting '{key}' could not be updated.");
    }

    private static async Task InsertDefaultAsync(SqliteConnection connection, SqliteTransaction transaction, string key, decimal value)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "INSERT OR IGNORE INTO SetUpKeyValue(\"Key\", Value) VALUES(@key, @value)";
        command.Parameters.AddWithValue("@key", key);
        command.Parameters.AddWithValue("@value", value.ToString("0.00", CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync();
    }

    private static decimal ParseStoredDecimal(string value, string key)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            return parsed;
        throw new InvalidDataException($"The stored value for '{key}' is not a valid non-negative amount.");
    }
}
