using MauiApp1.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MauiApp1.Data;

public sealed class EtlTextMapRepository
{
    private bool _initialized;
    private readonly SemaphoreSlim _initialization = new(1, 1);
    private readonly ILogger<EtlTextMapRepository> _logger;
    private readonly ICriticalOperationState _criticalOperations;

    public EtlTextMapRepository(ILogger<EtlTextMapRepository> logger, ICriticalOperationState criticalOperations)
    {
        _logger = logger;
        _criticalOperations = criticalOperations;
    }

    private async Task Init()
    {
        if (_initialized) return;
        await _initialization.WaitAsync();
        try
        {
            if (_initialized) return;
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON; CREATE TABLE IF NOT EXISTS ETL_TEXT_MAP (ID INTEGER PRIMARY KEY AUTOINCREMENT, LOOKUPTEXT VARCHAR(225) NOT NULL, SHOP_ID INTEGER NOT NULL, FOREIGN KEY(SHOP_ID) REFERENCES Shop(Id)); CREATE INDEX IF NOT EXISTS IX_ETL_TEXT_MAP_SHOP_ID ON ETL_TEXT_MAP(SHOP_ID);";
            await command.ExecuteNonQueryAsync();
            _initialized = true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating ETL_TEXT_MAP table");
            throw;
        }
        finally { _initialization.Release(); }
    }

    public async Task<List<EtlTextMap>> ListAsync()
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT m.ID, m.LOOKUPTEXT, m.SHOP_ID, s.Name FROM ETL_TEXT_MAP m LEFT JOIN Shop s ON s.Id=m.SHOP_ID ORDER BY m.ID";
        var result = new List<EtlTextMap>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(Read(reader));
        return result;
    }

    public async Task<EtlTextMap?> GetAsync(int id)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT m.ID, m.LOOKUPTEXT, m.SHOP_ID, s.Name FROM ETL_TEXT_MAP m LEFT JOIN Shop s ON s.Id=m.SHOP_ID WHERE m.ID=@id";
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }

    public async Task<bool> ExistsLookupTextAsync(string lookupText, int exceptId = 0)
    {
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM ETL_TEXT_MAP WHERE TRIM(LOOKUPTEXT) = TRIM(@text) COLLATE NOCASE AND ID <> @id";
        command.Parameters.AddWithValue("@text", lookupText.Trim());
        command.Parameters.AddWithValue("@id", exceptId);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> SaveItemAsync(EtlTextMap item)
    {
        var lookupText = item.LookupText.Trim();
        if (string.IsNullOrWhiteSpace(lookupText)) throw new ArgumentException("Lookup text is required.", nameof(item));
        if (lookupText.Length > 225) throw new ArgumentException("Lookup text cannot exceed 225 characters.", nameof(item));
        if (item.ShopId <= 0) throw new ArgumentException("A valid Shop is required.", nameof(item));
        using var operation = _criticalOperations.Begin();
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.Parameters.AddWithValue("@text", lookupText);
        command.Parameters.AddWithValue("@shop", item.ShopId);
        if (item.Id == 0)
        {
            command.CommandText = "INSERT INTO ETL_TEXT_MAP(LOOKUPTEXT, SHOP_ID) VALUES(@text, @shop); SELECT last_insert_rowid();";
            item.Id = Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        else
        {
            command.CommandText = "UPDATE ETL_TEXT_MAP SET LOOKUPTEXT=@text, SHOP_ID=@shop WHERE ID=@id";
            command.Parameters.AddWithValue("@id", item.Id);
            await command.ExecuteNonQueryAsync();
        }
        return item.Id;
    }

    public async Task<int> DeleteItemAsync(EtlTextMap item)
    {
        using var operation = _criticalOperations.Begin();
        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM ETL_TEXT_MAP WHERE ID=@id";
        command.Parameters.AddWithValue("@id", item.Id);
        return await command.ExecuteNonQueryAsync();
    }

    private static EtlTextMap Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        LookupText = reader.GetString(1),
        ShopId = reader.GetInt32(2),
        ShopName = reader.IsDBNull(3) ? "Shop unavailable" : reader.GetString(3)
    };
}
