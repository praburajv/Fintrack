using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MauiApp1.Data;

public class PaymentModeRepository
{
    private bool _initialized;
    private readonly ILogger<PaymentModeRepository> _logger;
    private readonly ICriticalOperationState _criticalOperations;
    public PaymentModeRepository(ILogger<PaymentModeRepository> logger, ICriticalOperationState criticalOperations) { _logger = logger; _criticalOperations = criticalOperations; }
    private async Task Init()
    {
        if (_initialized) return;
        await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        try { var cmd = c.CreateCommand(); cmd.CommandText = "CREATE TABLE IF NOT EXISTS Payment_mode (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name VARCHAR(225) NOT NULL COLLATE NOCASE UNIQUE);"; await cmd.ExecuteNonQueryAsync(); _initialized = true; }
        catch (Exception ex) { _logger.LogError(ex, "Error creating Payment_mode table"); throw; }
    }
    public async Task<List<PaymentMode>> ListAsync() { await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="SELECT Id,Name FROM Payment_mode ORDER BY Name"; var result=new List<PaymentMode>(); await using var r=await cmd.ExecuteReaderAsync(); while(await r.ReadAsync()) result.Add(new PaymentMode{Id=r.GetInt32(0),Name=r.GetString(1)}); return result; }
    public async Task<PaymentMode?> GetAsync(int id) { await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="SELECT Id,Name FROM Payment_mode WHERE Id=@id"; cmd.Parameters.AddWithValue("@id",id); await using var r=await cmd.ExecuteReaderAsync(); return await r.ReadAsync()?new PaymentMode{Id=r.GetInt32(0),Name=r.GetString(1)}:null; }
    public async Task<bool> ExistsNameAsync(string name,int exceptId=0) { await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="SELECT COUNT(*) FROM Payment_mode WHERE Name=@name AND Id<>@id"; cmd.Parameters.AddWithValue("@name",name.Trim()); cmd.Parameters.AddWithValue("@id",exceptId); return Convert.ToInt32(await cmd.ExecuteScalarAsync())>0; }
    public async Task<int> SaveItemAsync(PaymentMode item) { using var operation = _criticalOperations.Begin(); await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.Parameters.AddWithValue("@name",item.Name.Trim()); if(item.Id==0){cmd.CommandText="INSERT INTO Payment_mode(Name) VALUES(@name); SELECT last_insert_rowid();";item.Id=Convert.ToInt32(await cmd.ExecuteScalarAsync());}else{cmd.CommandText="UPDATE Payment_mode SET Name=@name WHERE Id=@id";cmd.Parameters.AddWithValue("@id",item.Id);await cmd.ExecuteNonQueryAsync();} return item.Id; }
    public async Task<int> DeleteItemAsync(PaymentMode item) { using var operation = _criticalOperations.Begin(); await Init(); await using var c=new SqliteConnection(Constants.DatabasePath); await c.OpenAsync(); var cmd=c.CreateCommand(); cmd.CommandText="DELETE FROM Payment_mode WHERE Id=@id"; cmd.Parameters.AddWithValue("@id",item.Id); return await cmd.ExecuteNonQueryAsync(); }
}
