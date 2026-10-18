using FinTracker.Data;
using FinTracker.Models;
using Microsoft.Data.Sqlite;

namespace FinTracker.Services;

public sealed class SqlSchemaMetadataService : ISqlSchemaMetadataService
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<SqlTableMetadata>? _tables;
    private readonly Dictionary<string, IReadOnlyList<SqlColumnMetadata>> _columns = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IReadOnlyList<SqlForeignKeyMetadata>> _foreignKeys = new(StringComparer.OrdinalIgnoreCase);

    // Structure shortcuts include every actual table, including SQLite-maintained tables.
    // Do not cache this list: restore and newly created tables must be reflected immediately.
    public async Task<IReadOnlyList<string>> GetTableNamesAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name COLLATE NOCASE;";
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(reader.GetString(0));
        return result;
    }

    public async Task<IReadOnlyList<SqlTableMetadata>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        if (_tables is not null) return _tables;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_tables is not null) return _tables;
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT name, type FROM sqlite_master WHERE type IN ('table','view') AND name NOT LIKE 'sqlite_%' ORDER BY name;";
            var result = new List<SqlTableMetadata>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
                result.Add(new SqlTableMetadata(reader.GetString(0), reader.GetString(1).Equals("view", StringComparison.OrdinalIgnoreCase)));
            return _tables = result;
        }
        finally { _gate.Release(); }
    }

    public async Task<IReadOnlyList<SqlColumnMetadata>> GetColumnsAsync(string tableName, CancellationToken cancellationToken = default)
    {
        if (_columns.TryGetValue(tableName, out var cached)) return cached;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteIdentifier(tableName)});";
        var result = new List<SqlColumnMetadata>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var name = reader.GetString(1);
            var dataType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var notNull = reader.GetInt32(3) != 0;
            var primaryKey = reader.GetInt32(5) != 0;
            result.Add(new SqlColumnMetadata(tableName, name, dataType, !notNull, primaryKey));
        }
        _columns[tableName] = result;
        return result;
    }

    public async Task<IReadOnlyList<SqlForeignKeyMetadata>> GetForeignKeysAsync(string tableName, CancellationToken cancellationToken = default)
    {
        if (_foreignKeys.TryGetValue(tableName, out var cached)) return cached;
        await using var connection = await OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA foreign_key_list({QuoteIdentifier(tableName)});";
        var result = new List<SqlForeignKeyMetadata>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var referencedTable = reader.GetString(2);
            var from = reader.GetString(3);
            var to = reader.IsDBNull(4) ? "" : reader.GetString(4);
            if (!string.IsNullOrWhiteSpace(to)) result.Add(new SqlForeignKeyMetadata(tableName, from, referencedTable, to));
        }
        _foreignKeys[tableName] = result;
        return result;
    }

    public void Invalidate()
    {
        _tables = null;
        _columns.Clear();
        _foreignKeys.Clear();
    }

    private static async Task<SqliteConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.Combine(FileSystem.AppDataDirectory, Constants.DatabaseFilename),
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Shared
        }.ToString());
        try { await connection.OpenAsync(cancellationToken); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }

    private static string QuoteIdentifier(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
