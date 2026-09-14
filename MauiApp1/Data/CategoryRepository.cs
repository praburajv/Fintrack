using MauiApp1.Models;
using MauiApp1.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;

namespace MauiApp1.Data;

public sealed class CategoryRepository
{
    private bool _initialized;
    private readonly ILogger<CategoryRepository> _logger;
    private readonly ICriticalOperationState _criticalOperations;

    private static readonly (string Name, string IconKey)[] Seeds =
    [
        ("Default Shop", "default_shop"), ("Groceries", "shopping_cart"), ("Travel", "travel"),
        ("Internet", "internet"), ("Fuel", "fuel"), ("Gas", "gas"), ("Telephone", "telephone"),
        ("Stationery", "stationery"), ("Electricity", "electricity"), ("Bank / Finance", "bank"),
        ("Home", "home"), ("Online Shopping", "online_shopping"), ("Restaurant / Food", "restaurant"),
        ("Medical / Health", "medical"), ("Receipt / Other", "receipt"), ("Transport", "transport")
        ,("Rent / Mortgage", "rent_mortgage"), ("Water", "water"), ("TV / Streaming", "tv_streaming"),
        ("Vehicle", "vehicle"), ("Car Workshop", "car_workshop"), ("Bike Workshop", "bike_workshop"),
        ("Pharmacy", "pharmacy"), ("Insurance", "insurance"), ("Clothing", "clothing"),
        ("Personal Care", "personal_care"), ("Barber / Hairdresser", "barber"), ("Gym / Fitness", "gym"),
        ("Education", "education"), ("Children", "children"), ("Pets", "pets"),
        ("Entertainment", "entertainment"), ("Subscriptions", "subscriptions"), ("Gifts", "gifts"),
        ("Taxes / Government", "taxes_government"), ("Home Maintenance", "home_maintenance"),
        ("Electronics", "electronics")
    ];

    public CategoryRepository(ILogger<CategoryRepository> logger, ICriticalOperationState criticalOperations)
    { _logger = logger; _criticalOperations = criticalOperations; }

    private async Task Init()
    {
        if (_initialized) return;
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync();
        try { await EnsureSchemaAndSeedAsync(connection); _initialized = true; }
        catch (Exception ex) { _logger.LogError(ex, "Error initializing Category table"); throw; }
    }

    internal static async Task EnsureSchemaAndSeedAsync(SqliteConnection connection)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tableCheck = connection.CreateCommand(); tableCheck.CommandText = "PRAGMA table_info(Category)";
        await using (var reader = await tableCheck.ExecuteReaderAsync())
            while (await reader.ReadAsync()) columns.Add(reader.GetString(1));

        if (columns.Count > 0 && (!columns.Contains("Name") || !columns.Contains("IconKey")))
        {
            // Older development builds created a different Category table. Rebuild it
            // with the normalized schema and migrate recognizable legacy values.
            var legacyColumn = columns.Contains("Category") ? "Category" : columns.Contains("CategoryName") ? "CategoryName" : null;
            var rename = connection.CreateCommand(); rename.CommandText = "ALTER TABLE Category RENAME TO Category_Legacy"; await rename.ExecuteNonQueryAsync();
            var createRepaired = connection.CreateCommand(); createRepaired.CommandText = "CREATE TABLE Category (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE UNIQUE, IconKey TEXT NOT NULL)"; await createRepaired.ExecuteNonQueryAsync();
            if (columns.Contains("Id") && legacyColumn is not null)
            {
                var legacy = connection.CreateCommand(); legacy.CommandText = $"SELECT Id, {legacyColumn} FROM Category_Legacy";
                var rows = new List<(int Id, string? Value)>(); await using var legacyReader = await legacy.ExecuteReaderAsync();
                while (await legacyReader.ReadAsync()) rows.Add((legacyReader.GetInt32(0), legacyReader.IsDBNull(1) ? null : legacyReader.GetString(1)));
                foreach (var row in rows)
                {
                    var name = LegacyName(row.Value); var icon = IconKeyFor(row.Value);
                    var insert = connection.CreateCommand(); insert.CommandText = "INSERT OR IGNORE INTO Category(Id,Name,IconKey) VALUES(@id,@name,@icon)"; insert.Parameters.AddWithValue("@id", row.Id); insert.Parameters.AddWithValue("@name", name); insert.Parameters.AddWithValue("@icon", icon); await insert.ExecuteNonQueryAsync();
                }
            }
        }
        else
        {
            var create = connection.CreateCommand(); create.CommandText = "CREATE TABLE IF NOT EXISTS Category (Id INTEGER PRIMARY KEY AUTOINCREMENT, Name TEXT NOT NULL COLLATE NOCASE UNIQUE, IconKey TEXT NOT NULL);"; await create.ExecuteNonQueryAsync();
        }
        foreach (var seed in Seeds)
        {
            var insert = connection.CreateCommand();
            insert.CommandText = "INSERT OR IGNORE INTO Category(Name, IconKey) VALUES(@name, @icon);";
            insert.Parameters.AddWithValue("@name", seed.Name); insert.Parameters.AddWithValue("@icon", seed.IconKey);
            await insert.ExecuteNonQueryAsync();
        }
    }

    private static string LegacyName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Default Shop";
        var normalized = ShopCategoryCatalog.Normalize(value).ToString();
        return normalized switch
        {
            "DefaultShop" => "Default Shop", "ShoppingCart" => "Groceries", "Bank" => "Bank / Finance",
            "OnlineShopping" => "Online Shopping", "Restaurant" => "Restaurant / Food", "Medical" => "Medical / Health",
            "Receipt" => "Receipt / Other", _ => Seeds.FirstOrDefault(x => x.Name.Equals(value.Trim(), StringComparison.OrdinalIgnoreCase)).Name ?? "Default Shop"
        };
    }

    private static string IconKeyFor(string? value)
    {
        var file = ShopCategoryCatalog.IconFor(ShopCategoryCatalog.Normalize(value));
        return file.StartsWith("category_", StringComparison.OrdinalIgnoreCase) ? file[9..^4] : "default_shop";
    }

    public async Task<List<Category>> ListAsync()
    {
        await Init(); await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText = "SELECT Id, Name, IconKey FROM Category ORDER BY Name COLLATE NOCASE";
        var result = new List<Category>(); await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(Read(reader));
        return result;
    }

    public async Task<Category?> GetAsync(int id)
    {
        await Init(); await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText = "SELECT Id, Name, IconKey FROM Category WHERE Id=@id"; command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(); return await reader.ReadAsync() ? Read(reader) : null;
    }

    public async Task<bool> ExistsByNameAsync(string name, int exceptId = 0)
    {
        await Init(); await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM Category WHERE Name=@name COLLATE NOCASE AND Id<>@id";
        command.Parameters.AddWithValue("@name", name.Trim()); command.Parameters.AddWithValue("@id", exceptId);
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    public async Task<int> InsertAsync(Category category)
    {
        using var operation = _criticalOperations.Begin(); await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText = "INSERT INTO Category(Name, IconKey) VALUES(@name,@icon); SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("@name", category.Name.Trim()); command.Parameters.AddWithValue("@icon", CategoryIconCatalog.Find(category.IconKey).IconKey);
        category.Id = Convert.ToInt32(await command.ExecuteScalarAsync()); return category.Id;
    }

    public async Task UpdateAsync(Category category)
    {
        using var operation = _criticalOperations.Begin(); await Init();
        await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText = "UPDATE Category SET Name=@name, IconKey=@icon WHERE Id=@id";
        command.Parameters.AddWithValue("@name", category.Name.Trim()); command.Parameters.AddWithValue("@icon", CategoryIconCatalog.Find(category.IconKey).IconKey); command.Parameters.AddWithValue("@id", category.Id);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<int> GetShopUsageCountAsync(int categoryId)
    {
        await Init(); await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText = "SELECT COUNT(*) FROM Shop WHERE Category_Id=@id"; command.Parameters.AddWithValue("@id", categoryId);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public async Task<bool> DeleteAsync(Category category)
    {
        if (category.Name.Equals("Default Shop", StringComparison.OrdinalIgnoreCase)) return false;
        if (await GetShopUsageCountAsync(category.Id) > 0) return false;
        using var operation = _criticalOperations.Begin(); await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
        var command = connection.CreateCommand(); command.CommandText = "DELETE FROM Category WHERE Id=@id"; command.Parameters.AddWithValue("@id", category.Id);
        return await command.ExecuteNonQueryAsync() > 0;
    }

    public async Task<Category> GetDefaultCategoryAsync() => (await ListAsync()).First(x => x.Name.Equals("Default Shop", StringComparison.OrdinalIgnoreCase));
    private static Category Read(SqliteDataReader reader) => new() { Id = reader.GetInt32(0), Name = reader.GetString(1), IconKey = reader.GetString(2) };
}
