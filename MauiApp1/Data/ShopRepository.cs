using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MauiApp1.Data;

public class ShopRepository
{
    private bool _initialized;
    private readonly ILogger<ShopRepository> _logger;
    private readonly ICriticalOperationState _criticalOperations;
    public ShopRepository(ILogger<ShopRepository> logger, ICriticalOperationState criticalOperations) { _logger = logger; _criticalOperations = criticalOperations; }

    private async Task Init()
    {
        if (_initialized) return;
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        try
        {
            var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE IF NOT EXISTS Shop (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name VARCHAR(225) NOT NULL COLLATE NOCASE UNIQUE);";
            await command.ExecuteNonQueryAsync();
            await EnsureCategoryColumnAsync(connection);
            await CategoryRepository.EnsureSchemaAndSeedAsync(connection);
            await EnsureCategoryIdColumnAndMigrationAsync(connection);
            _initialized = true;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error creating Shop table"); throw; }
    }

    public async Task<List<Shop>> ListAsync()
    {
        await Init();
        await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "SELECT s.Id, s.Name, s.Category, s.Category_Id, c.Name, c.IconKey FROM Shop s LEFT JOIN Category c ON c.Id=s.Category_Id ORDER BY s.Name";
        var result = new List<Shop>(); await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) result.Add(Read(r));
        return result;
    }

    public async Task<List<Shop>> SearchByPrefixAsync(string searchText, int limit = 8, CancellationToken cancellationToken = default)
    {
        var prefix = searchText.Trim();
        if (prefix.Length == 0) return [];

        await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = @"SELECT s.Id, s.Name, s.Category, s.Category_Id, c.Name, c.IconKey
FROM Shop s
LEFT JOIN Category c ON c.Id=s.Category_Id
WHERE s.Name LIKE @prefix ESCAPE '\' COLLATE NOCASE
ORDER BY s.Name COLLATE NOCASE
LIMIT @limit";
        command.Parameters.AddWithValue("@prefix", EscapeLikePrefix(prefix) + "%");
        command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 20));
        var result = new List<Shop>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) result.Add(Read(reader));
        return result;
    }

    public async Task<Shop?> GetAsync(int id)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "SELECT s.Id, s.Name, s.Category, s.Category_Id, c.Name, c.IconKey FROM Shop s LEFT JOIN Category c ON c.Id=s.Category_Id WHERE s.Id=@id"; cmd.Parameters.AddWithValue("@id", id);
        await using var r = await cmd.ExecuteReaderAsync();
        return await r.ReadAsync() ? Read(r) : null;
    }

    public async Task<bool> ExistsNameAsync(string name, int exceptId = 0)
    {
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "SELECT COUNT(*) FROM Shop WHERE Name=@name AND Id<>@id";
        cmd.Parameters.AddWithValue("@name", name.Trim()); cmd.Parameters.AddWithValue("@id", exceptId);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> SaveItemAsync(Shop item)
    {
        using var operation = _criticalOperations.Begin();
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var categoryId = item.CategoryId > 0 ? item.CategoryId : await GetDefaultCategoryIdAsync(c);
        var legacyCategory = ShopCategoryCatalog.FromIconKey(item.CategoryIconKey).ToString();
        var cmd = c.CreateCommand(); cmd.Parameters.AddWithValue("@name", item.Name.Trim()); cmd.Parameters.AddWithValue("@category", legacyCategory); cmd.Parameters.AddWithValue("@categoryId", categoryId);
        if (item.Id == 0) { cmd.CommandText = "INSERT INTO Shop(Name, Category, Category_Id) VALUES(@name, @category, @categoryId); SELECT last_insert_rowid();"; item.Id = Convert.ToInt32(await cmd.ExecuteScalarAsync()); }
        else { cmd.CommandText = "UPDATE Shop SET Name=@name, Category=@category, Category_Id=@categoryId WHERE Id=@id"; cmd.Parameters.AddWithValue("@id", item.Id); await cmd.ExecuteNonQueryAsync(); }
        item.CategoryId = categoryId;
        return item.Id;
    }

    internal static async Task EnsureCategoryColumnAsync(SqliteConnection connection)
    {
        var check = connection.CreateCommand(); check.CommandText = "PRAGMA table_info(Shop)";
        await using var reader = await check.ExecuteReaderAsync();
        while (await reader.ReadAsync()) if (string.Equals(reader.GetString(1), "Category", StringComparison.OrdinalIgnoreCase)) return;
        await reader.DisposeAsync();
        var alter = connection.CreateCommand(); alter.CommandText = "ALTER TABLE Shop ADD COLUMN Category TEXT"; await alter.ExecuteNonQueryAsync();
    }

    internal static async Task EnsureCategoryIdColumnAndMigrationAsync(SqliteConnection connection)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var check = connection.CreateCommand(); check.CommandText = "PRAGMA table_info(Shop)";
        await using (var reader = await check.ExecuteReaderAsync()) while (await reader.ReadAsync()) columns.Add(reader.GetString(1));
        if (!columns.Contains("Category_Id"))
        {
            var alter = connection.CreateCommand(); alter.CommandText = "ALTER TABLE Shop ADD COLUMN Category_Id INTEGER"; await alter.ExecuteNonQueryAsync();
        }
        var index = connection.CreateCommand(); index.CommandText = "CREATE INDEX IF NOT EXISTS IX_Shop_Category_Id ON Shop(Category_Id)"; await index.ExecuteNonQueryAsync();
        var defaultCommand = connection.CreateCommand(); defaultCommand.CommandText = "SELECT Id FROM Category WHERE Name='Default Shop' COLLATE NOCASE LIMIT 1";
        var defaultId = Convert.ToInt32(await defaultCommand.ExecuteScalarAsync());
        var shops = connection.CreateCommand(); shops.CommandText = "SELECT Id, Category FROM Shop WHERE Category_Id IS NULL OR Category_Id=0";
        var pending = new List<(int Id, string? Legacy)>(); await using (var reader = await shops.ExecuteReaderAsync()) while (await reader.ReadAsync()) pending.Add((reader.GetInt32(0), reader.IsDBNull(1) ? null : reader.GetString(1)));
        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        { ["DefaultShop"]="Default Shop", ["ShoppingCart"]="Groceries", ["Travel"]="Travel", ["Internet"]="Internet", ["Fuel"]="Fuel", ["Gas"]="Gas", ["Telephone"]="Telephone", ["Stationery"]="Stationery", ["Electricity"]="Electricity", ["Bank"]="Bank / Finance", ["Home"]="Home", ["OnlineShopping"]="Online Shopping", ["Restaurant"]="Restaurant / Food", ["Medical"]="Medical / Health", ["Receipt"]="Receipt / Other", ["Transport"]="Transport" };
        foreach (var shop in pending)
        {
            var name = shop.Legacy is not null && mappings.TryGetValue(shop.Legacy.Trim(), out var mapped) ? mapped : "Default Shop";
            var category = connection.CreateCommand(); category.CommandText = "SELECT Id FROM Category WHERE Name=@name COLLATE NOCASE LIMIT 1"; category.Parameters.AddWithValue("@name", name);
            var mappedId = Convert.ToInt32(await category.ExecuteScalarAsync());
            var categoryId = mappedId == 0 ? defaultId : mappedId;
            var update = connection.CreateCommand(); update.CommandText = "UPDATE Shop SET Category_Id=@categoryId WHERE Id=@id"; update.Parameters.AddWithValue("@categoryId", categoryId); update.Parameters.AddWithValue("@id", shop.Id); await update.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> GetDefaultCategoryIdAsync(SqliteConnection connection)
    { var command = connection.CreateCommand(); command.CommandText = "SELECT Id FROM Category WHERE Name='Default Shop' COLLATE NOCASE LIMIT 1"; return Convert.ToInt32(await command.ExecuteScalarAsync()); }
    private static Shop Read(SqliteDataReader reader) => new() { Id = reader.GetInt32(0), Name = reader.GetString(1), Category = ShopCategoryCatalog.Normalize(reader.IsDBNull(2) ? null : reader.GetString(2)), CategoryId = reader.IsDBNull(3) ? 0 : reader.GetInt32(3), CategoryName = reader.IsDBNull(4) ? "Default Shop" : reader.GetString(4), CategoryIconKey = reader.IsDBNull(5) ? "default_shop" : reader.GetString(5) };

    private static string EscapeLikePrefix(string value) => value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    public async Task<int> DeleteItemAsync(Shop item)
    {
        using var operation = _criticalOperations.Begin();
        await Init(); await using var c = new SqliteConnection(Constants.DatabasePath); await c.OpenAsync();
        var cmd = c.CreateCommand(); cmd.CommandText = "DELETE FROM Shop WHERE Id=@id"; cmd.Parameters.AddWithValue("@id", item.Id); return await cmd.ExecuteNonQueryAsync();
    }
}
