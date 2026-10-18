using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

SQLitePCL.Batteries_V2.Init();
var scratch = Directory.CreateTempSubdirectory("fintracker-account-test-");
FileSystem.AppDataDirectory = scratch.FullName;
try
{
    await using (var db = new SqliteConnection(Constants.DatabasePath))
    {
        await db.OpenAsync();
        var seed = db.CreateCommand();
        seed.CommandText = @"
CREATE TABLE Category(Id INTEGER PRIMARY KEY,Name TEXT NOT NULL UNIQUE,IconKey TEXT NOT NULL,Budgeted REAL NOT NULL DEFAULT 0);
INSERT INTO Category(Id,Name,IconKey,Budgeted) VALUES(77,'Custom category','default_shop',42);
CREATE TABLE Shop(Id INTEGER PRIMARY KEY,Name TEXT NOT NULL UNIQUE,Category TEXT,Category_Id INTEGER);
INSERT INTO Shop(Id,Name,Category,Category_Id) VALUES(3,'Legacy Shop','DefaultShop',77);
CREATE TABLE Payment_mode(Id INTEGER PRIMARY KEY,Name TEXT NOT NULL UNIQUE);
INSERT INTO Payment_mode(Id,Name) VALUES(5,'Cash');
CREATE TABLE Bill_transcation(Id INTEGER PRIMARY KEY,Bill_date TEXT,Shop_Id INTEGER,Payment_Mode_Id INTEGER,Bill_value REAL,Note TEXT,
FOREIGN KEY(Shop_Id) REFERENCES Shop(Id),FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));
INSERT INTO Bill_transcation(Id,Bill_date,Shop_Id,Payment_Mode_Id,Bill_value) VALUES(9,'2026-10-01',3,5,10);
CREATE TABLE income_transaction(Id INTEGER PRIMARY KEY,Transaction_date TEXT,Shop_Id INTEGER,Payment_Mode_Id INTEGER,Amount REAL,Note TEXT,
FOREIGN KEY(Shop_Id) REFERENCES Shop(Id),FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));
INSERT INTO income_transaction(Id,Transaction_date,Shop_Id,Payment_Mode_Id,Amount) VALUES(11,'2026-10-02',3,5,20);
CREATE TABLE MonthlyBudgetIncome(Year INTEGER NOT NULL,Month INTEGER NOT NULL,Income REAL NOT NULL,PRIMARY KEY(Year,Month));
INSERT INTO MonthlyBudgetIncome VALUES(2026,10,100);
CREATE TABLE SetUpKeyValue(Id INTEGER PRIMARY KEY,""Key"" TEXT NOT NULL UNIQUE,Value TEXT NOT NULL);
INSERT INTO SetUpKeyValue VALUES(2,'Monthly Budget','2000');
CREATE TABLE ETL_TEXT_MAP(ID INTEGER PRIMARY KEY,LOOKUPTEXT TEXT,SHOP_ID INTEGER,FOREIGN KEY(SHOP_ID) REFERENCES Shop(Id));
INSERT INTO ETL_TEXT_MAP VALUES(12,'A',3);
CREATE TABLE Recurring_Bill(Id INTEGER PRIMARY KEY,Shop_Id INTEGER,Payment_Mode_Id INTEGER,Amount REAL,Description TEXT,Start_Month TEXT,End_Month TEXT,Is_Active INTEGER,Created_At TEXT,Updated_At TEXT,
FOREIGN KEY(Shop_Id) REFERENCES Shop(Id),FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));
INSERT INTO Recurring_Bill VALUES(17,3,5,1,'test','2026-10',NULL,1,'2026-10-01','2026-10-01');";
        await seed.ExecuteNonQueryAsync();
    }

    var currency = new TestCurrency();
    var context = new FinancialAccountContext(currency);
    var operations = new CriticalOperationState();
    var accounts = new FinancialAccountRepository(context, operations, currency);
    await accounts.InitializeAsync();
    var initial = (await accounts.ListAsync()).Single();
    Assert(initial.Name == "scott_tiger" && initial.CurrencyCode == "EUR" && initial.IsDefault, "Initial account");
    context.Select(initial);
    var categories = new CategoryRepository(NullLogger<CategoryRepository>.Instance, operations, context);
    var modes = new PaymentModeRepository(NullLogger<PaymentModeRepository>.Instance, operations, context);
    var shops = new ShopRepository(NullLogger<ShopRepository>.Instance, operations, context);
    Assert((await categories.ListAsync()).Count == 39, "38 seeds plus custom legacy category");
    Assert((await modes.ListAsync()).Select(x => x.Name).ToHashSet().SetEquals(["Cash", "ETL_INCOME", "Debit Card"]), "Initial payment modes");
    Assert((await shops.ListAsync()).Single().Name == "Legacy Shop", "Legacy shop preserved");
    await using (var db = new SqliteConnection(Constants.DatabasePath))
    {
        await db.OpenAsync();
        foreach (var table in new[] { "Category", "Shop", "Payment_mode", "Bill_transcation", "income_transaction", "MonthlyBudgetIncome", "SetUpKeyValue", "ETL_TEXT_MAP", "Recurring_Bill" })
        {
            var command = db.CreateCommand();
            command.CommandText = $"SELECT COUNT(*) FROM {table} WHERE FinancialAccountId=@account";
            command.Parameters.AddWithValue("@account", initial.Id);
            Assert(Convert.ToInt32(await command.ExecuteScalarAsync()) > 0, $"{table} migrated to initial account");
        }
    }
    var copy = await accounts.CreateAsync(new FinancialAccount { Name = "Copied", Address = "X", CurrencyCode = "USD" }, true);
    context.Select(copy);
    Assert((await categories.ListAsync()).Count == 39 && (await categories.ListAsync()).All(x => x.Budgeted == 0), "Copied categories and zero budgets");
    Assert((await modes.ListAsync()).Count == 0 && (await shops.ListAsync()).Count == 0, "New company starts without payment modes or shops");
    Assert(await categories.GetAsync(77) is null && await shops.GetAsync(3) is null && await modes.GetAsync(5) is null,
        "Records from another company cannot be opened by ID");
    var cash = new PaymentMode { Name = "Cash" };
    await modes.SaveItemAsync(cash);
    var copiedCategory = (await categories.ListAsync()).First(x => x.Name == "Custom category");
    await shops.SaveItemAsync(new Shop { Name = "Legacy Shop", CategoryId = copiedCategory.Id });
    Assert((await modes.ListAsync()).Single().Name == "Cash" && (await shops.ListAsync()).Single().Name == "Legacy Shop",
        "Same catalog names may exist in different companies");
    var fresh = await accounts.CreateAsync(new FinancialAccount { Name = "Built-in", Address = "Y", CurrencyCode = "JPY" }, false);
    context.Select(fresh);
    Assert((await categories.ListAsync()).Count == 38, "Declined copy receives 38 built-in categories");
    Assert((await modes.ListAsync()).Count == 0, "Declined copy does not create automatic payment modes");
    var groceries = (await categories.ListAsync()).Single(x => x.Name == "Groceries");
    Assert(await categories.DeleteAsync(groceries), "An unused built-in category can be removed");
    var reopenedCategories = new CategoryRepository(NullLogger<CategoryRepository>.Instance, operations, context);
    Assert((await reopenedCategories.ListAsync()).Count == 37, "Deleted categories do not reappear on reopen");
    await accounts.InitializeAsync();
    Assert((await accounts.ListAsync()).Count == 3, "Migration and seed are idempotent");
    var newInstall = scratch.CreateSubdirectory("fresh-install");
    FileSystem.AppDataDirectory = newInstall.FullName;
    var freshContext = new FinancialAccountContext(currency);
    var freshAccounts = new FinancialAccountRepository(freshContext, operations, currency);
    await freshAccounts.InitializeAsync();
    var defaultCompany = (await freshAccounts.ListAsync()).Single();
    freshContext.Select(defaultCompany);
    var freshCategories = new CategoryRepository(NullLogger<CategoryRepository>.Instance, operations, freshContext);
    var freshModes = new PaymentModeRepository(NullLogger<PaymentModeRepository>.Instance, operations, freshContext);
    Assert((await freshCategories.ListAsync()).Count == 38 && (await freshModes.ListAsync()).Count == 2,
        "Fresh installation creates one company, 38 categories and two payment modes");
    Console.WriteLine("Financial account migration and isolation checks passed.");
}
finally
{
    SqliteConnection.ClearAllPools();
    var resolved = Path.GetFullPath(scratch.FullName);
    if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) scratch.Delete(true);
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
    Console.WriteLine("PASS: " + message);
}

public static class FileSystem { public static string AppDataDirectory { get; set; } = string.Empty; }
public sealed class TestCurrency : ICurrencyService
{
    public IReadOnlyList<DisplayCurrency> AvailableCurrencies { get; } = [
        new("EUR", "Euro", "€", "pt-PT", false, true), new("USD", "Dollar", "$", "en-US", true, false),
        new("JPY", "Yen", "¥", "ja-JP", true, false)];
    public DisplayCurrency CurrentCurrency { get; private set; } = new("EUR", "Euro", "€", "pt-PT", false, true);
    public event EventHandler? CurrencyChanged;
    public void LoadSavedPreference() => ApplyAccountCurrency("EUR");
    public void ApplyCurrency(string currencyCode) => ApplyAccountCurrency(currencyCode);
    public void ApplyAccountCurrency(string currencyCode)
    {
        CurrentCurrency = new DisplayCurrency(currencyCode, currencyCode, currencyCode, "en-US", true, false);
        CurrencyChanged?.Invoke(this, EventArgs.Empty);
    }
    public void ClearAccountCurrency() => LoadSavedPreference();
}
