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
    private const string SelectedDataName = "selected-months.json";
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
            if (metaEntry is null) return Invalid(sourcePath, "The backup package is incomplete.");
            await using var metaStream = metaEntry.Open();
            var meta = await JsonSerializer.DeserializeAsync<BackupMetadata>(metaStream, cancellationToken: token);
            if (meta is null || meta.BackupFormatVersion != FormatVersion || meta.DatabaseSchemaVersion != FormatVersion) return Invalid(sourcePath, "This backup version is not supported.");
            if (string.Equals(meta.BackupType, "SelectedMonths", StringComparison.OrdinalIgnoreCase))
            {
                var selected = await ReadSelectedBackupAsync(sourcePath, token);
                if (selected.Data.Year != meta.SelectedYear || !selected.Data.Months.SequenceEqual(meta.SelectedMonths.OrderBy(x => x)) || selected.Data.Bills.Count != meta.SelectedMonthBillCount)
                    return Invalid(sourcePath, "Selected-month backup metadata does not match its data.");
                return new BackupValidationResult { IsValid = true, FilePath = sourcePath, Metadata = meta };
            }
            if (data is null) return Invalid(sourcePath, "The backup package is incomplete.");
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
            await EnsureIncomeCompatibilitySchemaAsync(token);
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

    public async Task<(string Path, BackupMetadata Metadata)> CreateSelectedMonthsBackupAsync(int year, IReadOnlyCollection<int> months, CancellationToken token = default, IProgress<double>? progress = null)
    {
        var selectedMonths = months.Distinct().Where(month => month is >= 1 and <= 12).OrderBy(month => month).ToArray();
        if (selectedMonths.Length == 0) throw new ArgumentException("Select at least one month.", nameof(months));
        using var lease = criticalOperations.Begin();
        var path = Path.Combine(FileSystem.CacheDirectory, $"HouseholdExpenseTracker_Selected_{year}_{DateTime.Now:yyyyMMdd_HHmmss}.hhtbackup");
        try
        {
            Report(progress, 0);
            var document = await ReadSelectedDataAsync(year, selectedMonths, token);
            Report(progress, 20);
            var metadata = new BackupMetadata
            {
                BackupType = "SelectedMonths",
                CreatedAtUtc = DateTime.UtcNow,
                ApplicationVersion = AppInfo.Current.VersionString,
                SelectedYear = year,
                SelectedMonths = selectedMonths.ToList(),
                SelectedMonthBillCount = document.Bills.Count,
                BillCount = document.Bills.Count
            };
            await using (var output = File.Create(path))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
            {
                var dataEntry = zip.CreateEntry(SelectedDataName);
                await using (var stream = dataEntry.Open())
                    await JsonSerializer.SerializeAsync(stream, document, cancellationToken: token);
                Report(progress, 75);
                var metadataEntry = zip.CreateEntry(MetadataName);
                await using var metadataStream = metadataEntry.Open();
                await JsonSerializer.SerializeAsync(metadataStream, metadata, cancellationToken: token);
            }
            Report(progress, 100);
            return (path, metadata);
        }
        catch { Delete(path); throw; }
    }

    public async Task<SelectedMonthsRestoreResult> RestoreSelectedMonthsBackupAsync(string backupPath, CancellationToken token = default, IProgress<double>? progress = null)
    {
        using var lease = criticalOperations.Begin();
        Report(progress, 0);
        var document = await ReadSelectedBackupAsync(backupPath, token);
        var data = document.Data;
        if (document.Metadata.SelectedYear is null || document.Metadata.SelectedMonths.Count == 0)
            throw new InvalidDataException("The selected-month backup does not contain valid month information.");
        if (data.Year != document.Metadata.SelectedYear.Value || !data.Months.SequenceEqual(document.Metadata.SelectedMonths.OrderBy(x => x)))
            throw new InvalidDataException("The selected-month backup metadata does not match its data.");
        if (data.Months.Any(month => month is < 1 or > 12) || data.Bills.Any(bill => bill.BillDate.Year != data.Year || !data.Months.Contains(bill.BillDate.Month)))
            throw new InvalidDataException("The selected-month backup contains bills outside its declared date range.");
        Report(progress, 10);

        await EnsureSelectedRestoreSchemaAsync(token);
        await using var connection = await OpenAsync(DatabasePath, token);
        await using var transaction = connection.BeginTransaction();
        try
        {
            await using (var delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM Bill_transcation WHERE Bill_date >= @from AND Bill_date < @to";
                foreach (var month in data.Months)
                {
                    var from = new DateTime(data.Year, month, 1);
                    delete.Parameters.Clear();
                    delete.Parameters.AddWithValue("@from", from.ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                    delete.Parameters.AddWithValue("@to", from.AddMonths(1).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
                    await delete.ExecuteNonQueryAsync(token);
                }
            }
            Report(progress, 20);
            var shops = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var shop in data.Shops.Values)
                shops[shop.Name] = await ResolveShopAsync(connection, transaction, shop, token);
            var paymentModes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var mode in data.PaymentModes.Values)
                paymentModes[mode.Name] = await ResolvePaymentModeAsync(connection, transaction, mode.Name, token);

            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Bill_transcation(Bill_date,Shop_Id,Payment_Mode_Id,Bill_value,Note,EtlImportId) VALUES(@date,@shop,@mode,@value,@note,@import)";
            var date = insert.Parameters.Add("@date", SqliteType.Text);
            var shopId = insert.Parameters.Add("@shop", SqliteType.Integer);
            var modeId = insert.Parameters.Add("@mode", SqliteType.Integer);
            var value = insert.Parameters.Add("@value", SqliteType.Text);
            var note = insert.Parameters.Add("@note", SqliteType.Text);
            var importId = insert.Parameters.Add("@import", SqliteType.Text);
            for (var index = 0; index < data.Bills.Count; index++)
            {
                var bill = data.Bills[index];
                if (!shops.TryGetValue(bill.ShopName, out var resolvedShop) || !paymentModes.TryGetValue(bill.PaymentModeName, out var resolvedMode))
                    throw new InvalidDataException("A bill references a missing shop or payment mode.");
                date.Value = bill.BillDate.ToString("O", System.Globalization.CultureInfo.InvariantCulture);
                shopId.Value = resolvedShop;
                modeId.Value = resolvedMode;
                value.Value = bill.BillValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
                note.Value = string.IsNullOrWhiteSpace(bill.Note) ? DBNull.Value : bill.Note;
                importId.Value = (object?)bill.EtlImportId ?? DBNull.Value;
                await insert.ExecuteNonQueryAsync(token);
                Report(progress, 20 + (index + 1) * 70d / Math.Max(1, data.Bills.Count));
            }
            await transaction.CommitAsync(token);
            Report(progress, 100);
            return new SelectedMonthsRestoreResult { Year = data.Year, Months = data.Months, Imported = data.Bills.Count, Replaced = data.Months.Count, Failed = 0 };
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static async Task<SelectedBackupDocument> ReadSelectedDataAsync(int year, IReadOnlyList<int> months, CancellationToken token)
    {
        await using var connection = await OpenAsync(DatabasePath, token);
        await EtlImportSchema.EnsureAsync(connection);
        var document = new SelectedBackupDocument { Year = year, Months = months.ToList() };
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT b.Bill_date,b.Bill_value,b.Note,s.Name,p.Name,COALESCE(c.Name,s.Category,'Default Shop'),COALESCE(c.IconKey,'default_shop'),b.EtlImportId
FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id JOIN Payment_mode p ON p.Id=b.Payment_Mode_Id
LEFT JOIN Category c ON c.Id=s.Category_Id WHERE b.Bill_date >= @from AND b.Bill_date < @to ORDER BY b.Bill_date,b.Id";
        command.Parameters.AddWithValue("@from", new DateTime(year, 1, 1).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("@to", new DateTime(year + 1, 1, 1).ToString("O", System.Globalization.CultureInfo.InvariantCulture));
        var selected = months.ToHashSet();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            var date = DateTime.Parse(reader.GetString(0), System.Globalization.CultureInfo.InvariantCulture);
            if (!selected.Contains(date.Month)) continue;
            var shopName = reader.GetString(3);
            var modeName = reader.GetString(4);
            document.Shops.TryAdd(shopName, new SelectedBackupShop { Name = shopName, CategoryName = reader.IsDBNull(5) ? "Default Shop" : reader.GetString(5), CategoryIconKey = reader.IsDBNull(6) ? "default_shop" : reader.GetString(6) });
            document.PaymentModes.TryAdd(modeName, new SelectedBackupPaymentMode { Name = modeName });
            document.Bills.Add(new SelectedBackupBill { BillDate = date, BillValue = ParseDecimal(reader.GetValue(1)), Note = reader.IsDBNull(2) ? string.Empty : reader.GetString(2), ShopName = shopName, PaymentModeName = modeName, EtlImportId = reader.IsDBNull(7) ? null : reader.GetString(7) });
        }
        return document;
    }

    private static async Task<(BackupMetadata Metadata, SelectedBackupDocument Data)> ReadSelectedBackupAsync(string path, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) throw new InvalidDataException("The selected backup file could not be found.");
        using var zip = ZipFile.OpenRead(path);
        var dataEntry = zip.GetEntry(SelectedDataName) ?? throw new InvalidDataException("The selected-month data is missing.");
        var metadataEntry = zip.GetEntry(MetadataName) ?? throw new InvalidDataException("The backup metadata is missing.");
        await using var metadataStream = metadataEntry.Open();
        var metadata = await JsonSerializer.DeserializeAsync<BackupMetadata>(metadataStream, cancellationToken: token) ?? throw new InvalidDataException("The backup metadata is invalid.");
        if (!string.Equals(metadata.BackupType, "SelectedMonths", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("This is not a selected-month backup.");
        await using var dataStream = dataEntry.Open();
        var data = await JsonSerializer.DeserializeAsync<SelectedBackupDocument>(dataStream, cancellationToken: token) ?? throw new InvalidDataException("The selected-month data is invalid.");
        return (metadata, data);
    }

    private static async Task EnsureSelectedRestoreSchemaAsync(CancellationToken token)
    {
        await using var connection = await OpenAsync(DatabasePath, token);
        await using var command = connection.CreateCommand();
        command.CommandText = @"PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS Payment_mode (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name VARCHAR(225) NOT NULL COLLATE NOCASE UNIQUE);
CREATE TABLE IF NOT EXISTS Category (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE UNIQUE, IconKey TEXT NOT NULL DEFAULT 'default_shop', Budgeted REAL NOT NULL DEFAULT 0);
CREATE TABLE IF NOT EXISTS Shop (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name VARCHAR(225) NOT NULL COLLATE NOCASE UNIQUE, Category TEXT, Category_Id INTEGER);
CREATE TABLE IF NOT EXISTS Bill_transcation (Id INTEGER PRIMARY KEY AUTOINCREMENT, Bill_date DATETIME NOT NULL, Shop_Id INTEGER NOT NULL, Payment_Mode_Id INTEGER NOT NULL, Bill_value NUMERIC NOT NULL, Note VARCHAR(1000), FOREIGN KEY(Shop_Id) REFERENCES Shop(Id), FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));";
        await command.ExecuteNonQueryAsync(token);
        await EtlImportSchema.EnsureAsync(connection);
    }

    private static async Task<int> ResolvePaymentModeAsync(SqliteConnection connection, SqliteTransaction transaction, string name, CancellationToken token)
    {
        await using var find = connection.CreateCommand(); find.Transaction = transaction; find.CommandText = "SELECT Id FROM Payment_mode WHERE Name=@name COLLATE NOCASE"; find.Parameters.AddWithValue("@name", name);
        var existing = await find.ExecuteScalarAsync(token); if (existing is not null) return Convert.ToInt32(existing);
        await using var insert = connection.CreateCommand(); insert.Transaction = transaction; insert.CommandText = "INSERT INTO Payment_mode(Name) VALUES(@name); SELECT last_insert_rowid();"; insert.Parameters.AddWithValue("@name", name); return Convert.ToInt32(await insert.ExecuteScalarAsync(token));
    }

    private static async Task<int> ResolveShopAsync(SqliteConnection connection, SqliteTransaction transaction, SelectedBackupShop shop, CancellationToken token)
    {
        await using var find = connection.CreateCommand(); find.Transaction = transaction; find.CommandText = "SELECT Id FROM Shop WHERE Name=@name COLLATE NOCASE"; find.Parameters.AddWithValue("@name", shop.Name);
        var existing = await find.ExecuteScalarAsync(token); if (existing is not null) return Convert.ToInt32(existing);
        await using var category = connection.CreateCommand(); category.Transaction = transaction; category.CommandText = "SELECT Id FROM Category WHERE Name=@name COLLATE NOCASE"; category.Parameters.AddWithValue("@name", shop.CategoryName);
        var categoryId = await category.ExecuteScalarAsync(token);
        if (categoryId is null)
        {
            await using var createCategory = connection.CreateCommand(); createCategory.Transaction = transaction; createCategory.CommandText = "INSERT INTO Category(Name,IconKey,Budgeted) VALUES(@name,@icon,0); SELECT last_insert_rowid();"; createCategory.Parameters.AddWithValue("@name", shop.CategoryName); createCategory.Parameters.AddWithValue("@icon", shop.CategoryIconKey); categoryId = await createCategory.ExecuteScalarAsync(token);
        }
        await using var insert = connection.CreateCommand(); insert.Transaction = transaction; insert.CommandText = "INSERT INTO Shop(Name,Category,Category_Id) VALUES(@name,@category,@categoryId); SELECT last_insert_rowid();"; insert.Parameters.AddWithValue("@name", shop.Name); insert.Parameters.AddWithValue("@category", shop.CategoryName); insert.Parameters.AddWithValue("@categoryId", Convert.ToInt32(categoryId)); return Convert.ToInt32(await insert.ExecuteScalarAsync(token));
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

    private static async Task EnsureIncomeCompatibilitySchemaAsync(CancellationToken token)
    {
        await using var connection = await OpenAsync(DatabasePath, token);
        await using var command = connection.CreateCommand();
        command.CommandText = @"PRAGMA foreign_keys = ON;
CREATE TABLE IF NOT EXISTS income_transaction (
 Id INTEGER PRIMARY KEY AUTOINCREMENT,
 Transaction_date DATETIME NOT NULL,
 Shop_Id INTEGER NOT NULL,
 Payment_Mode_Id INTEGER NOT NULL,
 Amount NUMERIC NOT NULL CHECK(Amount > 0),
 Note VARCHAR(1000),
 FOREIGN KEY(Shop_Id) REFERENCES Shop(Id),
 FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));
CREATE INDEX IF NOT EXISTS IX_income_transaction_Transaction_date ON income_transaction(Transaction_date);
CREATE INDEX IF NOT EXISTS IX_income_transaction_Shop_Id ON income_transaction(Shop_Id);
CREATE INDEX IF NOT EXISTS IX_income_transaction_Payment_Mode_Id ON income_transaction(Payment_Mode_Id);
CREATE TABLE IF NOT EXISTS Category (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE UNIQUE, IconKey TEXT NOT NULL DEFAULT 'default_shop', Budgeted REAL NOT NULL DEFAULT 0);
INSERT OR IGNORE INTO Category(Name,IconKey,Budgeted) VALUES('Credits','default_shop',0);";
        await command.ExecuteNonQueryAsync(token);
        await EtlImportSchema.EnsureAsync(connection);
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
    // Backup files are read/hashed/deleted immediately after use. A pooled handle
    // can retain a Windows file lock beyond Dispose and prevent those operations.
    private static async Task<SqliteConnection> OpenAsync(string path, CancellationToken token) { var c = new SqliteConnection($"Data Source={path};Mode=ReadWriteCreate;Cache=Shared;Pooling=False"); await c.OpenAsync(token); return c; }
    private static decimal ParseDecimal(object value) => decimal.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0;
    private static async Task<string> HashAsync(string path, CancellationToken token) { await using var s = File.OpenRead(path); using var h = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var b = new byte[81920]; int n; while ((n = await s.ReadAsync(b, token)) > 0) h.AppendData(b, 0, n); return Convert.ToHexString(h.GetHashAndReset()); }
    private static BackupValidationResult Invalid(string? path, string error) => new() { FilePath = path ?? string.Empty, ErrorMessage = error };
    private static void Report(IProgress<double>? progress, double value) => progress?.Report(Math.Clamp(value, 0, 100));
    private static void Delete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }

    private sealed class SelectedBackupDocument
    {
        public int Year { get; set; }
        public List<int> Months { get; set; } = [];
        public Dictionary<string, SelectedBackupShop> Shops { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, SelectedBackupPaymentMode> PaymentModes { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public List<SelectedBackupBill> Bills { get; set; } = [];
    }

    private sealed class SelectedBackupShop
    {
        public string Name { get; set; } = string.Empty;
        public string CategoryName { get; set; } = "Default Shop";
        public string CategoryIconKey { get; set; } = "default_shop";
    }

    private sealed class SelectedBackupPaymentMode
    {
        public string Name { get; set; } = string.Empty;
    }

    private sealed class SelectedBackupBill
    {
        public string? EtlImportId { get; set; }
        public DateTime BillDate { get; set; }
        public string ShopName { get; set; } = string.Empty;
        public string PaymentModeName { get; set; } = string.Empty;
        public decimal BillValue { get; set; }
        public string Note { get; set; } = string.Empty;
    }
}
