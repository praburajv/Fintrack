using FinTracker.Models;
using FinTracker.Services;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace FinTracker.Data;

public sealed class IncomeTransactionRepository
{
    private readonly SemaphoreSlim _initialization = new(1, 1);
    private readonly ICriticalOperationState _criticalOperations;
    private bool _initialized;

    public IncomeTransactionRepository(ICriticalOperationState criticalOperations) => _criticalOperations = criticalOperations;

    private async Task InitAsync()
    {
        if (_initialized) return;
        await _initialization.WaitAsync();
        try
        {
            if (_initialized) return;
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = @"PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Shop (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name VARCHAR(225) NOT NULL COLLATE NOCASE UNIQUE);
CREATE TABLE IF NOT EXISTS Payment_mode (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name VARCHAR(225) NOT NULL COLLATE NOCASE UNIQUE);
CREATE TABLE IF NOT EXISTS income_transaction (
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 Transaction_date DATETIME NOT NULL,
 Shop_Id INTEGER NOT NULL,
 Payment_Mode_Id INTEGER NOT NULL,
 Amount NUMERIC NOT NULL CHECK(Amount > 0),
 Note VARCHAR(1000),
 FOREIGN KEY(Shop_Id) REFERENCES Shop(Id),
 FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));
CREATE INDEX IF NOT EXISTS IX_income_transaction_Transaction_date ON income_transaction(Transaction_date);
CREATE INDEX IF NOT EXISTS IX_income_transaction_Shop_Id ON income_transaction(Shop_Id);
CREATE INDEX IF NOT EXISTS IX_income_transaction_Payment_Mode_Id ON income_transaction(Payment_Mode_Id);";
            await command.ExecuteNonQueryAsync();
            await ShopRepository.EnsureCategoryColumnAsync(connection);
            await CategoryRepository.EnsureSchemaAndSeedAsync(connection);
            await ShopRepository.EnsureCategoryIdColumnAndMigrationAsync(connection);
            await EtlImportSchema.EnsureAsync(connection);
            _initialized = true;
        }
        finally { _initialization.Release(); }
    }

    public async Task<List<IncomeTransaction>> ListAsync(DateTime from, DateTime to)
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT i.Id,i.Transaction_date,i.Shop_Id,i.Payment_Mode_Id,i.Amount,i.Note,
s.Name,p.Name,COALESCE(c.Id,0),COALESCE(c.Name,s.Category,'Credits'),COALESCE(c.IconKey,'default_shop')
FROM income_transaction i
LEFT JOIN Shop s ON s.Id=i.Shop_Id
LEFT JOIN Payment_mode p ON p.Id=i.Payment_Mode_Id
LEFT JOIN Category c ON c.Id=s.Category_Id
WHERE i.Transaction_date >= @from AND i.Transaction_date < @to
ORDER BY i.Transaction_date,i.Id";
        command.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<IncomeTransaction>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(Read(reader));
        return result;
    }

    public async Task<IncomeTransaction?> GetAsync(int id)
    {
        var rows = await ListAsync(DateTime.MinValue, DateTime.MaxValue);
        return rows.FirstOrDefault(x => x.Id == id);
    }

    public async Task<Dictionary<(int Year, int Month), decimal>> GetMonthlyTotalsAsync(DateTime from, DateTime to)
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT substr(Transaction_date,1,7), SUM(CAST(Amount AS NUMERIC))
FROM income_transaction WHERE Transaction_date >= @from AND Transaction_date < @to
GROUP BY substr(Transaction_date,1,7)";
        command.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new Dictionary<(int Year, int Month), decimal>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var parts = reader.GetString(0).Split('-');
            if (parts.Length == 2 && int.TryParse(parts[0], out var year) && int.TryParse(parts[1], out var month))
                result[(year, month)] = ParseDecimal(reader.GetValue(1));
        }
        return result;
    }

    public async Task<decimal> GetAnnualTotalAsync(int year) => (await GetMonthlyTotalsAsync(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1))).Values.Sum();

    public async Task<List<MonthlyExpenseChartData>> GetMonthlyChartAsync(int year)
    {
        var totals = await GetMonthlyTotalsAsync(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));
        return Enumerable.Range(1, 12).Select(month => new MonthlyExpenseChartData { MonthNumber = month, MonthName = new DateTime(2000, month, 1).ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant(), TotalValue = totals.GetValueOrDefault((year, month)), Color = ChartColorPalette.ForMonth(month) }).ToList();
    }

    public async Task<List<WeeklyExpenseChartData>> GetWeeklyChartAsync(DateTime weekStart)
    {
        var rows = await ListAsync(weekStart, weekStart.AddDays(7));
        return Enumerable.Range(0, 7).Select(offset => { var date = weekStart.Date.AddDays(offset); return new WeeklyExpenseChartData { DayNumber = offset + 1, DayName = date.ToString("ddd", CultureInfo.InvariantCulture), Date = date, TotalValue = rows.Where(x => x.TransactionDate.Date == date).Sum(x => x.Amount) }; }).ToList();
    }

    public async Task<List<DailyExpenseCalendarItem>> GetDailyChartAsync(int year, int month)
    {
        var rows = await ListAsync(new DateTime(year, month, 1), new DateTime(year, month, 1).AddMonths(1));
        return Enumerable.Range(1, DateTime.DaysInMonth(year, month)).Select(day => new DailyExpenseCalendarItem { Date = new DateTime(year, month, day), DayNumber = day, TotalValue = rows.Where(x => x.TransactionDate.Day == day).Sum(x => x.Amount) }).ToList();
    }

    public async Task<List<CategoryExpenseChartData>> GetCategoryChartAsync(DateTime from, DateTime to)
    {
        var rows = await ListAsync(from, to);
        return rows.GroupBy(x => new { x.CategoryId, x.CategoryName }).Select(x => new CategoryExpenseChartData { CategoryId = x.Key.CategoryId, CategoryName = x.Key.CategoryName, TotalValue = x.Sum(item => item.Amount), Color = ChartColorPalette.ForShop(x.Key.CategoryName) }).OrderByDescending(x => x.TotalValue).ToList();
    }

    public async Task<int> SaveAsync(IncomeTransaction item, string? etlImportId = null)
    {
        if (item.Amount <= 0) throw new ArgumentException("Income amount must be greater than zero.", nameof(item));
        using var operation = _criticalOperations.Begin();
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.Parameters.AddWithValue("@date", item.TransactionDate.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@shop", item.ShopId);
        command.Parameters.AddWithValue("@mode", item.PaymentModeId);
        command.Parameters.AddWithValue("@amount", item.Amount.ToString(CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@note", string.IsNullOrWhiteSpace(item.Note) ? DBNull.Value : item.Note.Trim());
        if (item.Id == 0)
        {
            command.CommandText = "INSERT INTO income_transaction(Transaction_date,Shop_Id,Payment_Mode_Id,Amount,Note,EtlImportId) VALUES(@date,@shop,@mode,@amount,@note,@import); SELECT last_insert_rowid();";
            command.Parameters.AddWithValue("@import", (object?)etlImportId ?? DBNull.Value);
            item.Id = Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        else
        {
            command.CommandText = "UPDATE income_transaction SET Transaction_date=@date,Shop_Id=@shop,Payment_Mode_Id=@mode,Amount=@amount,Note=@note WHERE Id=@id";
            command.Parameters.AddWithValue("@id", item.Id);
            await command.ExecuteNonQueryAsync();
        }
        return item.Id;
    }

    public async Task<int> DeleteAsync(int id)
    {
        using var operation = _criticalOperations.Begin();
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM income_transaction WHERE Id=@id";
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<int> DeleteForMonthAsync(int year, int month)
    {
        using var operation = _criticalOperations.Begin();
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        var from = new DateTime(year, month, 1);
        command.CommandText = "DELETE FROM income_transaction WHERE Transaction_date >= @from AND Transaction_date < @to";
        command.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@to", from.AddMonths(1).ToString("O", CultureInfo.InvariantCulture));
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<int> DeleteForShopAsync(int shopId)
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM income_transaction WHERE Shop_Id=@shop";
        command.Parameters.AddWithValue("@shop", shopId);
        return await command.ExecuteNonQueryAsync();
    }

    private static IncomeTransaction Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        TransactionDate = DateTime.Parse(reader.GetString(1), CultureInfo.InvariantCulture),
        ShopId = reader.GetInt32(2),
        PaymentModeId = reader.GetInt32(3),
        Amount = ParseDecimal(reader.GetValue(4)),
        Note = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
        ShopName = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
        PaymentModeName = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
        CategoryId = reader.IsDBNull(8) ? 0 : reader.GetInt32(8),
        CategoryName = reader.IsDBNull(9) ? "Credits" : reader.GetString(9),
        CategoryIconKey = reader.IsDBNull(10) ? "default_shop" : reader.GetString(10)
    };

    private static decimal ParseDecimal(object value) => decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out var result) ? result : 0;
}
