using FinTracker.Models;
using FinTracker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace FinTracker.Data;

public class PaymentModeRepository
{
    private bool _initialized;
    public void InvalidateAfterDatabaseRestore() => _initialized = false;
    private readonly ILogger<PaymentModeRepository> _logger;
    private readonly ICriticalOperationState _criticalOperations;
    private readonly FinancialAccountContext _accounts;
    public PaymentModeRepository(ILogger<PaymentModeRepository> logger, ICriticalOperationState criticalOperations, FinancialAccountContext accounts) { _logger = logger; _criticalOperations = criticalOperations; _accounts = accounts; }
    private async Task Init()
    {
        if (_initialized) return;
        await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        try { var cmd = c.CreateCommand(); cmd.CommandText = "CREATE TABLE IF NOT EXISTS Payment_mode (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE, FinancialAccountId INTEGER NOT NULL, UNIQUE(FinancialAccountId,Name));"; await cmd.ExecuteNonQueryAsync(); _initialized = true; }
        catch (Exception ex) { _logger.LogError(ex, "Error creating Payment_mode table"); throw; }
    }
    public async Task<List<PaymentMode>> ListAsync() { await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="SELECT Id,Name,FinancialAccountId FROM Payment_mode WHERE FinancialAccountId=@account ORDER BY Name"; cmd.Parameters.AddWithValue("@account",_accounts.RequireId()); var result=new List<PaymentMode>(); await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) result.Add(new PaymentMode{Id=r.GetInt32(0),Name=r.GetString(1),FinancialAccountId=r.GetInt32(2)}); return result; }
    public async Task<PaymentMode?> GetAsync(int id) { await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="SELECT Id,Name,FinancialAccountId FROM Payment_mode WHERE Id=@id AND FinancialAccountId=@account"; cmd.Parameters.AddWithValue("@id",id); cmd.Parameters.AddWithValue("@account",_accounts.RequireId()); await using var r=await cmd.ExecuteReaderAsync(); return await r.ReadAsync()?new PaymentMode{Id=r.GetInt32(0),Name=r.GetString(1),FinancialAccountId=r.GetInt32(2)}:null; }
    public async Task<bool> ExistsNameAsync(string name,int exceptId=0) { await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="SELECT COUNT(*) FROM Payment_mode WHERE Name=@name AND Id<>@id AND FinancialAccountId=@account"; cmd.Parameters.AddWithValue("@name",name.Trim()); cmd.Parameters.AddWithValue("@id",exceptId); cmd.Parameters.AddWithValue("@account",_accounts.RequireId()); return Convert.ToInt32(await cmd.ExecuteScalarAsync())>0; }
    public async Task<int> EnsureEtlIncomeModeAsync()
    {
        using var operation = _criticalOperations.Begin();
        await Init();

        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();

        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT OR IGNORE INTO Payment_mode(Name,FinancialAccountId) VALUES (@name,@account);
SELECT Id FROM Payment_mode WHERE Name = @name COLLATE NOCASE AND FinancialAccountId=@account;";
        // The unique, case-insensitive Payment_mode.Name constraint makes
        // repeated ETL imports reuse this row instead of creating duplicates.
        command.Parameters.AddWithValue("@name", "ETL_INCOME");
        command.Parameters.AddWithValue("@account", _accounts.RequireId());

        var result = await command.ExecuteScalarAsync();
        if (result is null or DBNull)
            throw new InvalidOperationException("The ETL_INCOME payment mode could not be resolved.");

        var id = Convert.ToInt32(result);
        await transaction.CommitAsync();
        return id;
    }
    public async Task<int> SaveItemAsync(PaymentMode item) { using var operation = _criticalOperations.Begin(); await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); var account=_accounts.RequireId(); cmd.Parameters.AddWithValue("@account",account); cmd.Parameters.AddWithValue("@name",item.Name.Trim()); if(item.Id==0){cmd.CommandText="INSERT INTO Payment_mode(Name,FinancialAccountId) VALUES(@name,@account); SELECT last_insert_rowid();";item.Id=Convert.ToInt32(await cmd.ExecuteScalarAsync());}else{cmd.CommandText="UPDATE Payment_mode SET Name=@name WHERE Id=@id AND FinancialAccountId=@account";cmd.Parameters.AddWithValue("@id",item.Id);if(await cmd.ExecuteNonQueryAsync()!=1) throw new InvalidOperationException("The payment mode is not in the active company.");} item.FinancialAccountId=account; return item.Id; }
    public async Task<int> DeleteItemAsync(PaymentMode item) { using var operation = _criticalOperations.Begin(); await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="DELETE FROM Payment_mode WHERE Id=@id AND FinancialAccountId=@account"; cmd.Parameters.AddWithValue("@id",item.Id); cmd.Parameters.AddWithValue("@account",_accounts.RequireId()); return await cmd.ExecuteNonQueryAsync(); }
}
