using MauiApp1.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace MauiApp1.Data;

public class BillTransactionRepository
{
    private bool _initialized;
    private readonly ILogger<BillTransactionRepository> _logger;
    public BillTransactionRepository(ILogger<BillTransactionRepository> logger) => _logger = logger;

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

    public async Task<List<ShopExpenseChartData>> GetExpensesByShopAsync(int year)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT s.Name, SUM(CAST(b.Bill_value AS NUMERIC)) FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id WHERE b.Bill_date >= @from AND b.Bill_date < @to GROUP BY b.Shop_Id, s.Name ORDER BY s.Name";
        cmd.Parameters.AddWithValue("@from", new DateTime(year, 1, 1).ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", new DateTime(year + 1, 1, 1).ToString("O", CultureInfo.InvariantCulture));
        var result = new List<ShopExpenseChartData>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) result.Add(new ShopExpenseChartData { ShopName = r.GetString(0), TotalValue = ParseDecimal(r.GetValue(1)) }); return result;
    }

    public async Task<List<MonthlyExpenseChartData>> GetExpensesByMonthAsync(int year)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = @"SELECT CAST(strftime('%m', Bill_date) AS INTEGER), SUM(CAST(Bill_value AS NUMERIC)) FROM Bill_transcation WHERE Bill_date >= @from AND Bill_date < @to GROUP BY strftime('%m', Bill_date) ORDER BY strftime('%m', Bill_date)";
        cmd.Parameters.AddWithValue("@from", new DateTime(year, 1, 1).ToString("O", CultureInfo.InvariantCulture)); cmd.Parameters.AddWithValue("@to", new DateTime(year + 1, 1, 1).ToString("O", CultureInfo.InvariantCulture));
        var totals = new Dictionary<int, decimal>(); await using var r = await cmd.ExecuteReaderAsync(); while (await r.ReadAsync()) totals[r.GetInt32(0)] = ParseDecimal(r.GetValue(1));
        return Enumerable.Range(1, 12).Select(month => new MonthlyExpenseChartData { MonthNumber = month, MonthName = CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(month), TotalValue = totals.GetValueOrDefault(month) }).ToList();
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
        await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); AddParams(cmd,item);
        if(item.Id==0){cmd.CommandText="INSERT INTO Bill_transcation(Bill_date,Shop_Id,Payment_Mode_Id,Bill_value,Note) VALUES(@date,@shop,@mode,@value,@note); SELECT last_insert_rowid();"; item.Id=Convert.ToInt32(await cmd.ExecuteScalarAsync());}
        else{cmd.CommandText="UPDATE Bill_transcation SET Bill_date=@date,Shop_Id=@shop,Payment_Mode_Id=@mode,Bill_value=@value,Note=@note WHERE Id=@id";cmd.Parameters.AddWithValue("@id",item.Id);await cmd.ExecuteNonQueryAsync();} return item.Id;
    }
    public async Task<int> DeleteItemAsync(BillTransaction item) { await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="DELETE FROM Bill_transcation WHERE Id=@id"; cmd.Parameters.AddWithValue("@id",item.Id); return await cmd.ExecuteNonQueryAsync(); }
}
