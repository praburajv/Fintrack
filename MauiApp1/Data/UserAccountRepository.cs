using MauiApp1.Models;
using Microsoft.Data.Sqlite;

namespace MauiApp1.Data;

public sealed class UserAccountRepository
{
    private readonly SemaphoreSlim _initialization = new(1, 1);
    private bool _initialized;

    private async Task InitAsync()
    {
        if (_initialized) return;
        await _initialization.WaitAsync();
        try
        {
            if (_initialized) return;
            await using var connection = new SqliteConnection(Constants.DatabasePath);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = @"CREATE TABLE IF NOT EXISTS UserAccount (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    Username TEXT NOT NULL COLLATE NOCASE UNIQUE,
    PasswordHash BLOB NOT NULL,
    PasswordSalt BLOB NOT NULL,
    CreatedAt TEXT NOT NULL
);";
            await command.ExecuteNonQueryAsync();
            _initialized = true;
        }
        finally { _initialization.Release(); }
    }

    public async Task<UserAccount?> GetByUsernameAsync(string username)
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Username, PasswordHash, PasswordSalt, CreatedAt FROM UserAccount WHERE Username = @username LIMIT 1";
        command.Parameters.AddWithValue("@username", NormalizeUsername(username));
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }

    public async Task<bool> HasAnyAccountAsync()
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM UserAccount)";
        return Convert.ToInt32(await command.ExecuteScalarAsync()) == 1;
    }

    public async Task<UserAccount> CreateAsync(string name, string username, byte[] passwordHash, byte[] passwordSalt)
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = @"INSERT INTO UserAccount (Name, Username, PasswordHash, PasswordSalt, CreatedAt)
VALUES (@name, @username, @hash, @salt, @created);
SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@name", name.Trim());
        command.Parameters.AddWithValue("@username", NormalizeUsername(username));
        command.Parameters.Add("@hash", SqliteType.Blob).Value = passwordHash;
        command.Parameters.Add("@salt", SqliteType.Blob).Value = passwordSalt;
        command.Parameters.AddWithValue("@created", DateTime.UtcNow.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        var id = Convert.ToInt32(await command.ExecuteScalarAsync());
        return new UserAccount { Id = id, Name = name.Trim(), Username = NormalizeUsername(username), PasswordHash = passwordHash, PasswordSalt = passwordSalt, CreatedAt = DateTime.UtcNow };
    }

    private static UserAccount Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0), Name = reader.GetString(1), Username = reader.GetString(2),
        PasswordHash = reader.GetFieldValue<byte[]>(3), PasswordSalt = reader.GetFieldValue<byte[]>(4),
        CreatedAt = DateTime.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)
    };

    private static string NormalizeUsername(string username) => username.Trim().Normalize(System.Text.NormalizationForm.FormKC);
}
