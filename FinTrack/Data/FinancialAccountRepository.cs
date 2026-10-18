using FinTracker.Models;
using FinTracker.Services;
using Microsoft.Data.Sqlite;

namespace FinTracker.Data;

/// <summary>Owns account creation and the one-time conversion of the legacy shared catalogs.</summary>
public sealed class FinancialAccountRepository(FinancialAccountContext context, ICriticalOperationState operations, ICurrencyService currencies)
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _initialized;

    public void InvalidateAfterDatabaseRestore() => _initialized = false;

    public async Task InitializeAsync()
    {
        if (_initialized) return;
        await _gate.WaitAsync();
        try
        {
            if (_initialized) return;
            var file = Path.Combine(FileSystem.AppDataDirectory, Constants.DatabaseFilename);
            var hadDatabase = File.Exists(file) && new FileInfo(file).Length > 0;
            await using var db = new SqliteConnection(Constants.DatabasePath);
            await db.OpenAsync();
            if (!await ExistsAsync(db, "FinancialAccount"))
            {
                // Snapshot before changing a pre-existing installation. A failed migration leaves it recoverable.
                if (hadDatabase)
                {
                    var safety = Path.Combine(FileSystem.AppDataDirectory, $"pre-financial-accounts-{DateTime.UtcNow:yyyyMMdd-HHmmss}.db3");
                    await using var destination = new SqliteConnection($"Data Source={safety}");
                    await destination.OpenAsync();
                    db.BackupDatabase(destination);
                }
                await MigrateAsync(db);
            }
            _initialized = true;
        }
        finally { _gate.Release(); }
    }

    private static async Task MigrateAsync(SqliteConnection db)
    {
        // The legacy category repair/seeder has to run before removing its global Name constraint.
        await CategoryRepository.EnsureSchemaAndSeedAsync(db);
        await ExecuteAsync(db, "CREATE TABLE IF NOT EXISTS Shop (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE UNIQUE)");
        await ShopRepository.EnsureCategoryColumnAsync(db);
        await ShopRepository.EnsureCategoryIdColumnAndMigrationAsync(db);
        await ExecuteAsync(db, "CREATE TABLE IF NOT EXISTS Payment_mode (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE UNIQUE)");
        await ExecuteAsync(db, "PRAGMA foreign_keys=OFF");
        await using var transaction = (SqliteTransaction)await db.BeginTransactionAsync();
        try
        {
            await ExecuteAsync(db, @"CREATE TABLE FinancialAccount (
Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE UNIQUE,
Address TEXT NOT NULL DEFAULT '', LogoPath TEXT NOT NULL DEFAULT '', ProfilePicturePath TEXT NOT NULL DEFAULT '',
CurrencyCode TEXT NOT NULL DEFAULT 'EUR', IsActive INTEGER NOT NULL DEFAULT 1, IsDefault INTEGER NOT NULL DEFAULT 0)", transaction);
            await ExecuteAsync(db, @"INSERT INTO FinancialAccount(Name,Address,LogoPath,ProfilePicturePath,CurrencyCode,IsActive,IsDefault)
VALUES('scott_tiger','350 Fifth Avenue, Newyork','et_top_menu_logo.png','et_top_menu_logo.png','EUR',1,1)", transaction);
            var accountId = Convert.ToInt32(await ScalarAsync(db, "SELECT Id FROM FinancialAccount WHERE IsDefault=1", transaction));

            await RebuildAsync(db, transaction, "Category", @"CREATE TABLE Category_new (
Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE,
IconKey TEXT NOT NULL, Budgeted REAL NOT NULL DEFAULT 0, FinancialAccountId INTEGER NOT NULL,
UNIQUE(FinancialAccountId,Name))", "Id,Name,IconKey,Budgeted,FinancialAccountId", $"Id,Name,IconKey,Budgeted,{accountId}");
            await RebuildAsync(db, transaction, "Shop", @"CREATE TABLE Shop_new (
Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE,
Category TEXT, Category_Id INTEGER, FinancialAccountId INTEGER NOT NULL,
UNIQUE(FinancialAccountId,Name))", "Id,Name,Category,Category_Id,FinancialAccountId", $"Id,Name,Category,Category_Id,{accountId}");
            await RebuildAsync(db, transaction, "Payment_mode", @"CREATE TABLE Payment_mode_new (
Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE,
FinancialAccountId INTEGER NOT NULL, UNIQUE(FinancialAccountId,Name))", "Id,Name,FinancialAccountId", $"Id,Name,{accountId}");

            await RebuildIfPresentAsync(db, transaction, "MonthlyBudgetIncome", @"CREATE TABLE MonthlyBudgetIncome_new (
FinancialAccountId INTEGER NOT NULL, Year INTEGER NOT NULL, Month INTEGER NOT NULL,
Income REAL NOT NULL DEFAULT 0, PRIMARY KEY(FinancialAccountId,Year,Month))", "FinancialAccountId,Year,Month,Income", $"{accountId},Year,Month,Income");
            await RebuildIfPresentAsync(db, transaction, "SetUpKeyValue", @"CREATE TABLE SetUpKeyValue_new (
Id INTEGER PRIMARY KEY AUTOINCREMENT, FinancialAccountId INTEGER NOT NULL,
""Key"" TEXT NOT NULL COLLATE NOCASE, Value TEXT NOT NULL,
UNIQUE(FinancialAccountId,""Key""))", "Id,FinancialAccountId,\"Key\",Value", $"Id,{accountId},\"Key\",Value");

            foreach (var table in new[] { "Bill_transcation", "income_transaction", "ETL_TEXT_MAP", "Recurring_Bill" })
            {
                if (!await ExistsAsync(db, table, transaction)) continue;
                await ExecuteAsync(db, $"ALTER TABLE {table} ADD COLUMN FinancialAccountId INTEGER", transaction);
                await ExecuteAsync(db, $"UPDATE {table} SET FinancialAccountId={accountId} WHERE FinancialAccountId IS NULL", transaction);
                await ExecuteAsync(db, $"CREATE INDEX IF NOT EXISTS IX_{table}_FinancialAccountId ON {table}(FinancialAccountId)", transaction);
            }
            await ExecuteAsync(db, "INSERT OR IGNORE INTO Payment_mode(Name,FinancialAccountId) VALUES('ETL_INCOME'," + accountId + "),('Debit Card'," + accountId + ")", transaction);
            var badReferences = Convert.ToInt32(await ScalarAsync(db, "SELECT COUNT(*) FROM pragma_foreign_key_check", transaction));
            if (badReferences != 0) throw new InvalidDataException($"Account migration found {badReferences} broken foreign-key references.");
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
        finally { await ExecuteAsync(db, "PRAGMA foreign_keys=ON"); }
    }

    private static async Task RebuildIfPresentAsync(SqliteConnection db, SqliteTransaction transaction, string table, string create, string columns, string source)
    {
        if (await ExistsAsync(db, table, transaction)) await RebuildAsync(db, transaction, table, create, columns, source);
    }

    private static async Task RebuildAsync(SqliteConnection db, SqliteTransaction transaction, string table, string create, string columns, string source)
    {
        var before = Convert.ToInt64(await ScalarAsync(db, $"SELECT COUNT(*) FROM {table}", transaction));
        await ExecuteAsync(db, create, transaction);
        await ExecuteAsync(db, $"INSERT INTO {table}_new({columns}) SELECT {source} FROM {table}", transaction);
        var after = Convert.ToInt64(await ScalarAsync(db, $"SELECT COUNT(*) FROM {table}_new", transaction));
        if (before != after) throw new InvalidDataException($"{table} row count changed during migration.");
        await ExecuteAsync(db, $"DROP TABLE {table}", transaction);
        await ExecuteAsync(db, $"ALTER TABLE {table}_new RENAME TO {table}", transaction);
    }

    private static async Task<bool> ExistsAsync(SqliteConnection db, string table, SqliteTransaction? transaction = null)
    {
        await using var command = db.CreateCommand(); command.Transaction = transaction;
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
        command.Parameters.AddWithValue("@name", table);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) != 0;
    }

    private static async Task<object?> ScalarAsync(SqliteConnection db, string sql, SqliteTransaction? transaction = null)
    {
        await using var command = db.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        return await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteAsync(SqliteConnection db, string sql, SqliteTransaction? transaction = null)
    {
        await using var command = db.CreateCommand(); command.Transaction = transaction; command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    public async Task<IReadOnlyList<FinancialAccount>> ListAsync()
        => await ListByStatusAsync(true);

    public async Task<IReadOnlyList<FinancialAccount>> ListInactiveAsync()
        => await ListByStatusAsync(false);

    private async Task<IReadOnlyList<FinancialAccount>> ListByStatusAsync(bool active)
    {
        await InitializeAsync();
        await using var db = new SqliteConnection(Constants.DatabasePath); await db.OpenAsync();
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT Id,Name,Address,LogoPath,ProfilePicturePath,CurrencyCode,IsActive,IsDefault FROM FinancialAccount WHERE IsActive=@active ORDER BY IsDefault DESC,Name COLLATE NOCASE";
        command.Parameters.AddWithValue("@active", active ? 1 : 0);
        var rows = new List<FinancialAccount>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(Read(reader));
        return rows;
    }

    public async Task<FinancialAccount?> GetAsync(int id)
    {
        await InitializeAsync();
        await using var db = new SqliteConnection(Constants.DatabasePath); await db.OpenAsync();
        await using var command = db.CreateCommand();
        command.CommandText = "SELECT Id,Name,Address,LogoPath,ProfilePicturePath,CurrencyCode,IsActive,IsDefault FROM FinancialAccount WHERE Id=@id";
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync();
        return await reader.ReadAsync() ? Read(reader) : null;
    }

    public async Task<FinancialAccount> CreateAsync(FinancialAccount account, bool copyCategories)
    {
        await InitializeAsync();
        using var operation = operations.Begin();
        var source = context.RequireId();
        if (string.IsNullOrWhiteSpace(account.Name)) throw new ArgumentException("Company name is required.");
        if (!currencies.AvailableCurrencies.Any(x => x.Code.Equals(account.CurrencyCode, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Choose a supported display currency.");
        await using var db = new SqliteConnection(Constants.DatabasePath); await db.OpenAsync();
        await using var transaction = (SqliteTransaction)await db.BeginTransactionAsync();
        try
        {
            await using var insert = db.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = @"INSERT INTO FinancialAccount(Name,Address,LogoPath,ProfilePicturePath,CurrencyCode,IsActive,IsDefault)
VALUES(@name,@address,@logo,@profile,@currency,1,0); SELECT last_insert_rowid()";
            insert.Parameters.AddWithValue("@name", account.Name.Trim()); insert.Parameters.AddWithValue("@address", account.Address.Trim());
            insert.Parameters.AddWithValue("@logo", account.LogoPath); insert.Parameters.AddWithValue("@profile", account.ProfilePicturePath);
            insert.Parameters.AddWithValue("@currency", account.CurrencyCode);
            account.Id = Convert.ToInt32(await insert.ExecuteScalarAsync());
            if (copyCategories)
            {
                await using var copy = db.CreateCommand(); copy.Transaction = transaction;
                copy.CommandText = @"INSERT INTO Category(Name,IconKey,Budgeted,FinancialAccountId)
SELECT Name,IconKey,0,@target FROM Category WHERE FinancialAccountId=@source";
                copy.Parameters.AddWithValue("@target", account.Id); copy.Parameters.AddWithValue("@source", source);
                await copy.ExecuteNonQueryAsync();
            }
            else await CategoryRepository.SeedForAccountAsync(db, account.Id, transaction);
            await transaction.CommitAsync();
            return account;
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async Task<FinancialAccount> UpdateAsync(FinancialAccount account)
    {
        await InitializeAsync();
        using var operation = operations.Begin();
        if (account.Id <= 0 || string.IsNullOrWhiteSpace(account.Name))
            throw new ArgumentException("An existing company and its name are required.");
        if (!currencies.AvailableCurrencies.Any(x => x.Code.Equals(account.CurrencyCode, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("Choose a supported display currency.");
        await using var db = new SqliteConnection(Constants.DatabasePath); await db.OpenAsync();
        await using var command = db.CreateCommand();
        command.CommandText = @"UPDATE FinancialAccount SET Name=@name,Address=@address,CurrencyCode=@currency
WHERE Id=@id AND IsActive=1";
        command.Parameters.AddWithValue("@id", account.Id);
        command.Parameters.AddWithValue("@name", account.Name.Trim());
        command.Parameters.AddWithValue("@address", account.Address.Trim());
        command.Parameters.AddWithValue("@currency", account.CurrencyCode);
        if (await command.ExecuteNonQueryAsync() != 1)
            throw new InvalidOperationException("This company is no longer active.");
        var saved = await GetAsync(account.Id) ?? throw new InvalidOperationException("The company could not be loaded after saving.");
        if (context.Current?.Id == saved.Id) context.Select(saved);
        return saved;
    }

    public async Task DeactivateAsync(int id)
    {
        await InitializeAsync();
        using var operation = operations.Begin();
        if (context.Current?.Id == id) throw new InvalidOperationException("Select another company before deactivating this one.");
        await using var db = new SqliteConnection(Constants.DatabasePath); await db.OpenAsync();
        await using var transaction = (SqliteTransaction)await db.BeginTransactionAsync();
        try
        {
            if (Convert.ToInt32(await ScalarAsync(db, "SELECT COUNT(*) FROM FinancialAccount WHERE IsActive=1", transaction)) <= 1)
                throw new InvalidOperationException("The last active company cannot be deactivated.");
            await using var command = db.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "UPDATE FinancialAccount SET IsActive=0 WHERE Id=@id AND IsActive=1 AND IsDefault=0";
            command.Parameters.AddWithValue("@id", id);
            if (await command.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("The default or an inactive company cannot be deactivated.");
            await transaction.CommitAsync();
        }
        catch { await transaction.RollbackAsync(); throw; }
    }

    public async Task ReactivateAsync(int id)
    {
        await InitializeAsync();
        using var operation = operations.Begin();
        await using var db = new SqliteConnection(Constants.DatabasePath); await db.OpenAsync();
        await using var command = db.CreateCommand();
        command.CommandText = "UPDATE FinancialAccount SET IsActive=1 WHERE Id=@id AND IsActive=0";
        command.Parameters.AddWithValue("@id", id);
        if (await command.ExecuteNonQueryAsync() != 1) throw new InvalidOperationException("This company is not inactive.");
    }

    private static FinancialAccount Read(SqliteDataReader r) => new()
    {
        Id = r.GetInt32(0), Name = r.GetString(1), Address = r.GetString(2), LogoPath = r.GetString(3),
        ProfilePicturePath = r.GetString(4), CurrencyCode = r.GetString(5), IsActive = r.GetInt32(6) != 0, IsDefault = r.GetInt32(7) != 0
    };
}
