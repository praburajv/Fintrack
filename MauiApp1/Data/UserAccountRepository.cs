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

    public async Task<IReadOnlyList<LocalUserSummary>> ListUserSummariesAsync()
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Username, CreatedAt FROM UserAccount ORDER BY Name, Username";
        await using var reader = await command.ExecuteReaderAsync();
        var users = new List<LocalUserSummary>();
        while (await reader.ReadAsync())
        {
            users.Add(new LocalUserSummary
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1),
                Username = reader.GetString(2),
                CreatedAt = DateTime.Parse(reader.GetString(3), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)
            });
        }
        return users;
    }

    public async Task<int> DeleteAllLocalUsersAsync()
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM UserAccount";
        var deletedCount = await command.ExecuteNonQueryAsync();
        await transaction.CommitAsync();
        return deletedCount;
    }

    public async Task<UserAccount?> GetByIdAsync(int id)
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name, Username, PasswordHash, PasswordSalt, CreatedAt FROM UserAccount WHERE Id = @id LIMIT 1";
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }

    public async Task<int> UpdatePasswordAsync(int userId, byte[] passwordHash, byte[] passwordSalt)
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE UserAccount SET PasswordHash = @hash, PasswordSalt = @salt WHERE Id = @id";
        command.Parameters.Add("@hash", SqliteType.Blob).Value = passwordHash;
        command.Parameters.Add("@salt", SqliteType.Blob).Value = passwordSalt;
        command.Parameters.AddWithValue("@id", userId);
        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected == 1)
            await transaction.CommitAsync();
        else
            await transaction.RollbackAsync();
        return rowsAffected;
    }

    public async Task<UserAccount> CreateAsync(string name, string username, byte[] passwordHash, byte[] passwordSalt)
    {
        await InitAsync();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"INSERT INTO UserAccount (Name, Username, PasswordHash, PasswordSalt, CreatedAt)
VALUES (@name, @username, @hash, @salt, @created);";
        var normalizedName = name.Trim();
        var normalizedUsername = NormalizeUsername(username);
        var createdAt = DateTime.UtcNow;
        command.Parameters.AddWithValue("@name", normalizedName);
        command.Parameters.AddWithValue("@username", normalizedUsername);
        command.Parameters.Add("@hash", SqliteType.Blob).Value = passwordHash;
        command.Parameters.Add("@salt", SqliteType.Blob).Value = passwordSalt;
        command.Parameters.AddWithValue("@created", createdAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        var rowsAffected = await command.ExecuteNonQueryAsync();
        if (rowsAffected != 1)
        {
            await transaction.RollbackAsync();
            throw new InvalidOperationException("The local account was not inserted.");
        }

        await using var idCommand = connection.CreateCommand();
        idCommand.Transaction = transaction;
        idCommand.CommandText = "SELECT last_insert_rowid()";
        var id = Convert.ToInt32(await idCommand.ExecuteScalarAsync());
        await transaction.CommitAsync();

        var insertedUser = await GetByIdAsync(id);
        if (insertedUser is null)
            throw new InvalidOperationException("The local account could not be verified after insertion.");
        return insertedUser;
    }

    private static UserAccount Read(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0), Name = reader.GetString(1), Username = reader.GetString(2),
        PasswordHash = reader.GetFieldValue<byte[]>(3), PasswordSalt = reader.GetFieldValue<byte[]>(4),
        CreatedAt = DateTime.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)
    };

    private static string NormalizeUsername(string username) => username.Trim().Normalize(System.Text.NormalizationForm.FormKC);
}
