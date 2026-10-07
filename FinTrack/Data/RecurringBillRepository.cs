using FinTracker.Models;
using FinTracker.Services;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace FinTracker.Data;

public sealed class RecurringBillRepository
{
    private bool _initialized;
    private readonly SemaphoreSlim _initialization = new(1, 1);
    private readonly ICriticalOperationState _criticalOperations;
    public RecurringBillRepository(ICriticalOperationState criticalOperations) => _criticalOperations = criticalOperations;

    private async Task InitAsync()
    {
        if (_initialized) return;
        await _initialization.WaitAsync();
        try
        {
            if (_initialized) return;
            await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS Recurring_Bill (
 Id INTEGER PRIMARY KEY AUTOINCREMENT, Shop_Id INTEGER NOT NULL, Payment_Mode_Id INTEGER NOT NULL,
 Amount NUMERIC NOT NULL, Description TEXT, Start_Month TEXT NOT NULL, End_Month TEXT NULL,
 Is_Active INTEGER NOT NULL DEFAULT 1, Created_At TEXT NOT NULL, Updated_At TEXT NOT NULL,
 FOREIGN KEY(Shop_Id) REFERENCES Shop(Id), FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));
CREATE INDEX IF NOT EXISTS IX_Recurring_Bill_Active ON Recurring_Bill(Is_Active, Start_Month, End_Month);";
            await command.ExecuteNonQueryAsync();
            await BillTransactionRepository.EnsureRecurringColumnsAsync(connection);
            _initialized = true;
        }
        finally { _initialization.Release(); }
    }

    public async Task<List<RecurringBill>> ListAsync()
    {
        await InitAsync(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "SELECT r.Id,r.Shop_Id,r.Payment_Mode_Id,r.Amount,r.Description,r.Start_Month,r.End_Month,r.Is_Active,r.Created_At,r.Updated_At,s.Name,p.Name FROM Recurring_Bill r JOIN Shop s ON s.Id=r.Shop_Id JOIN Payment_mode p ON p.Id=r.Payment_Mode_Id ORDER BY s.Name";
        var result = new List<RecurringBill>(); await using var reader = await cmd.ExecuteReaderAsync(); while (await reader.ReadAsync()) result.Add(Read(reader)); return result;
    }

    public async Task<RecurringBill?> GetAsync(int id)
    {
        return (await ListAsync()).FirstOrDefault(x => x.Id == id);
    }

    public async Task SaveAsync(RecurringBill bill)
    {
        using var operation = _criticalOperations.Begin(); await InitAsync();
        await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
        var cmd = c.CreateCommand(); cmd.Parameters.AddWithValue("@shop", bill.ShopId); cmd.Parameters.AddWithValue("@mode", bill.PaymentModeId); cmd.Parameters.AddWithValue("@amount", bill.Amount); cmd.Parameters.AddWithValue("@description", bill.Description.Trim()); cmd.Parameters.AddWithValue("@start", MonthValue(bill.StartMonth)); cmd.Parameters.AddWithValue("@end", bill.EndMonth is null ? DBNull.Value : MonthValue(bill.EndMonth.Value)); cmd.Parameters.AddWithValue("@active", bill.IsActive ? 1 : 0); cmd.Parameters.AddWithValue("@updated", now);
        if (bill.Id == 0) { cmd.CommandText = "INSERT INTO Recurring_Bill(Shop_Id,Payment_Mode_Id,Amount,Description,Start_Month,End_Month,Is_Active,Created_At,Updated_At) VALUES(@shop,@mode,@amount,@description,@start,@end,@active,@updated,@updated); SELECT last_insert_rowid();"; bill.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync()); }
        else { cmd.CommandText = "UPDATE Recurring_Bill SET Shop_Id=@shop,Payment_Mode_Id=@mode,Amount=@amount,Description=@description,Start_Month=@start,End_Month=@end,Is_Active=@active,Updated_At=@updated WHERE Id=@id"; cmd.Parameters.AddWithValue("@id", bill.Id); await cmd.ExecuteNonQueryAsync(); }
    }

    public async Task DeleteAsync(int id)
    {
        using var operation = _criticalOperations.Begin(); await InitAsync(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var check = c.CreateCommand(); check.CommandText = "SELECT COUNT(*) FROM Bill_transcation WHERE Recurring_Bill_Id=@id"; check.Parameters.AddWithValue("@id", id); if (Convert.ToInt32(await check.ExecuteScalarAsync()) > 0) throw new InvalidOperationException("This recurring bill has historical transactions. Disable it instead of deleting it.");
        var cmd = c.CreateCommand(); cmd.CommandText = "DELETE FROM Recurring_Bill WHERE Id=@id"; cmd.Parameters.AddWithValue("@id", id); await cmd.ExecuteNonQueryAsync();
    }

    public async Task ToggleAsync(RecurringBill bill) { bill.IsActive = !bill.IsActive; await SaveAsync(bill); }
    internal static string MonthValue(DateTime month) => new DateTime(month.Year, month.Month, 1).ToString("yyyy-MM", CultureInfo.InvariantCulture);
    private static RecurringBill Read(SqliteDataReader r) => new() { Id = r.GetInt32(0), ShopId = r.GetInt32(1), PaymentModeId = r.GetInt32(2), Amount = Convert.ToDecimal(r.GetValue(3), CultureInfo.InvariantCulture), Description = r.IsDBNull(4) ? string.Empty : r.GetString(4), StartMonth = DateTime.ParseExact(r.GetString(5), "yyyy-MM", CultureInfo.InvariantCulture), EndMonth = r.IsDBNull(6) ? null : DateTime.ParseExact(r.GetString(6), "yyyy-MM", CultureInfo.InvariantCulture), IsActive = r.GetInt32(7) != 0, CreatedAt = DateTime.Parse(r.GetString(8), CultureInfo.InvariantCulture), UpdatedAt = DateTime.Parse(r.GetString(9), CultureInfo.InvariantCulture), ShopName = r.GetString(10), PaymentModeName = r.GetString(11) };
}
