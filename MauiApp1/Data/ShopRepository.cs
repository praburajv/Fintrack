using MauiApp1.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MauiApp1.Data;

public class ShopRepository
{
    private bool _initialized;
    private readonly ILogger<ShopRepository> _logger;
    public ShopRepository(ILogger<ShopRepository> logger) => _logger = logger;

    private async Task Init()
    {
        if (_initialized) return;
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        try
        {
            var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS Shop (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name VARCHAR(225) NOT NULL COLLATE NOCASE UNIQUE);";
            await command.ExecuteNonQueryAsync();
            _initialized = true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating Shop table"); throw; }
    }

    public async Task<List<Shop>> ListAsync()
    {
        await Init();
        await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "SELECT Id, Name FROM Shop ORDER BY Name";
        var result = new List<Shop>(); await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(new Shop { Id = r.GetInt32(0), Name = r.GetString(1) });
        return result;
    }

    public async Task<Shop?> GetAsync(int id)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "SELECT Id, Name FROM Shop WHERE Id=@id"; cmd.Parameters.AddWithValue("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? new Shop { Id = r.GetInt32(0), Name = r.GetString(1) } : null;
    }

    public async Task<bool> ExistsNameAsync(string name, int exceptId = 0)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM Shop WHERE Name=@name AND Id<>@id";
        cmd.Parameters.AddWithValue("@name", name.Trim()); cmd.Parameters.AddWithValue("@id", exceptId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> SaveItemAsync(Shop item)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.Parameters.AddWithValue("@name", item.Name.Trim());
        if (item.Id == 0) { cmd.CommandText = "INSERT INTO Shop(Name) VALUES(@name); SELECT last_insert_rowid();"; item.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync()); }
        else { cmd.CommandText = "UPDATE Shop SET Name=@name WHERE Id=@id"; cmd.Parameters.AddWithValue("@id", item.Id); await cmd.ExecuteNonQueryAsync(); }
        return item.Id;
    }

    public async Task<int> DeleteItemAsync(Shop item)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "DELETE FROM Shop WHERE Id=@id"; cmd.Parameters.AddWithValue("@id", item.Id); return await cmd.ExecuteNonQueryAsync();
    }
}
