using FinTracker.Utilities;
using Microsoft.Data.Sqlite;

namespace FinTracker.Data;

public static class BillReferenceSchema
{
    private static readonly string[] Tables = ["Bill_transcation", "income_transaction"];
    private static readonly SemaphoreSlim Gate = new(1, 1);
    // Keep '-' last in the GLOB character class so it is a literal, not a range.
    private const string InvalidCharacters = "*[^A-Za-z0-9!@#&_-]*";

    public static async Task EnsureAsync(SqliteConnection connection, CancellationToken token = default)
    {
        await Gate.WaitAsync(token);
        try { await EnsureCoreAsync(connection, token); }
        finally { Gate.Release(); }
    }

    private static async Task EnsureCoreAsync(SqliteConnection connection, CancellationToken token)
    {
        var foreignKeys = Convert.ToInt32(await ScalarAsync(connection, "PRAGMA foreign_keys", null, token));
        var legacyAlter = Convert.ToInt32(await ScalarAsync(connection, "PRAGMA legacy_alter_table", null, token));
        try
        {
            await ExecuteAsync(connection, "PRAGMA foreign_keys=OFF; PRAGMA legacy_alter_table=ON", null, token);
            await using var transaction = connection.BeginTransaction();
            var rebuilt = false;
            foreach (var table in Tables)
            {
                var columns = await ColumnsAsync(connection, table, token, transaction);
                if (columns.Count == 0) continue;
                if (!columns.Contains("bill_reference_no"))
                {
                    await ExecuteAsync(connection, $"ALTER TABLE {table} ADD COLUMN bill_reference_no CHAR(15) NULL CHECK(bill_reference_no IS NULL OR (length(bill_reference_no) BETWEEN 1 AND 15 AND bill_reference_no NOT GLOB '{InvalidCharacters}' AND instr(bill_reference_no,char(0))=0))", transaction, token);
                }
                else rebuilt |= await UpgradeReferenceConstraintAsync(connection, transaction, table, token);
                await using var command = connection.CreateCommand();
                command.Transaction = transaction;
                // Triggers also protect databases that already had an unconstrained reference column.
                command.CommandText = $@"
    DROP TRIGGER IF EXISTS TR_{table}_ReferenceInsert;
    DROP TRIGGER IF EXISTS TR_{table}_ReferenceUpdate;
    CREATE TRIGGER TR_{table}_ReferenceInsert BEFORE INSERT ON {table}
    WHEN NEW.bill_reference_no IS NOT NULL AND (length(NEW.bill_reference_no) NOT BETWEEN 1 AND 15 OR NEW.bill_reference_no GLOB '{InvalidCharacters}' OR instr(NEW.bill_reference_no,char(0))>0)
    BEGIN SELECT RAISE(ABORT,'Invalid Reference No.'); END;
    CREATE TRIGGER TR_{table}_ReferenceUpdate BEFORE UPDATE OF bill_reference_no ON {table}
    WHEN NEW.bill_reference_no IS NOT NULL AND (length(NEW.bill_reference_no) NOT BETWEEN 1 AND 15 OR NEW.bill_reference_no GLOB '{InvalidCharacters}' OR instr(NEW.bill_reference_no,char(0))>0)
    BEGIN SELECT RAISE(ABORT,'Invalid Reference No.'); END;";
                if (columns.Contains("FinancialAccountId"))
                    command.CommandText += $"CREATE INDEX IF NOT EXISTS IX_{table}_AccountReference ON {table}(FinancialAccountId,bill_reference_no COLLATE NOCASE);";
                await command.ExecuteNonQueryAsync(token);
            }
            if (rebuilt)
            {
                await using var check = connection.CreateCommand();
                check.Transaction = transaction;
                check.CommandText = "PRAGMA foreign_key_check";
                await using var reader = await check.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token)) throw new InvalidDataException("Reference migration found invalid foreign keys. No schema changes were committed.");
            }
            await transaction.CommitAsync(token);
        }
        finally
        {
            // Cancellation must not leave the caller's connection with foreign keys disabled.
            await ExecuteAsync(connection, $"PRAGMA foreign_keys={foreignKeys}; PRAGMA legacy_alter_table={legacyAlter}", null, CancellationToken.None);
        }
    }

    private static async Task<bool> UpgradeReferenceConstraintAsync(SqliteConnection connection, SqliteTransaction transaction, string table, CancellationToken token)
    {
        // Only rebuild the constraint installed by the earlier reference migration.
        // Copy the complete original definition so unrelated columns/checks/defaults survive.
        var schema = (string)(await ScalarAsync(connection, $"SELECT sql FROM sqlite_master WHERE type='table' AND name='{table}'", transaction, token))!;
        const string oldRule = "bill_reference_no NOT GLOB '*[^A-Za-z0-9]*'";
        if (!schema.Contains(oldRule, StringComparison.Ordinal)) return false;
        var revised = schema.Replace(oldRule, $"bill_reference_no NOT GLOB '{InvalidCharacters}'", StringComparison.Ordinal);
        var replacement = table + "_ReferenceV2";
        var objects = new List<string>();
        await using (var query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText = "SELECT sql FROM sqlite_master WHERE tbl_name=@table AND type IN ('index','trigger') AND sql IS NOT NULL ORDER BY type,name";
            query.Parameters.AddWithValue("@table", table);
            await using var reader = await query.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) objects.Add(reader.GetString(0));
        }
        long? sequence = null;
        if (Convert.ToInt64(await ScalarAsync(connection, "SELECT count(*) FROM sqlite_master WHERE name='sqlite_sequence'", transaction, token)) > 0)
        {
            var value = await ScalarAsync(connection, $"SELECT seq FROM sqlite_sequence WHERE name='{table}'", transaction, token);
            if (value is not null and not DBNull) sequence = Convert.ToInt64(value);
        }
        var columns = await ColumnsAsync(connection, table, token, transaction);
        var columnList = string.Join(",", columns.Select(name => '"' + name.Replace("\"", "\"\"") + '"'));
        await ExecuteAsync(connection, $"CREATE TABLE \"{replacement}\" " + revised[revised.IndexOf('(')..], transaction, token);
        await ExecuteAsync(connection, $"INSERT INTO \"{replacement}\" ({columnList}) SELECT {columnList} FROM \"{table}\"; DROP TABLE \"{table}\"; ALTER TABLE \"{replacement}\" RENAME TO \"{table}\";", transaction, token);
        foreach (var sql in objects) await ExecuteAsync(connection, sql, transaction, token);
        if (sequence.HasValue)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "UPDATE sqlite_sequence SET seq=MAX(seq,@sequence) WHERE name=@table";
            command.Parameters.AddWithValue("@sequence", sequence.Value);
            command.Parameters.AddWithValue("@table", table);
            await command.ExecuteNonQueryAsync(token);
        }
        return true;
    }

    private static async Task<object?> ScalarAsync(SqliteConnection connection, string sql, SqliteTransaction? transaction, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction; command.CommandText = sql;
        return await command.ExecuteScalarAsync(token);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql, SqliteTransaction? transaction, CancellationToken token)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction; command.CommandText = sql;
        await command.ExecuteNonQueryAsync(token);
    }

    public static async Task ValidateAsync(SqliteConnection connection, CancellationToken token = default)
    {
        foreach (var table in Tables)
        {
            if (!(await ColumnsAsync(connection, table, token)).Contains("bill_reference_no")) continue;
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT bill_reference_no FROM {table} WHERE bill_reference_no IS NOT NULL";
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
                if (!TransactionInput.IsReference(reader.GetString(0)))
                    throw new InvalidDataException($"The backup contains an invalid Reference No. in {table}.");
        }
    }

    private static async Task<HashSet<string>> ColumnsAsync(SqliteConnection connection, string table, CancellationToken token, SqliteTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"PRAGMA table_info({table})";
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) columns.Add(reader.GetString(1));
        return columns;
    }
}
