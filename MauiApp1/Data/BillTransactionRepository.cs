using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace MauiApp1.Data;

public class BillTransactionRepository
{
    private bool _initialized;
    private readonly ILogger<BillTransactionRepository> _logger;
    private readonly ICriticalOperationState _criticalOperations;
    public BillTransactionRepository(ILogger<BillTransactionRepository> logger, ICriticalOperationState criticalOperations)
    {
        _logger = logger;
        _criticalOperations = criticalOperations;
    }

    private async Task Init()
    {
        if (_initialized) return;
        await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        try
        {
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
 FOREIGN KEY(Shop_Id) REFERENCES Shop(Id),
 FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));
CREATE INDEX IF NOT EXISTS IX_Bill_transcation_Bill_date ON Bill_transcation(Bill_date);
CREATE INDEX IF NOT EXISTS IX_Bill_transcation_Shop_Id ON Bill_transcation(Shop_Id);
CREATE INDEX IF NOT EXISTS IX_Bill_transcation_Payment_Mode_Id ON Bill_transcation(Payment_Mode_Id);";
            await cmd.ExecuteNonQueryAsync(); _initialized = true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating Bill_transcation table"); throw; }
    }

    private static BillTransaction Read(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0), BillDate = DateTime.Parse(r.GetString(1), CultureInfo.InvariantCulture), ShopId = r.GetInt32(2),
        PaymentModeId = r.GetInt32(3), BillValue = decimal.Parse(r.GetString(4), CultureInfo.InvariantCulture), Note = r.IsDBNull(5) ? string.Empty : r.GetString(5),
        ShopName = r.IsDBNull(6) ? string.Empty : r.GetString(6), PaymentModeName = r.IsDBNull(7) ? string.Empty : r.GetString(7)
    };

    private static void AddParams(SqliteCommand cmd, BillTransaction item)
    {
        cmd.Parameters.AddWithValue("@date", item.BillDate.ToString("O", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@shop", item.ShopId); cmd.Parameters.AddWithValue("@mode", item.PaymentModeId);
        cmd.Parameters.AddWithValue("@value", item.BillValue.ToString(CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@note", string.IsNullOrWhiteSpace(item.Note) ? DBNull.Value : item.Note.Trim());
    }

    public async Task<List<BillTransaction>> ListAsync(int? year = null)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id";
        if (year.HasValue) { cmd.CommandText += " WHERE b.Bill_date >= @from AND b.Bill_date < @to"; cmd.Parameters.AddWithValue("@from", new DateTime(year.Value,1,1).ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", new DateTime(year.Value+1,1,1).ToString("O", CultureInfo.InvariantCulture)); }
        cmd.CommandText += " ORDER BY b.Bill_date,b.Id";
        var result = new List<BillTransaction>(); await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) result.Add(Read(r)); return result;
    }

    public async Task<decimal> GetAnnualTotalAsync(int year)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COALESCE(SUM(CAST(Bill_value AS NUMERIC)), 0) FROM Bill_transcation WHERE Bill_date >= @from AND Bill_date < @to";
        cmd.Parameters.AddWithValue("@from", new DateTime(year, 1, 1).ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", new DateTime(year + 1, 1, 1).ToString("O", CultureInfo.InvariantCulture));
        return ParseDecimal(await cmd.ExecuteScalarAsync());
    }

    public Task<List<ShopExpenseChartData>> GetExpensesByShopAsync(int year) => GetExpensesByShopAsync(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1));

    public Task<List<ShopExpenseChartData>> GetExpensesByShopForMonthAsync(int year, int month)
        => GetExpensesByShopAsync(new DateTime(year, month, 1), new DateTime(year, month, 1).AddMonths(1));

    private async Task<List<ShopExpenseChartData>> GetExpensesByShopAsync(DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT s.Id, s.Name, SUM(CAST(b.Bill_value AS NUMERIC)) FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id WHERE b.Bill_date >= @from AND b.Bill_date < @to GROUP BY b.Shop_Id, s.Name ORDER BY s.Name";
        cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<ShopExpenseChartData>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) result.Add(new ShopExpenseChartData { ShopId = r.GetInt32(0), ShopName = r.GetString(1), TotalValue = ParseDecimal(r.GetValue(2)) }); return result;
    }

    public async Task<List<WeeklyExpenseChartData>> GetExpensesByWeekAsync(DateTime weekStart)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT CAST(strftime('%w', Bill_date) AS INTEGER), SUM(CAST(Bill_value AS NUMERIC)) FROM Bill_transcation WHERE Bill_date >= @from AND Bill_date < @to GROUP BY strftime('%w', Bill_date)";
        cmd.Parameters.AddWithValue("@from", weekStart.ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", weekStart.AddDays(7).ToString("O", CultureInfo.InvariantCulture));
        var totals = new Dictionary<int, decimal>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) totals[r.GetInt32(0)] = ParseDecimal(r.GetValue(1));
        return Enumerable.Range(0, 7).Select(day => { var date = weekStart.AddDays(day); var sqliteDay = (int)date.DayOfWeek; return new WeeklyExpenseChartData { DayNumber = day + 1, DayName = date.ToString("ddd", CultureInfo.InvariantCulture), Date = date, TotalValue = totals.GetValueOrDefault(sqliteDay) }; }).ToList();
    }

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
WHERE b.Bill_date >= @from AND b.Bill_date < @to
GROUP BY p.Id, p.Name ORDER BY p.Name";
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
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id
WHERE b.Payment_Mode_Id=@mode AND b.Bill_date >= @from AND b.Bill_date < @to ORDER BY b.Bill_date DESC,b.Id DESC";
        cmd.Parameters.AddWithValue("@mode", paymentModeId); cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<BillTransaction>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) result.Add(Read(r)); return result;
    }

    public async Task<Dictionary<int, decimal>> GetDailyExpenseTotalsAsync(int year, int month)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT Bill_date, Bill_value
FROM Bill_transcation
WHERE Bill_date >= @from AND Bill_date < @to
";
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

    private async Task<List<BillTransaction>> GetBillsForDateRangeAsync(DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id
WHERE b.Bill_date >= @from AND b.Bill_date < @to ORDER BY b.Bill_date DESC,b.Id DESC";
        cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<BillTransaction>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) result.Add(Read(r)); return result;
    }

    private async Task<List<BillTransaction>> GetBillsByShopAsync(int shopId, DateTime from, DateTime to)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id
WHERE b.Shop_Id=@shop AND b.Bill_date >= @from AND b.Bill_date < @to ORDER BY b.Bill_date DESC,b.Id DESC";
        cmd.Parameters.AddWithValue("@shop", shopId); cmd.Parameters.AddWithValue("@from", from.ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", to.ToString("O", CultureInfo.InvariantCulture));
        var result = new List<BillTransaction>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) result.Add(Read(r)); return result;
    }

    public async Task<List<MonthlyExpenseChartData>> GetExpensesByMonthAsync(int year)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT CAST(strftime('%m', Bill_date) AS INTEGER), SUM(CAST(Bill_value AS NUMERIC)) FROM Bill_transcation WHERE Bill_date >= @from AND Bill_date < @to GROUP BY strftime('%m', Bill_date) ORDER BY strftime('%m', Bill_date)";
        cmd.Parameters.AddWithValue("@from", new DateTime(year, 1, 1).ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", new DateTime(year + 1, 1, 1).ToString("O", CultureInfo.InvariantCulture));
        var totals = new Dictionary<int, decimal>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) totals[r.GetInt32(0)] = ParseDecimal(r.GetValue(1));
        return Enumerable.Range(1, 12).Select(month => new MonthlyExpenseChartData { MonthNumber = month, MonthName = new DateTime(2000, month, 1).ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant(), TotalValue = totals.GetValueOrDefault(month) }).ToList();
    }

    private static decimal ParseDecimal(object? value) => value is null || value == DBNull.Value ? 0m : decimal.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture);

    public async Task<BillTransaction?> GetAsync(int id)
    {
        await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText=@"SELECT b.Id,b.Bill_date,b.Shop_Id,b.Payment_Mode_Id,b.Bill_value,b.Note,s.Name,p.Name FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id WHERE b.Id=@id"; cmd.Parameters.AddWithValue("@id",id); await using var r=await cmd.ExecuteReaderAsync(); return await r.ReadAsync()?Read(r):null;
    }

    public async Task<bool> IsShopReferencedAsync(int id) => await IsReferencedAsync("Shop_Id", id);
    public async Task<bool> IsPaymentModeReferencedAsync(int id) => await IsReferencedAsync("Payment_Mode_Id", id);
    private async Task<bool> IsReferencedAsync(string column,int id) { await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText=$"SELECT COUNT(*) FROM Bill_transcation WHERE {column}=@id"; cmd.Parameters.AddWithValue("@id",id); return Convert.ToInt32(await cmd.ExecuteScalarAsync())>0; }

    public async Task<int> SaveItemAsync(BillTransaction item)
    {
        using var operation = _criticalOperations.Begin();
        await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); AddParams(cmd,item);
        if(item.Id==0){cmd.CommandText="INSERT INTO Bill_transcation(Bill_date,Shop_Id,Payment_Mode_Id,Bill_value,Note) VALUES(@date,@shop,@mode,@value,@note); SELECT last_insert_rowid();"; item.Id=Convert.ToInt32(await cmd.ExecuteScalarAsync());}
        else{cmd.CommandText="UPDATE Bill_transcation SET Bill_date=@date,Shop_Id=@shop,Payment_Mode_Id=@mode,Bill_value=@value,Note=@note WHERE Id=@id";cmd.Parameters.AddWithValue("@id",item.Id);await cmd.ExecuteNonQueryAsync();} return item.Id;
    }
    public async Task<int> DeleteItemAsync(BillTransaction item) { using var operation = _criticalOperations.Begin(); await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="DELETE FROM Bill_transcation WHERE Id=@id"; cmd.Parameters.AddWithValue("@id",item.Id); return await cmd.ExecuteNonQueryAsync(); }
}
