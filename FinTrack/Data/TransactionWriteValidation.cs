using FinTracker.Utilities;
using Microsoft.Data.Sqlite;

namespace FinTracker.Data;

internal static class TransactionWriteValidation
{
    public static async Task ValidateNoteAsync(SqliteConnection connection, string table, int id, int account, string? note)
    {
        if (table is not ("Bill_transcation" or "income_transaction")) throw new ArgumentOutOfRangeException(nameof(table));
        string? original = null;
        if (id > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT Note FROM {table} WHERE Id=@id AND FinancialAccountId=@account";
            command.Parameters.AddWithValue("@id", id);
            command.Parameters.AddWithValue("@account", account);
            var stored = await command.ExecuteScalarAsync();
            original = stored is null or DBNull ? null : Convert.ToString(stored);
        }
        var error = TransactionInput.ValidateManualNote(note, original);
        if (error is not null) throw new ArgumentException(error, nameof(note));
    }
}
