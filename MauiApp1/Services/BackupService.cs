using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using MauiApp1.Data;
using MauiApp1.Models;
using Microsoft.Data.Sqlite;

namespace MauiApp1.Services;

public sealed class BackupService(ICriticalOperationState criticalOperations) : IBackupService
{
    private const int FormatVersion = 1;
    private const string MetadataName = "metadata.json";
    private static string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, Constants.DatabaseFilename);

    public async Task<BackupRecordSummary> GetCurrentSummaryAsync(CancellationToken token = default)
    {
        await using var c = await OpenAsync(DatabasePath, token);
        return await CountsAsync(c, token);
    }

    public async Task<(string Path, BackupMetadata Metadata)> CreateBackupAsync(CancellationToken token = default, IProgress<double>? progress = null)
    {
        using var lease = criticalOperations.Begin();
        var tempDb = Path.Combine(FileSystem.CacheDirectory, $".backup-{Guid.NewGuid():N}.db3");
        var path = Path.Combine(FileSystem.CacheDirectory, $"HouseholdExpenseTracker_{DateTime.Now:yyyyMMdd_HHmmss}.hhtbackup");
        try
        {
            Report(progress, 0);
            var counts = await GetCurrentSummaryAsync(token);
            Report(progress, 10);
            await CopyDatabaseAsync(tempDb, token);
            Report(progress, 55);
            await RemoveAuthenticationDataAsync(tempDb, token);
            Report(progress, 65);
            var metadata = new BackupMetadata { CreatedAtUtc = DateTime.UtcNow, ApplicationVersion = AppInfo.Current.VersionString, ShopCount = counts.ShopCount, PaymentModeCount = counts.PaymentModeCount, BillCount = counts.BillCount, DatabaseSha256 = await HashAsync(tempDb, token) };
            await using (var output = File.Create(path))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
            {
                await AddFileAsync(zip, Constants.DatabaseFilename, tempDb, token, progress, 65, 88);
                var entry = zip.CreateEntry(MetadataName);
                await using var stream = entry.Open();
                await JsonSerializer.SerializeAsync(stream, metadata, cancellationToken: token);
            }
            Report(progress, 90);
            var check = await ValidateBackupAsync(path, token);
            if (!check.IsValid) throw new InvalidDataException(check.ErrorMessage);
            Report(progress, 100);
            return (path, metadata);
        }
        catch { Delete(path); throw; }
        finally { Delete(tempDb); }
    }

    public async Task<BackupValidationResult> ValidateBackupAsync(string sourcePath, CancellationToken token = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return Invalid(sourcePath, "The selected backup file could not be found.");
            using var zip = ZipFile.OpenRead(sourcePath);
            var data = zip.GetEntry(Constants.DatabaseFilename);
            var metaEntry = zip.GetEntry(MetadataName);
            if (data is null || metaEntry is null) return Invalid(sourcePath, "The backup package is incomplete.");
            await using var metaStream = metaEntry.Open();
            var meta = await JsonSerializer.DeserializeAsync<BackupMetadata>(metaStream, cancellationToken: token);
            if (meta is null || meta.BackupFormatVersion != FormatVersion || meta.DatabaseSchemaVersion != FormatVersion) return Invalid(sourcePath, "This backup version is not supported.");
            var temp = Path.Combine(FileSystem.CacheDirectory, $".validate-{Guid.NewGuid():N}.db3");
            try
            {
                await using (var input = data.Open())
                await using (var output = File.Create(temp)) await input.CopyToAsync(output, token);
                if (!string.Equals(await HashAsync(temp, token), meta.DatabaseSha256, StringComparison.OrdinalIgnoreCase)) return Invalid(sourcePath, "The backup database checksum does not match.");
                var db = await ValidateDatabaseAsync(temp, token);
                if (!db.IsValid) return Invalid(sourcePath, db.Error);
                if (await CountsFromPathAsync(temp, token) != new BackupRecordSummary(meta.ShopCount, meta.PaymentModeCount, meta.BillCount)) return Invalid(sourcePath, "Backup metadata does not match the database contents.");
                return new BackupValidationResult { IsValid = true, FilePath = sourcePath, Metadata = meta };
            }
            finally { Delete(temp); }
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or JsonException or SqliteException) { return Invalid(sourcePath, "The selected file is invalid, corrupted, or incompatible."); }
    }

    public async Task RestoreBackupAsync(string backupPath, CancellationToken token = default, IProgress<double>? progress = null)
    {
        using var lease = criticalOperations.Begin();
        Report(progress, 0);
        var check = await ValidateBackupAsync(backupPath, token);
        if (!check.IsValid) throw new InvalidDataException(check.ErrorMessage);
        var safety = Path.Combine(FileSystem.CacheDirectory, $"HouseholdExpenseTracker_safety_{DateTime.UtcNow:yyyyMMdd_HHmmss}.db3");
        var candidate = Path.Combine(FileSystem.CacheDirectory, $".restore-{Guid.NewGuid():N}.db3");
        var replacing = false;
        try
        {
            var localAccount = await ReadAuthenticationAccountAsync(DatabasePath, token);
            Report(progress, 20);
            await CopyDatabaseAsync(safety, token);
            Report(progress, 45);
            await ExtractAsync(backupPath, candidate, token);
            Report(progress, 70);
            var candidateCheck = await ValidateDatabaseAsync(candidate, token);
            if (!candidateCheck.IsValid) throw new InvalidDataException(candidateCheck.Error);
            await ReplaceDatabaseContentsAsync(candidate, DatabasePath, token);
            replacing = true;
            await RestoreAuthenticationAccountAsync(DatabasePath, localAccount, token);
            Report(progress, 90);
            var live = await ValidateDatabaseAsync(DatabasePath, token);
            if (!live.IsValid) throw new InvalidDataException(live.Error);
            Delete(safety);
            Report(progress, 100);
        }
        catch
        {
            Delete(candidate);
            if (replacing)
            {
                await ReplaceDatabaseContentsAsync(safety, DatabasePath, CancellationToken.None);
                var rollback = await ValidateDatabaseAsync(DatabasePath, CancellationToken.None);
                if (!rollback.IsValid) throw new InvalidOperationException("Restore failed and rollback validation also failed. The safety backup was preserved at " + safety);
            }
            throw;
        }
    }

    private static async Task CopyDatabaseAsync(string destination, CancellationToken token)
    {
        Delete(destination);
        await using var source = await OpenAsync(DatabasePath, token);
        await using var target = await OpenAsync(destination, token);
        source.BackupDatabase(target);
    }

    private static async Task ReplaceDatabaseContentsAsync(string sourcePath, string destinationPath, CancellationToken token)
    {
        await using var source = await OpenAsync(sourcePath, token);
        await using var destination = await OpenAsync(destinationPath, token);
        source.BackupDatabase(destination);
    }

    private static async Task RemoveAuthenticationDataAsync(string path, CancellationToken token)
    {
        await using var connection = await OpenAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP TABLE IF EXISTS UserAccount";
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task<UserAccount?> ReadAuthenticationAccountAsync(string path, CancellationToken token)
    {
        if (!File.Exists(path)) return null;
        await using var connection = await OpenAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM sqlite_master WHERE type='table' AND name='UserAccount'";
        if (Convert.ToInt32(await command.ExecuteScalarAsync(token)) != 1) return null;
        command.CommandText = "SELECT Id, Name, Username, PasswordHash, PasswordSalt, CreatedAt FROM UserAccount LIMIT 1";
        await using var reader = await command.ExecuteReaderAsync(token);
        if (!await reader.ReadAsync(token)) return null;
        return new UserAccount
        {
            Id = reader.GetInt32(0), Name = reader.GetString(1), Username = reader.GetString(2),
            PasswordHash = (byte[])reader[3], PasswordSalt = (byte[])reader[4], CreatedAt = DateTime.Parse(reader.GetString(5), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind)
        };
    }

    private static async Task RestoreAuthenticationAccountAsync(string path, UserAccount? account, CancellationToken token)
    {
        await using var connection = await OpenAsync(path, token);
        await using var command = connection.CreateCommand();
        command.CommandText = @"DROP TABLE IF EXISTS UserAccount;
CREATE TABLE UserAccount (
    Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL, Username TEXT NOT NULL COLLATE NOCASE UNIQUE,
    PasswordHash BLOB NOT NULL, PasswordSalt BLOB NOT NULL, CreatedAt TEXT NOT NULL);";
        await command.ExecuteNonQueryAsync(token);

        if (account is null) return;

        command.CommandText = @"INSERT INTO UserAccount (Id, Name, Username, PasswordHash, PasswordSalt, CreatedAt)
VALUES (@id, @name, @username, @hash, @salt, @created);";
        command.Parameters.AddWithValue("@id", account.Id);
        command.Parameters.AddWithValue("@name", account.Name);
        command.Parameters.AddWithValue("@username", account.Username);
        command.Parameters.Add("@hash", SqliteType.Blob).Value = account.PasswordHash;
        command.Parameters.Add("@salt", SqliteType.Blob).Value = account.PasswordSalt;
        command.Parameters.AddWithValue("@created", account.CreatedAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        await command.ExecuteNonQueryAsync(token);
    }

    private static async Task ExtractAsync(string path, string destination, CancellationToken token)
    {
        Delete(destination);
        using var zip = ZipFile.OpenRead(path);
        var entry = zip.GetEntry(Constants.DatabaseFilename) ?? throw new InvalidDataException("Database entry is missing.");
        await using var input = entry.Open();
        await using var output = File.Create(destination);
        await input.CopyToAsync(output, token);
    }

    private static async Task AddFileAsync(ZipArchive zip, string name, string path, CancellationToken token, IProgress<double>? progress, double start, double end)
    {
        var entry = zip.CreateEntry(name);
        await using var input = File.OpenRead(path);
        await using var output = entry.Open();
        var total = input.Length;
        var copied = 0L;
        var buffer = new byte[81920];
        int read;
        while ((read = await input.ReadAsync(buffer, token)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            copied += read;
            Report(progress, start + (end - start) * copied / Math.Max(1d, total));
        }
    }

    private static async Task<(bool IsValid, string Error)> ValidateDatabaseAsync(string path, CancellationToken token)
    {
        await using var c = await OpenAsync(path, token);
        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table'";
            await using var reader = await cmd.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token)) tables.Add(reader.GetString(0));
        }
        if (new[] { "Shop", "Payment_mode", "Bill_transcation" }.Any(x => !tables.Contains(x))) return (false, "The backup database is missing required application tables.");
        await using var integrity = c.CreateCommand(); integrity.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals(Convert.ToString(await integrity.ExecuteScalarAsync(token)), "ok", StringComparison.OrdinalIgnoreCase)) return (false, "SQLite integrity validation failed.");
        await using var fk = c.CreateCommand(); fk.CommandText = "PRAGMA foreign_key_check;";
        await using var fkReader = await fk.ExecuteReaderAsync(token);
        return await fkReader.ReadAsync(token) ? (false, "SQLite relationship validation failed.") : (true, string.Empty);
    }

    private static async Task<BackupRecordSummary> CountsFromPathAsync(string path, CancellationToken token) { await using var c = await OpenAsync(path, token); return await CountsAsync(c, token); }
    private static async Task<BackupRecordSummary> CountsAsync(SqliteConnection c, CancellationToken token)
    {
        async Task<int> Count(string table) { await using var cmd = c.CreateCommand(); cmd.CommandText = $"SELECT COUNT(*) FROM {table}"; return Convert.ToInt32(await cmd.ExecuteScalarAsync(token)); }
        return new(await Count("Shop"), await Count("Payment_mode"), await Count("Bill_transcation"));
    }
    private static async Task<SqliteConnection> OpenAsync(string path, CancellationToken token) { var c = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate;Cache=Shared"); await c.OpenAsync(token); return c; }
    private static async Task<string> HashAsync(string path, CancellationToken token) { await using var s = File.OpenRead(path); using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var b = new byte[81920]; int n; while ((n = await s.ReadAsync(b, token)) > 0) h.AppendData(b, 0, n); return Convert.ToHexString(h.GetHashAndReset()); }
    private static BackupValidationResult Invalid(string? path, string error) => new() { FilePath = path ?? string.Empty, ErrorMessage = error };
    private static void Report(IProgress<double>? progress, double value) => progress?.Report(Math.Clamp(value, 0, 100));
    private static void Delete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
}
