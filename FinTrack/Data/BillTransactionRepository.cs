using FinTracker.Models;
using FinTracker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace FinTracker.Data;

public class BillTransactionRepository
{
    private bool _initialized;
    public Task EnsureInitializedAsync() => Init();
    public void InvalidateAfterDatabaseRestore() => _initialized = false;
    private readonly SemaphoreSlim _initialization = new(1, 1);
    private readonly ILogger<BillTransactionRepository> _logger;
    private readonly ICriticalOperationState _criticalOperations;
    private readonly FinancialAccountContext _accounts;
    public BillTransactionRepository(ILogger<BillTransactionRepository> logger, ICriticalOperationState criticalOperations, FinancialAccountContext accounts)
    {
        _logger = logger;
        _criticalOperations = criticalOperations;
        _accounts = accounts;
    }

    private async Task Init()
    {
        if (_initialized) return;
        await _initialization.WaitAsync();
        try
        {
            if (_initialized) return;
            await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
            var cmd = c.CreateCommand();
            cmd.CommandText = @"PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Shop (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name VARCHAR(225) NOT NULL COLLATE NOCASE UNIQUE);
CREATE TABLE IF NOT EXISTS Payment_mode (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name VARCHAR(225) NOT NULL COLLATE NOCASE UNIQUE);
CREATE TABLE IF NOT EXISTS Bill_transcation (
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 Bill_date DATETIME NOT NULL,
 Shop_Id INTEGER NOT NULL,
 Payment_Mode_Id INTEGER NOT NULL,
 Bill_value NUMERIC NOT NULL,
 Note VARCHAR(1000),
 FinancialAccountId INTEGER NOT NULL,
 FOREIGN KEY(Shop_Id) REFERENCES Shop(Id),
 FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));
CREATE INDEX IF NOT EXISTS IX_Bill_transcation_Bill_date ON Bill_transcation(Bill_date);
CREATE INDEX IF NOT EXISTS IX_Bill_transcation_Shop_Id ON Bill_transcation(Shop_Id);
CREATE INDEX IF NOT EXISTS IX_Bill_transcation_Payment_Mode_Id ON Bill_transcation(Payment_Mode_Id);
CREATE TRIGGER IF NOT EXISTS TR_BillAccountInsert BEFORE INSERT ON Bill_transcation
WHEN NEW.FinancialAccountId IS NULL OR NOT EXISTS(SELECT 1 FROM FinancialAccount WHERE Id=NEW.FinancialAccountId AND IsActive=1)
 OR NOT EXISTS(SELECT 1 FROM Shop WHERE Id=NEW.Shop_Id AND FinancialAccountId=NEW.FinancialAccountId)
 OR NOT EXISTS(SELECT 1 FROM Payment_mode WHERE Id=NEW.Payment_Mode_Id AND FinancialAccountId=NEW.FinancialAccountId)
BEGIN SELECT RAISE(ABORT,'Bill account, shop or payment mode mismatch'); END;
CREATE TRIGGER IF NOT EXISTS TR_BillAccountUpdate BEFORE UPDATE ON Bill_transcation
WHEN NEW.FinancialAccountId IS NULL OR NOT EXISTS(SELECT 1 FROM Shop WHERE Id=NEW.Shop_Id AND FinancialAccountId=NEW.FinancialAccountId)
 OR NOT EXISTS(SELECT 1 FROM Payment_mode WHERE Id=NEW.Payment_Mode_Id AND FinancialAccountId=NEW.FinancialAccountId)
BEGIN SELECT RAISE(ABORT,'Bill account, shop or payment mode mismatch'); END;
CREATE TABLE IF NOT EXISTS ETL_TEXT_MAP (
 ID INTEGER PRIMARY KEY AUTOINCREMENT,
 LOOKUPTEXT VARCHAR(225) NOT NULL,
 SHOP_ID INTEGER NOT NULL,
 FinancialAccountId INTEGER NOT NULL,
 FOREIGN KEY(SHOP_ID) REFERENCES Shop(Id));
CREATE INDEX IF NOT EXISTS IX_ETL_TEXT_MAP_SHOP_ID ON ETL_TEXT_MAP(SHOP_ID);";
            await cmd.ExecuteNonQueryAsync();
            await BillReferenceSchema.EnsureAsync(c);
            await EnsureRecurringColumnsAsync(c);
            await ShopRepository.EnsureCategoryColumnAsync(c);
            await CategoryRepository.EnsureSchemaAndSeedAsync(c);
            await ShopRepository.EnsureCategoryIdColumnAndMigrationAsync(c);
            await EtlImportSchema.EnsureAsync(c);
            _initialized = true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating Bill_transcation table"); throw; }
        finally { _initialization.Release(); }
    }

    internal static async Task EnsureRecurringColumnsAsync(SqliteConnection connection)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var check = connection.CreateCommand(); check.CommandText = "PRAGMA table_info(Bill_transcation)";
        await using (var reader = await check.ExecuteReaderAsync()) while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
        if (!columns.Contains("Recurring_Bill_Id")) { var add = connection.CreateCommand(); add.CommandText = "ALTER TABLE Bill_transcation ADD COLUMN Recurring_Bill_Id INTEGER NULL"; await add.ExecuteNonQueryAsync(); }
        if (!columns.Contains("Recurring_Period")) { var add = connection.CreateCommand(); add.CommandText = "ALTER TABLE Bill_transcation ADD COLUMN Recurring_Period TEXT NULL"; await add.ExecuteNonQueryAsync(); }
        var index = connection.CreateCommand(); index.CommandText = "CREATE UNIQUE INDEX IF NOT EXISTS UX_Bill_transcation_Recurring_Period ON Bill_transcation(Recurring_Bill_Id, Recurring_Period) WHERE Recurring_Bill_Id IS NOT NULL AND Recurring_Period IS NOT NULL"; await index.ExecuteNonQueryAsync();
    }

    private static BillTransaction Read(SqliteDataReader r) => new()
    {
        BillReferenceNo = r.IsDBNull(12) ? null : r.GetString(12),
        Id = r.GetInt32(0), BillDate = DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture), ShopId = r.GetInt32(2),
        PaymentModeId = r.GetInt32(3), BillValue = ParseDecimal(r.GetValue(4)), Note = r.IsDBNull(5) ? string.Empty : r.GetString(5),
        ShopName = r.IsDBNull(6) ? string.Empty : r.GetString(6), PaymentModeName = r.IsDBNull(7) ? string.Empty : r.GetString(7), ShopCategory = ShopCategoryCatalog.Normalize(r.IsDBNull(8) ? null : r.GetString(8)), ShopCategoryId = r.IsDBNull(9) ? 0 : r.GetInt32(9), ShopCategoryName = r.IsDBNull(10) ? "Default Shop" : r.GetString(10), ShopCategoryIconKey = r.IsDBNull(11) ? "default_shop" : r.GetString(11)
    };

    private static void AddParams(SqliteCommand cmd, BillTransaction item)
    {
        cmd.Parameters.AddWithValue("@reference", (object?)FinTracker.Utilities.TransactionInput.NormalizeReference(item.BillReferenceNo) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@date", item.BillDate.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@shop", item.ShopId); cmd.Parameters.AddWithValue("@mode", item.PaymentModeId);
        cmd.Parameters.AddWithValue("@value", item.BillValue.ToString(CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@note", string.IsNullOrWhiteSpace(item.Note) ? DBNull.Value : item.Note.Trim());
    }

    public async Task<List<BillTransaction>> ListAsync(int? year = null)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name,s.Category,s.Category_Id,c.Name,c.IconKey,b.bill_reference_no
FROM Bill_transcation b LEFT JOIN Shop s ON s.Id=b.Shop_Id LEFT JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id LEFT JOIN Category c ON c.Id=s.Category_Id
WHERE b.FinancialAccountId=@account";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        if (year.HasValue)
        {
            cmd.CommandText += " AND b.Bill_date >= @from AND b.Bill_date < @to";
            cmd.Parameters.AddWithValue("@from", new DateTime(year.Value, 1, 1).ToString("O", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("@to", new DateTime(year.Value + 1, 1, 1).ToString("O", CultureInfo.InvariantCulture));
        }
        cmd.CommandText += " ORDER BY b.Bill_date,b.Id";
        var result = new List<BillTransaction>(); await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) result.Add(Read(r));
        return result;
    }

    public async Task<List<BillTransaction>> ListAsync(DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name,s.Category,s.Category_Id,c.Name,c.IconKey,b.bill_reference_no FROM Bill_transcation b LEFT JOIN Shop s ON s.Id=b.Shop_Id LEFT JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id LEFT JOIN Category c ON c.Id=s.Category_Id WHERE b.FinancialAccountId=@account AND b.Bill_date >= @from AND b.Bill_date < @to ORDER BY b.Bill_date,b.Id";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<BillTransaction>(); await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(Read(r));
        return result;
    }

    public async Task<Dictionary<int, int>> GetMonthlyBillCountsAsync(int year)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CAST(strftime('%m', Bill_date) AS INTEGER), COUNT(*) FROM Bill_transcation WHERE FinancialAccountId=@account AND Bill_date >= @from AND Bill_date < @to GROUP BY strftime('%m', Bill_date)";
        command.Parameters.AddWithValue("@account", _accounts.RequireId());
        command.Parameters.AddWithValue("@from", new DateTime(year, 1, 1).ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@to", new DateTime(year + 1, 1, 1).ToString("O", CultureInfo.InvariantCulture));
        var result = new Dictionary<int, int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result[reader.GetInt32(0)] = reader.GetInt32(1);
        return result;
    }

    public async Task<int> DeleteBillsForMonthAsync(int year, int month, IProgress<double>? progress = null)
    {
        using var operation = _criticalOperations.Begin();
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        try
        {
            var from = new DateTime(year, month, 1);
            var to = from.AddMonths(1);
            var ids = new List<int>();
            await using (var select = connection.CreateCommand())
            {
                select.Transaction = transaction;
                select.CommandText = "SELECT Id FROM Bill_transcation WHERE FinancialAccountId=@account AND Bill_date >= @from AND Bill_date < @to ORDER BY Id";
                select.Parameters.AddWithValue("@account", _accounts.RequireId());
                select.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
                select.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
                await using var reader = await select.ExecuteReaderAsync();
                while (await reader.ReadAsync()) ids.Add(reader.GetInt32(0));
            }
            Report(progress, 0);
            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM Bill_transcation WHERE Id=@id AND FinancialAccountId=@account";
            delete.Parameters.AddWithValue("@account", _accounts.RequireId());
            var parameter = delete.Parameters.Add("@id", SqliteType.Integer);
            for (var index = 0; index < ids.Count; index++)
            {
                parameter.Value = ids[index];
                await delete.ExecuteNonQueryAsync();
                Report(progress, (index + 1) * 100d / Math.Max(1, ids.Count));
            }
            await transaction.CommitAsync();
            return ids.Count;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private static void Report(IProgress<double>? progress, double value) => progress?.Report(Math.Clamp(value, 0, 100));

    public async Task<Dictionary<(int Year, int Month), decimal>> GetMonthlyTotalsAsync(DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT substr(Bill_date, 1, 7), SUM(CAST(Bill_value AS NUMERIC))
FROM Bill_transcation
WHERE FinancialAccountId=@account AND Bill_date >= @from AND Bill_date < @to
GROUP BY substr(Bill_date, 1, 7)";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var totals = new Dictionary<(int Year, int Month), decimal>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var key = reader.GetString(0).Split('-');
            if (key.Length == 2 && int.TryParse(key[0], out var year) && int.TryParse(key[1], out var month))
                totals[(year, month)] = ParseDecimal(reader.GetValue(1));
        }
        return totals;
    }

    public async Task<List<BillTransaction>> GetBillsByCategoryAsync(int categoryId)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name,s.Category,s.Category_Id,c.Name,c.IconKey,b.bill_reference_no
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id LEFT JOIN Category c ON c.Id=s.Category_Id
WHERE b.FinancialAccountId=@account AND s.Category_Id=@category ORDER BY b.Bill_date,b.Id";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@category", categoryId);
        var result = new List<BillTransaction>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) result.Add(Read(r)); return result;
    }

    public async Task<List<BillTransaction>> GetBillsByCategoryAsync(int categoryId, DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name,s.Category,s.Category_Id,c.Name,c.IconKey,b.bill_reference_no
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id LEFT JOIN Category c ON c.Id=s.Category_Id
WHERE b.FinancialAccountId=@account AND ((@category=0 AND COALESCE(s.Category_Id,0)=0) OR s.Category_Id=@category)
  AND b.Bill_date >= @from AND b.Bill_date < @to
ORDER BY b.Bill_date,b.Id";
        cmd.Parameters.AddWithValue("@category", categoryId);
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<BillTransaction>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(Read(r));
        return result;
    }

    public async Task<List<BillTransaction>> GetBillsByShopIdAsync(int shopId)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name,s.Category,s.Category_Id,c.Name,c.IconKey,b.bill_reference_no
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id LEFT JOIN Category c ON c.Id=s.Category_Id
WHERE b.FinancialAccountId=@account AND b.Shop_Id=@shop ORDER BY b.Bill_date,b.Id";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@shop", shopId);
        var result = new List<BillTransaction>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) result.Add(Read(r)); return result;
    }

    public async Task<int> ReassignShopIdsAsync(int currentShopId, IReadOnlyDictionary<int, int> assignments)
    {
        if (assignments.Count == 0) return 0;
        using var operation = _criticalOperations.Begin();
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        await using var transaction = c.BeginTransaction();
        try
        {
            var command = c.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE Bill_transcation SET Shop_Id=@newShop WHERE Id=@bill AND Shop_Id=@currentShop AND FinancialAccountId=@account AND EXISTS(SELECT 1 FROM Shop WHERE Id=@newShop AND FinancialAccountId=@account)";
            command.Parameters.AddWithValue("@account", _accounts.RequireId());
            command.Parameters.Add("@newShop", SqliteType.Integer);
            command.Parameters.Add("@bill", SqliteType.Integer);
            command.Parameters.Add("@currentShop", SqliteType.Integer);
            command.Parameters["@currentShop"].Value = currentShopId;
            var updated = 0;
            foreach (var assignment in assignments)
            {
                command.Parameters["@newShop"].Value = assignment.Value;
                command.Parameters["@bill"].Value = assignment.Key;
                updated += await command.ExecuteNonQueryAsync();
            }
            await transaction.CommitAsync();
            return updated;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<decimal> GetAnnualTotalAsync(int year)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COALESCE(SUM(CAST(Bill_value AS NUMERIC)), 0) FROM Bill_transcation WHERE FinancialAccountId=@account AND Bill_date >= @from AND Bill_date < @to";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@from", new DateTime(year, 1, 1).ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", new DateTime(year + 1, 1, 1).ToString("O", CultureInfo.InvariantCulture));
        return ParseDecimal(await cmd.ExecuteScalarAsync());
    }

    public Task<List<ShopExpenseChartData>> GetExpensesByShopAsync(int year) => GetExpensesByShopAsync(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));

    public Task<List<ShopExpenseChartData>> GetExpensesByShopForMonthAsync(int year, int month)
        => GetExpensesByShopAsync(new DateTime(year, month, 1), new DateTime(year, month, 1).AddMonths(1));

    public Task<List<CategoryExpenseChartData>> GetExpensesByCategoryAsync(int year)
        => GetExpensesByCategoryAsync(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));

    public Task<List<CategoryExpenseChartData>> GetExpensesByCategoryForMonthAsync(int year, int month)
        => GetExpensesByCategoryAsync(new DateTime(year, month, 1), new DateTime(year, month, 1).AddMonths(1));

    private async Task<List<CategoryExpenseChartData>> GetExpensesByCategoryAsync(DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT COALESCE(c.Id, 0), COALESCE(c.Name, s.Category, 'Uncategorized'), SUM(CAST(b.Bill_value AS NUMERIC))
FROM Bill_transcation b
JOIN Shop s ON s.Id=b.Shop_Id
LEFT JOIN Category c ON c.Id=s.Category_Id
WHERE b.FinancialAccountId=@account AND b.Bill_date >= @from AND b.Bill_date < @to
GROUP BY COALESCE(c.Id, 0), COALESCE(c.Name, s.Category, 'Uncategorized')
ORDER BY COALESCE(c.Name, s.Category, 'Uncategorized')";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<CategoryExpenseChartData>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var name = r.GetString(1);
            result.Add(new CategoryExpenseChartData
            {
                CategoryId = r.GetInt32(0),
                CategoryName = name,
                TotalValue = ParseDecimal(r.GetValue(2)),
                Color = ChartColorPalette.ForShop(name)
            });
        }
        return result;
    }

    public async Task<List<ShopExpenseChartData>> GetExpensesByShopAsync(DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT s.Id, s.Name, COUNT(*), SUM(CAST(b.Bill_value AS NUMERIC)) FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id WHERE b.FinancialAccountId=@account AND b.Bill_date >= @from AND b.Bill_date < @to GROUP BY b.Shop_Id, s.Name ORDER BY COUNT(*) DESC, s.Name";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<ShopExpenseChartData>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) { var name = r.GetString(1); result.Add(new ShopExpenseChartData { SerialNumber = result.Count + 1, ShopId = r.GetInt32(0), ShopName = name, ShopDisplayName = name, VisitCount = r.GetInt32(2), TotalValue = ParseDecimal(r.GetValue(3)), Color = ChartColorPalette.ForShop(name) }); } return result;
    }

    public async Task<List<WeeklyExpenseChartData>> GetExpensesByWeekAsync(DateTime weekStart)
    {
        // Build the chart from the same parsed calendar dates used by the daily
        // drill-down. This avoids SQLite weekday-number/time-format differences
        // causing a bar to point at a different day's detail page.
        var end = weekStart.Date.AddDays(7);
        var bills = await GetBillsForDateRangeAsync(weekStart.Date, end);
        var totals = bills
            .Where(b => b.BillDate >= weekStart.Date && b.BillDate < end)
            .GroupBy(b => b.BillDate.Date)
            .ToDictionary(g => g.Key, g => g.Sum(b => b.BillValue));
        return Enumerable.Range(0, 7).Select(day =>
        {
            var date = weekStart.Date.AddDays(day);
            return new WeeklyExpenseChartData { DayNumber = day + 1, DayName = date.ToString("ddd", CultureInfo.InvariantCulture), Date = date, TotalValue = totals.GetValueOrDefault(date) };
        }).ToList();
    }

    public async Task<List<MonthlyVisitData>> GetShopMonthlyVisitsAsync(int shopId, int year)
    {
        await Init();
        var from = new DateTime(year, 1, 1);
        var to = from.AddYears(1);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT CAST(substr(Bill_date, 6, 2) AS INTEGER), COUNT(*), SUM(CAST(Bill_value AS NUMERIC))
FROM Bill_transcation
WHERE FinancialAccountId=@account AND Shop_Id=@shop AND Bill_date >= @from AND Bill_date < @to
GROUP BY substr(Bill_date, 6, 2)
ORDER BY substr(Bill_date, 6, 2)";
        command.Parameters.AddWithValue("@account", _accounts.RequireId());
        command.Parameters.AddWithValue("@shop", shopId);
        command.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var aggregates = new Dictionary<int, (int Count, decimal Total)>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) aggregates[reader.GetInt32(0)] = (reader.GetInt32(1), ParseDecimal(reader.GetValue(2)));
        return Enumerable.Range(1, 12).Select(month => new MonthlyVisitData
        {
            MonthNumber = month,
            MonthName = new DateTime(2000, month, 1).ToString("MMM", CultureInfo.InvariantCulture),
            VisitCount = aggregates.GetValueOrDefault(month).Count,
            TotalSpent = aggregates.GetValueOrDefault(month).Total,
            Color = ChartColorPalette.ForMonth(month)
        }).ToList();
    }

    public async Task<List<int>> GetShopTransactionYearsAsync(int shopId)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT DISTINCT CAST(substr(Bill_date, 1, 4) AS INTEGER)
FROM Bill_transcation
WHERE FinancialAccountId=@account AND Shop_Id=@shop
ORDER BY 1 DESC";
        command.Parameters.AddWithValue("@account", _accounts.RequireId());
        command.Parameters.AddWithValue("@shop", shopId);
        var years = new List<int>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) years.Add(reader.GetInt32(0));
        return years;
    }

    public async Task<List<ShopTransactionDateSummary>> GetShopTransactionDatesAsync(int shopId, int year, int month)
    {
        await Init();
        var from = new DateTime(year, month, 1);
        var to = from.AddMonths(1);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT substr(Bill_date, 1, 10), COUNT(*), SUM(CAST(Bill_value AS NUMERIC))
FROM Bill_transcation
WHERE FinancialAccountId=@account AND Shop_Id=@shop AND Bill_date >= @from AND Bill_date < @to
GROUP BY substr(Bill_date, 1, 10)
ORDER BY substr(Bill_date, 1, 10)";
        command.Parameters.AddWithValue("@account", _accounts.RequireId());
        command.Parameters.AddWithValue("@shop", shopId);
        command.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<ShopTransactionDateSummary>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ShopTransactionDateSummary
            {
                Date = DateTime.ParseExact(reader.GetString(0), "yyyy-MM-dd", CultureInfo.InvariantCulture),
                BillCount = reader.GetInt32(1),
                TotalSpent = ParseDecimal(reader.GetValue(2))
            });
        }
        return result;
    }

    public Task<List<BillTransaction>> GetBillsByShopAndDateAsync(int shopId, DateTime date)
        => GetBillsByShopAsync(shopId, date.Date, date.Date.AddDays(1));
    public Task<List<BillTransaction>> GetBillsByShopAndRangeAsync(int shopId, DateTime from, DateTime to)
        => GetBillsByShopAsync(shopId, from, to);

    public Task<List<BillTransaction>> GetBillsByShopAndYearAsync(int shopId, int year)
        => GetBillsByShopAsync(shopId, new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));

    public Task<List<BillTransaction>> GetBillsByShopAndMonthAsync(int shopId, int year, int month)
        => GetBillsByShopAsync(shopId, new DateTime(year, month, 1), new DateTime(year, month, 1).AddMonths(1));

    public async Task<List<PaymentModeExpenseChartData>> GetPaymentModeExpenseTotalsAsync(DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand();
        cmd.CommandText = @"SELECT p.Id, p.Name, SUM(CAST(b.Bill_value AS NUMERIC))
FROM Bill_transcation b JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id
WHERE b.FinancialAccountId=@account AND b.Bill_date >= @from AND b.Bill_date < @to
GROUP BY p.Id, p.Name ORDER BY p.Name";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<PaymentModeExpenseChartData>(); await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var name = r.GetString(1);
            result.Add(new PaymentModeExpenseChartData { PaymentModeId = r.GetInt32(0), PaymentModeName = name, DisplayName = ShopNameAbbreviator.Abbreviate(name), TotalValue = ParseDecimal(r.GetValue(2)), Color = ChartColorPalette.ForShop(name) });
        }
        return result;
    }

    public async Task<List<BillTransaction>> GetBillsByPaymentModeAndDateRangeAsync(int paymentModeId, DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name,s.Category,s.Category_Id,c.Name,c.IconKey,b.bill_reference_no
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id LEFT JOIN Category c ON c.Id=s.Category_Id
WHERE b.FinancialAccountId=@account AND b.Payment_Mode_Id=@mode AND b.Bill_date >= @from AND b.Bill_date < @to ORDER BY b.Bill_date DESC,b.Id DESC";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@mode", paymentModeId); cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<BillTransaction>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) result.Add(Read(r)); return result;
    }

    public async Task<Dictionary<int, decimal>> GetDailyExpenseTotalsAsync(int year, int month)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT Bill_date, Bill_value
FROM Bill_transcation
WHERE FinancialAccountId=@account AND Bill_date >= @from AND Bill_date < @to
";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@from", new DateTime(year, month, 1).ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", new DateTime(year, month, 1).AddMonths(1).ToString("O", CultureInfo.InvariantCulture));
        var totals = new Dictionary<int, decimal>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var billDate = DateTime.Parse(r.GetString(0), CultureInfo.InvariantCulture);
            if (billDate.Year != year || billDate.Month != month) continue;
            totals[billDate.Day] = totals.GetValueOrDefault(billDate.Day) + ParseDecimal(r.GetValue(1));
        }

        return totals;
    }

    public Task<List<BillTransaction>> GetBillsForDateAsync(DateTime date)
        => GetBillsForDateRangeAsync(date.Date, date.Date.AddDays(1));

    public Task<List<BillTransaction>> GetBillsForDateRangeAsync(DateTime from, DateTime to)
        => GetBillsForDateRangeCoreAsync(from, to);

    private async Task<List<BillTransaction>> GetBillsForDateRangeCoreAsync(DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name,s.Category,s.Category_Id,c.Name,c.IconKey,b.bill_reference_no
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id LEFT JOIN Category c ON c.Id=s.Category_Id
WHERE b.FinancialAccountId=@account AND b.Bill_date >= @from AND b.Bill_date < @to ORDER BY b.Bill_date DESC,b.Id DESC";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<BillTransaction>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) result.Add(Read(r)); return result;
    }

    private async Task<List<BillTransaction>> GetBillsByShopAsync(int shopId, DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name,s.Category,s.Category_Id,c.Name,c.IconKey,b.bill_reference_no
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id LEFT JOIN Category c ON c.Id=s.Category_Id
WHERE b.FinancialAccountId=@account AND b.Shop_Id=@shop AND b.Bill_date >= @from AND b.Bill_date < @to ORDER BY b.Bill_date DESC,b.Id DESC";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@shop", shopId); cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<BillTransaction>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) result.Add(Read(r)); return result;
    }

    public async Task<List<MonthlyExpenseChartData>> GetExpensesByMonthAsync(int year)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT CAST(strftime('%m', Bill_date) AS INTEGER), SUM(CAST(Bill_value AS NUMERIC)) FROM Bill_transcation WHERE FinancialAccountId=@account AND Bill_date >= @from AND Bill_date < @to GROUP BY strftime('%m', Bill_date) ORDER BY strftime('%m', Bill_date)";
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        cmd.Parameters.AddWithValue("@from", new DateTime(year, 1, 1).ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", new DateTime(year + 1, 1, 1).ToString("O", CultureInfo.InvariantCulture));
        var totals = new Dictionary<int, decimal>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) totals[r.GetInt32(0)] = ParseDecimal(r.GetValue(1));
        return Enumerable.Range(1, 12).Select(month => new MonthlyExpenseChartData { MonthNumber = month, MonthName = new DateTime(2000, month, 1).ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant(), TotalValue = totals.GetValueOrDefault(month) }).ToList();
    }

    private static decimal ParseDecimal(object? value) => value is null || value == DBNull.Value ? 0m : decimal.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);

    public async Task<BillTransaction?> GetAsync(int id)
    {
        await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText=@"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name,s.Category,s.Category_Id,c.Name,c.IconKey,b.bill_reference_no FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id LEFT JOIN Category c ON c.Id=s.Category_Id WHERE b.Id=@id AND b.FinancialAccountId=@account"; cmd.Parameters.AddWithValue("@id",id); cmd.Parameters.AddWithValue("@account",_accounts.RequireId()); await using var r=await cmd.ExecuteReaderAsync(); return await r.ReadAsync()?Read(r):null;
    }

    public async Task<bool> IsShopReferencedAsync(int id) => await IsReferencedAsync("Shop_Id", id);
    public async Task<bool> IsPaymentModeReferencedAsync(int id) => await IsReferencedAsync("Payment_Mode_Id", id);
    private async Task<bool> IsReferencedAsync(string column,int id) { await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText=$"SELECT COUNT(*) FROM Bill_transcation WHERE {column}=@id AND FinancialAccountId=@account"; cmd.Parameters.AddWithValue("@id",id); cmd.Parameters.AddWithValue("@account",_accounts.RequireId()); return Convert.ToInt32(await cmd.ExecuteScalarAsync())>0; }

    public async Task<int> DeleteBillsByCategoryAsync(int categoryId)
    {
        using var operation = _criticalOperations.Begin();
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        await using var transaction = c.BeginTransaction();
        try
        {
            var command = c.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "DELETE FROM Bill_transcation WHERE FinancialAccountId=@account AND Shop_Id IN (SELECT Id FROM Shop WHERE Category_Id=@category AND FinancialAccountId=@account)";
            command.Parameters.AddWithValue("@account", _accounts.RequireId());
            command.Parameters.AddWithValue("@category", categoryId);
            var deleted = await command.ExecuteNonQueryAsync();
            await transaction.CommitAsync();
            return deleted;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<int> DeleteBillsByCategoryForMonthAsync(int categoryId, int year, int month)
    {
        using var operation = _criticalOperations.Begin();
        await Init();
        var from = new DateTime(year, month, 1);
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"DELETE FROM Bill_transcation
WHERE FinancialAccountId=@account AND Bill_date>=@from AND Bill_date<@to
AND Shop_Id IN (SELECT Id FROM Shop WHERE FinancialAccountId=@account AND Category_Id=@category)";
        command.Parameters.AddWithValue("@account", _accounts.RequireId());
        command.Parameters.AddWithValue("@category", categoryId);
        command.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@to", from.AddMonths(1).ToString("O", CultureInfo.InvariantCulture));
        return await command.ExecuteNonQueryAsync();
    }

    public async Task<int> SaveItemAsync(BillTransaction item, string? etlImportId = null)
    {
        using var operation = _criticalOperations.Begin();
        await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); AddParams(cmd,item);
        if (etlImportId is null) await TransactionWriteValidation.ValidateNoteAsync(c, "Bill_transcation", item.Id, _accounts.RequireId(), item.Note);
        cmd.Parameters.AddWithValue("@account", _accounts.RequireId());
        if (item.Id == 0)
        {
            cmd.CommandText = @"INSERT INTO Bill_transcation(Bill_date,Shop_Id,Payment_Mode_Id,Bill_value,Note,EtlImportId,FinancialAccountId,bill_reference_no)
SELECT @date,@shop,@mode,@value,@note,@import,@account,@reference
WHERE EXISTS(SELECT 1 FROM Shop WHERE Id=@shop AND FinancialAccountId=@account)
AND EXISTS(SELECT 1 FROM Payment_mode WHERE Id=@mode AND FinancialAccountId=@account);
SELECT CASE WHEN changes()=1 THEN last_insert_rowid() ELSE NULL END;";
            cmd.Parameters.AddWithValue("@import", (object?)etlImportId ?? DBNull.Value);
            var inserted = await cmd.ExecuteScalarAsync();
            if (inserted is null or DBNull) throw new InvalidOperationException("The shop or payment mode is not in the active company.");
            item.Id = Convert.ToInt32(inserted);
        }
        else{cmd.CommandText=@"UPDATE Bill_transcation SET Bill_date=@date,Shop_Id=@shop,Payment_Mode_Id=@mode,Bill_value=@value,Note=@note,bill_reference_no=@reference
WHERE Id=@id AND FinancialAccountId=@account
AND EXISTS(SELECT 1 FROM Shop WHERE Id=@shop AND FinancialAccountId=@account)
AND EXISTS(SELECT 1 FROM Payment_mode WHERE Id=@mode AND FinancialAccountId=@account)";cmd.Parameters.AddWithValue("@id",item.Id);if(await cmd.ExecuteNonQueryAsync()!=1) throw new InvalidOperationException("The bill, shop, or payment mode is not in the active company.");} return item.Id;
    }
    public async Task<int> DeleteItemAsync(BillTransaction item) { using var operation = _criticalOperations.Begin(); await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="DELETE FROM Bill_transcation WHERE Id=@id AND FinancialAccountId=@account"; cmd.Parameters.AddWithValue("@id",item.Id); cmd.Parameters.AddWithValue("@account",_accounts.RequireId()); return await cmd.ExecuteNonQueryAsync(); }
}
