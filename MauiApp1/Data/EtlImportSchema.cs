using Microsoft.Data.Sqlite;

namespace MauiApp1.Data;

/// <summary>Nullable provenance is additive: old/manual rows are never classified as ETL automatically.</summary>
public static class EtlImportSchema
{
    private static readonly SemaphoreSlim SchemaGate = new(1, 1);
    public static async Task EnsureAsync(SqliteConnection connection)
    {
        await SchemaGate.WaitAsync();
        try
        {
            foreach (var table in new[] { "Bill_transcation", "income_transaction" })
            {
                var exists = false;
                var hasColumn = false;
                await using (var info = connection.CreateCommand())
                {
                    info.CommandText = $"PRAGMA table_info({table})";
                    await using var reader = await info.ExecuteReaderAsync();
                    while (await reader.ReadAsync())
                    {
                        exists = true;
                        hasColumn |= reader.GetString(1).Equals("EtlImportId", StringComparison.OrdinalIgnoreCase);
                    }
                }
                if (!exists || hasColumn) continue;
                await using var alter = connection.CreateCommand();
                alter.CommandText = $"ALTER TABLE {table} ADD COLUMN EtlImportId TEXT NULL";
                await alter.ExecuteNonQueryAsync();
            }
        }
        finally { SchemaGate.Release(); }
    }
}
