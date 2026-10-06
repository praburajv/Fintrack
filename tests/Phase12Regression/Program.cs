using System.Globalization;
using System.Text;
using System.Xml.Linq;
using System.IO.Compression;
using System.Text.Json.Nodes;
using MauiApp1.Models;
using MauiApp1.PageModels;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

SQLitePCL.Batteries_V2.Init();
var testDirectory = Directory.CreateTempSubdirectory("expense-phase12-").FullName;
Constants.DatabasePath = $"Data Source={Path.Combine(testDirectory, "test.db")}";
FileSystem.CacheDirectory = Path.Combine(testDirectory, "cache");
FileSystem.AppDataDirectory = testDirectory;
Directory.CreateDirectory(FileSystem.CacheDirectory);
var critical = new CriticalOperationState();
var bills = new BillTransactionRepository(NullLogger<BillTransactionRepository>.Instance, critical);
var income = new IncomeTransactionRepository(critical);
var shops = new ShopRepository(NullLogger<ShopRepository>.Instance, critical);
var deletion = new MonthlyFinancialDeletionService(bills, income, critical);
var passed = 0;

await Test("Additive legacy schema upgrade retains records and leaves provenance NULL", async () =>
{
    await Sql(@"CREATE TABLE Shop (Id INTEGER PRIMARY KEY AUTOINCREMENT,Name TEXT NOT NULL COLLATE NOCASE UNIQUE);
CREATE TABLE Payment_mode (Id INTEGER PRIMARY KEY AUTOINCREMENT,Name TEXT NOT NULL COLLATE NOCASE UNIQUE);
CREATE TABLE Bill_transcation (Id INTEGER PRIMARY KEY AUTOINCREMENT,Bill_date TEXT,Shop_Id INTEGER,Payment_Mode_Id INTEGER,Bill_value NUMERIC,Note TEXT);
CREATE TABLE income_transaction (Id INTEGER PRIMARY KEY AUTOINCREMENT,Transaction_date TEXT,Shop_Id INTEGER,Payment_Mode_Id INTEGER,Amount NUMERIC,Note TEXT);
INSERT INTO Shop(Id,Name) VALUES(1,'ETL_DATA_VAULT');
INSERT INTO Payment_mode(Id,Name) VALUES(1,'Income'),(2,'Debit Card');
INSERT INTO Bill_transcation VALUES(100,'2025-12-02T00:00:00.0000000',1,2,30,'older debit');
INSERT INTO income_transaction VALUES(100,'2025-12-02T00:00:00.0000000',1,1,40,'older credit');");
    var plan = await deletion.GetPlanAsync(2025, 12);
    Check(plan.Expenses.Single().Id == 100 && plan.UntrackedIncome.Single().Id == 100, "Legacy rows retained");
    Check(!plan.ImportedIncome.Any(), "Legacy credits cannot be inferred from shop/payment/category");
    await using var connection = new SqliteConnection(Constants.DatabasePath);
    await connection.OpenAsync();
    await EtlImportSchema.EnsureAsync(connection);
    await EtlImportSchema.EnsureAsync(connection);
    Check(await Count("income_transaction") == 1, "Migration is idempotent");
});

await Test("Debit-only deletion keeps existing whole-month expense scope", async () =>
{
    await AddBill(1, 11, "debit-only"); await AddBill(1, 12, null);
    var result = await deletion.DeleteAsync(await deletion.GetPlanAsync(2026, 1), []);
    Check(result.ExpenseCount == 2 && result.IncomeCount == 0, "Both imported and normal monthly expenses removed");
});

await Test("ETL payment mode is created once, reused case-insensitively and preserves existing modes", async () =>
{
    var modes = new PaymentModeRepository(NullLogger<PaymentModeRepository>.Instance, critical);
    Check(!(await modes.ListAsync()).Any(mode => mode.Name.Equals("Bank Trnsfer", StringComparison.OrdinalIgnoreCase)), "Mode initially missing");
    var id = await modes.EnsureIncomeModeAsync();
    Check(id > 0 && (await modes.GetAsync(id))?.Name == "Bank Trnsfer", "Requested mode created");
    var repeatedIds = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => modes.EnsureIncomeModeAsync())));
    Check(repeatedIds.All(value => value == id), "Repeated/concurrent ensures reuse same ID");
    await modes.SaveItemAsync(new PaymentMode { Id = id, Name = "bank trnsfer" });
    Check(await modes.EnsureIncomeModeAsync() == id, "Case-insensitive reuse");
    var all = await modes.ListAsync();
    Check(all.Count(mode => mode.Name.Equals("Bank Trnsfer", StringComparison.OrdinalIgnoreCase)) == 1, "No duplicate mode");
    Check(all.Single(mode => mode.Id == 1).Name == "Income" && all.Single(mode => mode.Id == 2).Name == "Debit Card", "Existing Income/Debit modes unchanged");
    Check(!critical.IsBusy, "Critical-operation leases released");
    var existingDatabase = Constants.DatabasePath;
    try
    {
        Constants.DatabasePath = $"Data Source={Path.Combine(testDirectory, "empty-payment-modes.db")}";
        var fresh = new PaymentModeRepository(NullLogger<PaymentModeRepository>.Instance, critical);
        var freshId = await fresh.EnsureIncomeModeAsync();
        var freshModes = await fresh.ListAsync();
        Check(freshModes.Count == 1 && freshModes[0].Id == freshId && freshModes[0].Name == "Bank Trnsfer", "Fresh database works without an Income payment mode");
    }
    finally { Constants.DatabasePath = existingDatabase; }
});

await Test("ETL Credit uses the ensured Bank Trnsfer ID and remains separate from Debit", async () =>
{
    var modes = new PaymentModeRepository(NullLogger<PaymentModeRepository>.Instance, critical);
    var id = await modes.EnsureIncomeModeAsync();
    var credit = new IncomeTransaction { TransactionDate = new(2025,11,2), ShopId = 1, PaymentModeId = id, Amount = 25, Note = "bank-mode regression" };
    await income.SaveAsync(credit, "bank-mode-regression");
    Check((await income.GetAsync(credit.Id))?.PaymentModeId == id, "Credit uses ensured mode in income_transaction");
    Check((await bills.ListAsync(new(2025,11,1),new(2025,12,1))).Count == 0, "No Expense record created");
    var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "MauiApp1", "Services", "EtlPdfImportService.cs"));
    Check(source.Contains("var incomeModeId = await paymentModes.EnsureIncomeModeAsync();") && source.Contains("PaymentModeId = incomeModeId"), "Importer wired to helper and Credit save");
    Check(!source.Contains("The payment mode Income was not found"), "Old missing-Income prerequisite removed");
    Check(source.Contains("PaymentModeId = mode.Id, BillValue = transaction.DebitAmount"), "Debit routing unchanged");
    var etlPage = XDocument.Load(Path.Combine(FindRepositoryRoot(), "MauiApp1", "Pages", "EtlDataPage.xaml"));
    Check(etlPage.Descendants().Single(node => node.Name.LocalName == "ProcessingOverlay").Attribute("Grid.RowSpan")?.Value == "2", "ETL screen lock covers bottom controls");
});

await Test("Credit-only month is deletable; manual Income with identical holding-shop/mode remains", async () =>
{
    var imported = await AddIncome(2, 120, "credit-only");
    var manual = await AddIncome(2, 120, null);
    var counts = await deletion.GetIncomeCountsAsync(2026);
    Check(counts[2] == (1, 1), "Imported and untracked counts separate");
    Check(new ExpenseMonthMaintenanceItem { ImportedIncomeCount = 1 }.HasDeletableData, "Credit-only Delete enabled");
    var result = await deletion.DeleteAsync(await deletion.GetPlanAsync(2026, 2), []);
    Check(result.IncomeCount == 1 && result.ProtectedIncomeCount == 1, "Only proven Credit removed");
    Check(await income.GetAsync(imported.Id) is null && await income.GetAsync(manual.Id) is not null, "Manual Income retained");
});

await Test("Mixed Debit/Credit deletion is month-bounded and leaves no linked rows", async () =>
{
    await AddBill(3, 33, "mixed"); var credit = await AddIncome(3, 333, "mixed");
    var manual = await AddIncome(3, 31, null); var nextMonth = await AddIncome(4, 444, "mixed");
    var result = await deletion.DeleteAsync(await deletion.GetPlanAsync(2026, 3), []);
    Check(result.ExpenseCount == 1 && result.IncomeCount == 1, "Both sides removed");
    Check(await income.GetAsync(credit.Id) is null, "No linked Credit left behind");
    Check(await income.GetAsync(manual.Id) is not null && await income.GetAsync(nextMonth.Id) is not null, "Manual/other month protected");
});

await Test("Selected ETL import leaves other imports and normal expenses/Income intact", async () =>
{
    await AddBill(5, 51, "batch-A"); await AddIncome(5, 510, "batch-A");
    var otherBill = await AddBill(5, 52, "batch-B"); var otherCredit = await AddIncome(5, 520, "batch-B");
    var normalBill = await AddBill(5, 53, null); var normalIncome = await AddIncome(5, 530, null);
    var result = await deletion.DeleteAsync(await deletion.GetPlanAsync(2026, 5, "batch-A"), []);
    Check(result.ExpenseCount == 1 && result.IncomeCount == 1, "Only batch A removed");
    Check(await bills.GetAsync(otherBill.Id) is not null && await bills.GetAsync(normalBill.Id) is not null, "Other expenses kept");
    Check(await income.GetAsync(otherCredit.Id) is not null && await income.GetAsync(normalIncome.Id) is not null, "Other Income kept");
});

await Test("Legacy Credit review includes only individually approved IDs", async () =>
{
    var legacy = await AddIncome(6, 61, null); var manual = await AddIncome(6, 62, null);
    var plan = await deletion.GetPlanAsync(2026, 6);
    var result = await deletion.DeleteAsync(plan, [legacy.Id]);
    Check(result.IncomeCount == 1 && result.ProtectedIncomeCount == 1, "Only reviewed older Credit removed");
    Check(await income.GetAsync(manual.Id) is not null, "Unreviewed manual Income kept");
    await ExpectFailure(() => deletion.DeleteAsync(plan, [int.MaxValue]), "Unknown review ID rejected");
});

await Test("Income editing updates the same ID, retains import provenance, refreshes totals and isolates Expense", async () =>
{
    var expense = await AddBill(7, 71, null); var credit = await AddIncome(7, 700, "editable-credit");
    var countBefore = await Count("income_transaction"); var originalId = credit.Id;
    credit.Amount = 750; credit.Note = "edited ETL Credit";
    await income.SaveAsync(credit); // Existing editor uses the same save path, without re-importing.
    Check(credit.Id == originalId && await Count("income_transaction") == countBefore, "No duplicate created");
    Check((await income.GetAsync(originalId))?.Amount == 750, "Income value updated");
    Check((await income.GetMonthlyTotalsAsync(new(2026,7,1), new(2026,8,1)))[(2026,7)] == 750, "Totals recalculated");
    Check((await deletion.GetPlanAsync(2026,7,"editable-credit")).ImportedIncome.Single().Id == originalId, "Provenance survives edit");
    Check((await bills.GetAsync(expense.Id))?.BillValue == 71, "Expense unchanged");
    await ExpectFailure(() => income.SaveAsync(new IncomeTransaction { Amount = 0 }), "Income validation retained");
});

await Test("Database failure rolls back previously deleted expenses and credits", async () =>
{
    var expense = await AddBill(8, 81, "rollback"); var credit = await AddIncome(8, 810, "rollback");
    var plan = await deletion.GetPlanAsync(2026, 8);
    await Sql($"CREATE TRIGGER test_delete_failure BEFORE DELETE ON income_transaction WHEN OLD.Id={credit.Id} BEGIN SELECT RAISE(ABORT,'simulated failure'); END;");
    await ExpectFailure(() => deletion.DeleteAsync(plan, []), "Injected failure propagates");
    await Sql("DROP TRIGGER test_delete_failure;");
    Check(await bills.GetAsync(expense.Id) is not null && await income.GetAsync(credit.Id) is not null, "Both sides rolled back");
    Check(!critical.IsBusy, "Critical operation lease released on failure");
});

await Test("Concurrent edits after review abort the whole deletion", async () =>
{
    var expense = await AddBill(9, 91, "changed"); var credit = await AddIncome(9, 910, "changed");
    var plan = await deletion.GetPlanAsync(2026, 9);
    credit.Amount = 911; await income.SaveAsync(credit);
    await ExpectFailure(() => deletion.DeleteAsync(plan, []), "Stale confirmation rejected");
    Check(await bills.GetAsync(expense.Id) is not null && (await income.GetAsync(credit.Id))?.Amount == 911, "Rollback and edited value retained");
});

await Test("Shop/range drilldown loads without a date and excludes unrelated shops/months", async () =>
{
    var aldi = new Shop { Name = "Aldi" }; await shops.SaveItemAsync(aldi);
    var october = await AddBill(10, 101, null); await AddBill(11, 111, null);
    await bills.SaveItemAsync(new BillTransaction { BillDate = new(2026,10,2), ShopId = aldi.Id, PaymentModeId = 2, BillValue = 10 });
    var model = new DailyBillListPageModel(bills, income, shops, new ModalErrorHandler());
    model.ApplyQueryAttributes(new Dictionary<string, object> { ["shopId"] = "1", ["from"] = "2026-10-01", ["to"] = "2026-11-01" });
    await model.AppearingCommand.ExecuteAsync(null);
    Check(model.Items.Count == 1 && model.Items[0].Id == october.Id, "Correct shop and half-open month range");
    Check(model.DateTitle.Contains("October 2026"), "Range title, not default date");
    model.ApplyQueryAttributes(new Dictionary<string, object> { ["date"] = "2026-10-02" });
    await model.AppearingCommand.ExecuteAsync(null);
    Check(model.ShopId is null && model.RangeStart == default && model.Items.Count == 2, "Stale drilldown filters cleared");
    Check(ShopBillNavigation.Route(1, new(2026,10,1), new(2026,11,1)) == "dailybills?shopId=1&from=2026-10-01&to=2026-11-01", "Shared route preserves bounds");
});

await Test("New matching imports created during review abort instead of leaving an import partly deleted", async () =>
{
    await AddBill(12, 121, "scope-change"); await AddIncome(12, 1210, "scope-change");
    var plan = await deletion.GetPlanAsync(2026,12,"scope-change");
    await AddIncome(12, 1220, "scope-change");
    await ExpectFailure(() => deletion.DeleteAsync(plan, []), "New scoped row must require another confirmation");
    Check((await deletion.GetPlanAsync(2026,12,"scope-change")).Records.Count == 3, "All imported rows retained");
});

await Test("Selected-month backup/restore preserves Expense provenance and does not touch Income", async () =>
{
    var service = new BackupService(critical);
    var originalIncomeCount = await Count("income_transaction");
    var backup = await service.CreateSelectedMonthsBackupAsync(2026, [12]);
    await bills.DeleteBillsForMonthAsync(2026,12);
    await service.RestoreSelectedMonthsBackupAsync(backup.Path);
    var plan = await deletion.GetPlanAsync(2026,12,"scope-change");
    Check(plan.Expenses.Count() == 1 && plan.ImportedIncome.Count() == 2, "Provenance round-trips for imported expenses");
    Check(await Count("income_transaction") == originalIncomeCount, "Selected-month expense restore preserves Income");
    // Older selected-month JSON has no provenance property; it must still restore as untracked.
    using (var archive = ZipFile.Open(backup.Path, ZipArchiveMode.Update))
    {
        var entry = archive.GetEntry("selected-months.json")!;
        JsonNode data;
        using (var reader = new StreamReader(entry.Open())) data = JsonNode.Parse(await reader.ReadToEndAsync())!;
        foreach (var bill in data["Bills"]!.AsArray()) bill!.AsObject().Remove("EtlImportId");
        entry.Delete();
        using var writer = new StreamWriter(archive.CreateEntry("selected-months.json").Open());
        await writer.WriteAsync(data.ToJsonString());
    }
    await service.RestoreSelectedMonthsBackupAsync(backup.Path);
    Check(!(await deletion.GetPlanAsync(2026,12,"scope-change")).Expenses.Any(), "Older backup has no fabricated import identity");
    Check((await bills.ListAsync(new(2026,12,1),new(2027,1,1))).Count == 1, "Older selected-month backup remains compatible");
});

await Test("Full backup/restore retains imported Income provenance and edited values", async () =>
{
    var service = new BackupService(critical);
    var backup = await service.CreateBackupAsync();
    var original = (await deletion.GetPlanAsync(2026,7,"editable-credit")).ImportedIncome.Single();
    await income.DeleteAsync(original.Id);
    await service.RestoreBackupAsync(backup.Path);
    var restored = (await deletion.GetPlanAsync(2026,7,"editable-credit")).ImportedIncome.Single();
    Check(restored.Id == original.Id && restored.Amount == 750, "Full restore retains Income and import marker");
});

await Test("Older full backup restores additive provenance even when repository instances are already initialized", async () =>
{
    var service = new BackupService(critical);
    var before = await Count("income_transaction");
    await Sql("ALTER TABLE Bill_transcation DROP COLUMN EtlImportId; ALTER TABLE income_transaction DROP COLUMN EtlImportId;");
    var legacyBackup = await service.CreateBackupAsync();
    await service.RestoreBackupAsync(legacyBackup.Path);
    var plan = await deletion.GetPlanAsync(2026,7);
    Check(plan.UntrackedIncome.Any(row => row.Amount == 750) && !plan.ImportedIncome.Any(), "Existing Income retained with unknown provenance");
    Check(await Count("income_transaction") == before, "Older full restore retains all Income rows");
    var newCredit = await AddIncome(12, 1230, "after-old-restore");
    var newDebit = await AddBill(12, 123, "after-old-restore");
    Check((await deletion.GetPlanAsync(2026,12,"after-old-restore")).Records.Count == 2, "Both repositories can import after old restore");
});

await Test("December/January bounds and moving an edited Income date preserve deletion scope", async () =>
{
    var december = new IncomeTransaction { TransactionDate=new(2026,12,31),ShopId=1,PaymentModeId=1,Amount=50 };
    var january = new IncomeTransaction { TransactionDate=new(2027,1,1),ShopId=1,PaymentModeId=1,Amount=60 };
    await income.SaveAsync(december,"year-boundary"); await income.SaveAsync(january,"year-boundary");
    var result = await deletion.DeleteAsync(await deletion.GetPlanAsync(2026,12,"year-boundary"), []);
    Check(result.IncomeCount == 1 && await income.GetAsync(january.Id) is not null, "January excluded from December delete");
    january.TransactionDate = new(2027,2,1); january.ShopId = 2;
    await income.SaveAsync(january);
    Check(!(await deletion.GetPlanAsync(2027,1,"year-boundary")).Records.Any(), "Old month no longer targets edited Credit");
    Check((await deletion.GetPlanAsync(2027,2,"year-boundary")).ImportedIncome.Single().Id == january.Id, "Date/shop edit retains proof of import");
});

await Test("ASC/DESC keep shop identity, amount, color and tap targets attached", () =>
{
    ShopExpenseChartData[] data = [
        new() { ShopId = 1, ShopName = "LIDL", VisitCount = 4, TotalValue = 55 },
        new() { ShopId = 2, ShopName = "aldi", VisitCount = 2, TotalValue = 30 },
        new() { ShopId = 3, ShopName = "Continente", VisitCount = 2, TotalValue = 40 }];
    var ascending = ShopVisitOrdering.Order(data, true); var descending = ShopVisitOrdering.Order(data, false);
    Check(ascending.Select(row => row.ShopId).SequenceEqual([2,3,1]), "Visit ASC with name ties");
    Check(descending.Select(row => row.ShopId).SequenceEqual([1,2,3]), "Visit DESC with name ties");
    Check(ReferenceEquals(ascending[2], data[0]) && ascending[2].TotalValue == 55, "Sorted bars keep original objects");
    Check(ShopBillNavigation.Route(ascending[2].ShopId,new(2026,10,1),new(2026,11,1)).Contains("shopId=1"), "Sorted LIDL target correct");
    return Task.CompletedTask;
});

await Test("Category/shop ordering, deterministic date/ID ties and generated PDF agree", async () =>
{
    var groceries = new CategoryExpenseGroup { CategoryId = 1, CategoryName = "Groceries" };
    foreach (var bill in new BillTransaction[] {
        new() { Id=1,ShopName="LIDL",BillDate=new(2026,10,2),BillValue=10 },
        new() { Id=2,ShopName="Aldi",BillDate=new(2026,10,2),BillValue=20 },
        new() { Id=3,ShopName="Pingo Doce",BillDate=new(2026,10,2),BillValue=30 },
        new() { Id=4,ShopName="Continente",BillDate=new(2026,10,2),BillValue=40 },
        new() { Id=5,ShopName="ALDI",BillDate=new(2026,10,3),BillValue=50 },
        new() { Id=6,ShopName="aldi",BillDate=new(2026,10,3),BillValue=60 } }) groceries.Bills.Add(bill);
    var utilities = new CategoryExpenseGroup { CategoryId = 2, CategoryName = "Utilities" };
    utilities.Bills.Add(new() { ShopName = "Water Company", BillValue = 20 });
    utilities.Bills.Add(new() { ShopName = "EDP", BillValue = 10 });
    Check(ReportBillOrdering.Order(groceries.Bills).Select(row => row.Id).SequenceEqual([6,5,2,4,1,3]), "Case insensitive names then date/ID descending");
    var statement = new ExpenseStatementBuilder(new TestCurrency()).Build(ExpensePeriodType.Monthly,
        "October 2026", new(2026,10,1), new(2026,11,1), [groceries,utilities]);
    Check(statement.Categories.Count == 2 && statement.Categories[0].BillCount == 6, "Category hierarchy/counts retained");
    Check(statement.Categories[0].Rows.Select(row => row.Value).SequenceEqual([60m,50m,20m,40m,10m,30m]), "Statement uses shared bill order");
    var pdf = await new ExpenseStatementDocumentService().CreatePdfAsync(statement);
    var content = Encoding.Latin1.GetString(await File.ReadAllBytesAsync(pdf));
    var tokens = new[] { "(Groceries)", "(aldi)", "(ALDI)", "(Aldi)", "(Continente)", "(LIDL)", "(Pingo Doce)", "(Utilities)", "(EDP)", "(Water Company)" };
    var position = -1;
    foreach (var token in tokens) { var next = content.IndexOf(token, position + 1, StringComparison.Ordinal); Check(next > position, $"PDF row order: {token}"); position = next; }
    Check(File.Exists(pdf) && content.StartsWith("%PDF"), "Real PDF generated from ordered snapshot");
});

await Test("UI source wiring retains shared Income editor, intended charts and page overlays", () =>
{
    var root = FindRepositoryRoot();
    var source = Path.Combine(root, "MauiApp1");
    var list = XDocument.Load(Path.Combine(source,"Pages","MyMExpense.xaml"));
    Check(!list.Descendants().Any(node => node.Name.LocalName == "ShopVisitsChart"), "No empty Shop Visit container on Income Expense List");
    var incomeLayout = list.Descendants().Single(node => node.Attribute("BindableLayout.ItemsSource")?.Value == "{Binding IncomeCategories}");
    var expenseLayout = list.Descendants().Single(node => node.Attribute("BindableLayout.ItemsSource")?.Value == "{Binding Categories}");
    Check(incomeLayout.Attribute("BindableLayout.ItemTemplate")?.Value == expenseLayout.Attribute("BindableLayout.ItemTemplate")?.Value, "Income and Expense reuse the same row/category template");
    var template = list.Descendants().Single(node => node.Attribute("{http://schemas.microsoft.com/winfx/2009/xaml}Key")?.Value == "FinancialCategoryTemplate");
    Check(template.Descendants().Any(node => node.Attribute("Command")?.Value.Contains("EditBillCommand") == true), "Existing type-aware edit command reused");
    Check(template.Descendants().Any(node => node.Attribute("TextColor")?.Value == "{Binding AmountTextColor}"), "Income green style retained");
    foreach (var page in new[] { "DailyDashPage", "ExpenseReportPage", "ExpenseStatementPreviewPage" })
    {
        var document = XDocument.Load(Path.Combine(source,"Pages",page+".xaml"));
        var charts = document.Descendants().Where(node => node.Name.LocalName == "ShopVisitsChart").ToArray();
        Check(charts.Length == (page == "DailyDashPage" ? 3 : 1), $"{page} chart retained");
        Check(charts.All(chart => chart.Attribute("ShowSortControl")?.Value == "True" && chart.Attribute("OpenBillsOnSelection")?.Value == "True" && chart.Attribute("StartDate") is not null && chart.Attribute("EndExclusive") is not null && chart.Attribute("IsProcessing") is not null), $"{page} shared sort/navigation/screen-lock wiring");
        Check(document.Descendants().Any(node => node.Name.LocalName == "ProcessingOverlay"), $"{page} spinner overlay retained");
    }
    Check(XDocument.Load(Path.Combine(source,"Pages","ExpenseAnalysisPage.xaml")).Descendants().Any(node => node.Name.LocalName == "ShopVisitsChart"), "Expense Analysis Shop Visit preserved");
    var previewSource = File.ReadAllText(Path.Combine(source,"PageModels","ExpenseStatementPreviewPageModel.cs"));
    Check(previewSource.Split("var path = await EnsurePdfAsync();").Length == 3, "View and Share use the same cached PDF generation path");
    return Task.CompletedTask;
});

var modesForImport = new PaymentModeRepository(NullLogger<PaymentModeRepository>.Instance, critical);
var categoriesForImport = new CategoryRepository(NullLogger<CategoryRepository>.Instance, critical);
await categoriesForImport.InsertAsync(new Category { Name = "ETL_DT", IconKey = "default_shop" });
var importer = new EtlPdfImportService(bills, income, shops, categoriesForImport, modesForImport,
    new EtlTextMapRepository(NullLogger<EtlTextMapRepository>.Instance, critical),
    new EtlTextMappingService(), NullLogger<EtlPdfImportService>.Instance);

await Test("Real PDF import classifies only Debit/Credit, ignores balances and retains repeated rows", async () =>
{
    var path = CreateBankFixture(testDirectory, "classification", 2024, includeBalanceHeader: true);
    var result = await importer.ImportAsync(new FileResult(path));
    Check(result.Imported == 4 && result.IncomeImported == 6 && result.InvalidOrErrors == 0, $"Expected 4 Debit/6 Credit rows; got {result}");
    var credits = await income.ListAsync(new(2024,9,1), new(2024,10,1));
    var expenses = await bills.ListAsync(new(2024,9,1), new(2024,10,1));
    Check(credits.Select(row => row.Amount).Order().SequenceEqual(new decimal[] { 600,1999.99m,2197.45m,30,17,600 }.Order()), $"Only Credit column amounts reach income_transaction: {string.Join(", ", credits.Select(row => row.Amount))}");
    Check(expenses.Select(row => row.BillValue).Order().SequenceEqual(new decimal[] { 25,50,10,1234.56m }.Order()), "Debit amounts only; no Balance used as Income");
    Check(credits.Count(row => row.Amount == 600 && row.Note == "TRF DE PRABURAJ VISHVANATHAN") == 2, "Repeated legitimate source rows retained");
    Check(credits.All(row => row.TransactionDate == new DateTime(2024,9,14)), "DATA LANC used, not DATA VALOR");
    var bankMode = await modesForImport.EnsureIncomeModeAsync();
    Check(credits.All(row => row.PaymentModeId == bankMode) && expenses.All(row => row.PaymentModeId == 2), "Existing payment mode routing retained");
    Check(!credits.Any(row => row.Note.Contains("BALANCE ONLY") || row.Note.Contains("ZERO ONLY")), "Blank/zero columns skipped");
    var repeated = await importer.ImportAsync(new FileResult(path));
    Check(repeated.Imported == 0 && repeated.IncomeImported == 0 && repeated.DuplicatesSkipped == 10, $"Count-aware duplicate import unchanged: {repeated}");
    var plan = await deletion.GetPlanAsync(2024,9);
    Check(plan.ImportedIncome.Count() == 6 && plan.Expenses.Count() == 4 && !plan.UntrackedIncome.Any(), "Provenance retained on both sides");
});

await Test("Finite amount bounds also exclude balances without a SALDO header", async () =>
{
    var result = await importer.ImportAsync(new FileResult(CreateBankFixture(testDirectory, "no-balance-header", 2023, false)));
    Check(result.Imported == 4 && result.IncomeImported == 6 && result.InvalidOrErrors == 0, $"Header fallback classification: {result}");
});

await Test("Monthly View targets the flyout root with the row year/month and releases screen lock", async () =>
{
    var model = new MonthlyExpenseDataPageModel(bills, deletion, new ModalErrorHandler()) { Year = 2024 };
    await model.AppearingCommand.ExecuteAsync(null);
    var month = model.Months.Single(row => row.MonthNumber == 9);
    var navigated = false;
    Shell.Current.OnNavigate = route => { navigated = true; Check(model.IsBusy && model.ProcessingMessage.Contains("September"), "View is screen-locked while navigating"); };
    try { await model.ViewMonthCommand.ExecuteAsync(month); }
    finally { Shell.Current.OnNavigate = null; }
    Check(navigated && Shell.Current.Routes.Last() == "//myexpense?mode=FullYear&year=2024&month=9", "Absolute flyout route preserves selected period");
    Check(!model.IsBusy && model.ProcessingMessage == "", "View overlay released");
    model.IsBusy = true;
    var count = Shell.Current.Routes.Count;
    await model.ViewMonthCommand.ExecuteAsync(month);
    Check(Shell.Current.Routes.Count == count, "Busy View cannot navigate again");
});

await Test("Requested month loads and expands on Appearing, including a previously loaded page", async () =>
{
    var model = new ExpenseListPageModel(bills, income, new ModalErrorHandler(), new TestAuthentication());
    model.RequestSpecificMonth(2024,9);
    await model.AppearingCommand.ExecuteAsync(null);
    var month = model.Months.Single(row => row.MonthNumber == 9);
    Check(model.Year == 2024 && month.IsExpanded && month.IsLoaded && month.Bills.Count == 4 && month.IncomeBills.Count == 6, "Selected month loaded without fire-and-forget race");
    Check(!model.IsOverlayBusy && model.Mode == ExpenseListMode.FullYear, "State and screen lock released");
    model.RequestSpecificMonth(2023,9);
    await model.AppearingCommand.ExecuteAsync(null);
    Check(model.Year == 2023 && model.Months.Single(row => row.MonthNumber == 9).IncomeBills.Count == 6, "Reused list respects new period");
    // Exercise navigation while a previous summary refresh owns the gate.
    var refresh = model.LoadCommand.ExecuteAsync(null);
    var open = model.OpenSpecificMonthAsync(2024,9);
    await Task.WhenAll(refresh,open);
    Check(model.Year == 2024 && model.Months.Single(row => row.MonthNumber == 9).IsLoaded && !model.IsOverlayBusy, "Requested month waits for concurrent summary load");
});

await Test("Monthly Delete cancellation keeps data; confirmed deletion refreshes counts and protects manual Income", async () =>
{
    var manual = new IncomeTransaction { TransactionDate = new(2023,9,14), ShopId = 1, PaymentModeId = 1, Amount = 88, Note = "manual income" };
    await income.SaveAsync(manual);
    var model = new MonthlyExpenseDataPageModel(bills, deletion, new ModalErrorHandler()) { Year = 2023 };
    await model.AppearingCommand.ExecuteAsync(null);
    var month = model.Months.Single(row => row.MonthNumber == 9);
    Check(month.HasDeletableData && month.BillCount == 4 && month.ImportedIncomeCount == 6 && month.UntrackedIncomeCount == 1, "Both sides counted before deletion");
    await RunDeletionReview(model, month, ["whole-month", "keep-untracked", null]);
    Check((await deletion.GetPlanAsync(2023,9)).Records.Count == 11 && !model.IsBusy, "Cancel retains all records and unlocks");
    await RunDeletionReview(model, month, ["whole-month", "keep-untracked", "delete"]);
    var refreshed = model.Months.Single(row => row.MonthNumber == 9);
    Check(refreshed.BillCount == 0 && refreshed.ImportedIncomeCount == 0 && refreshed.UntrackedIncomeCount == 1, "Delete removes only approved data and refreshes row counts");
    Check(await income.GetAsync(manual.Id) is not null && (await deletion.GetPlanAsync(2024,9)).Records.Count == 10, "Manual Income and other years untouched");
    Check(!model.IsBusy && !critical.IsBusy, "All locks released after deletion");
});

await Test("Monthly action bindings reuse parent commands, equal sizes, and full-screen processing overlay", () =>
{
    var page = XDocument.Load(Path.Combine(FindRepositoryRoot(), "MauiApp1", "Pages", "MonthlyExpenseDataPage.xaml"));
    foreach (var label in new[] { "View", "Delete" })
    {
        var button = page.Descendants().Single(node => node.Name.LocalName == "Button" && node.Attribute("Text")?.Value == label
            && node.Attribute("Style")?.Value == "{StaticResource MonthlyActionButton}");
        Check(button.Attribute("Style")?.Value == "{StaticResource MonthlyActionButton}", "Both buttons reuse the same dimensions/style");
        Check(button.Attribute("Command")?.Value.Contains("RelativeSource AncestorType={x:Type pm:MonthlyExpenseDataPageModel}") == true, "Command binds to parent model, not row-item scope");
        Check(button.Attribute("CommandParameter")?.Value == "{Binding .}", "Command gets selected month item");
    }
    Check(page.Descendants().Single(node => node.Name.LocalName == "ProcessingOverlay").Attribute("Grid.RowSpan")?.Value == "3", "Overlay covers whole maintenance page");
    var overlay = page.Descendants().Single(node => node.Name.LocalName == "ProcessingOverlay");
    Check(overlay.Attribute("IsBusy")?.Value == "{Binding IsProcessing}" && overlay.Attribute("ShowProgress")?.Value == "{Binding IsDeletingData}", "Spinner/progress are separate from waiting for user input");
    Check(overlay.Attribute("Progress")?.Value == "{Binding ProgressFraction}", "Progress bar uses a 0-to-1 fraction, not a percentage");
    var reviewLayer = page.Descendants().Single(node => node.Name.LocalName == "Grid" && node.Attribute("IsVisible")?.Value == "{Binding IsAwaitingDeletionChoice}");
    Check(reviewLayer.Attribute("Grid.RowSpan")?.Value == "3" && reviewLayer.Attribute("InputTransparent")?.Value == "False", "Review layer blocks the whole page");
    Check(reviewLayer.Descendants().Any(node => node.Name.LocalName == "Image" && node.Attribute("Source")?.Value == "appicon_runtime.svg"), "Review screen retains application icon");
    Check(reviewLayer.Descendants().Any(node => node.Attribute("Command")?.Value == "{Binding CancelDeletionReviewCommand}"), "Explicit Cancel button");
    var finalDelete = reviewLayer.Descendants().Single(node => node.Name.LocalName == "Button" && node.Attribute("Text")?.Value == "Delete");
    Check(finalDelete.Parent?.Attribute("Grid.Row")?.Value == "2" && finalDelete.Attribute("CommandParameter")?.Value == "{Binding DeleteConfirmationChoice}", "Final Delete is pinned outside scrolling details with the active confirmation choice");
    Check(page.Descendants().Any(node => node.Name.LocalName == "InvertedBoolConverter"
        && node.Attribute("{http://schemas.microsoft.com/winfx/2009/xaml}Key")?.Value == "InvertedBoolConverter"), "Review visibility converter is explicitly registered in page resources");
    Check(page.Descendants().Single(node => node.Name.LocalName == "Label" && node.Attribute("Text")?.Value == "{Binding MonthName}").Parent?.Attribute("Grid.ColumnSpan")?.Value == "3", "Month details get the full row width");
    var pageCode = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "MauiApp1", "Pages", "MonthlyExpenseDataPage.xaml.cs"));
    Check(pageCode.Contains("protected override void OnDisappearing()") && pageCode.Contains("_model.CancelDeletionReviewCommand.Execute(null);"), "Navigating away cancels the pending review");
    return Task.CompletedTask;
});

await Test("70 imported Income records with zero expenses reach explicit Delete and preserve manual Income/other months", async () =>
{
    for (var index = 0; index < 70; index++)
        await income.SaveAsync(new IncomeTransaction { TransactionDate = new(2022,9,14), ShopId = 1, PaymentModeId = 1, Amount = 600, Note = $"credit {index}" }, "september-70");
    var manual = new IncomeTransaction { TransactionDate = new(2022,9,14), ShopId = 1, PaymentModeId = 1, Amount = 77, Note = "protected manual" };
    var otherMonth = new IncomeTransaction { TransactionDate = new(2022,10,14), ShopId = 1, PaymentModeId = 1, Amount = 88, Note = "other month" };
    await income.SaveAsync(manual); await income.SaveAsync(otherMonth, "september-70");
    var model = new MonthlyExpenseDataPageModel(bills, deletion, new ModalErrorHandler()) { Year = 2022 };
    await model.AppearingCommand.ExecuteAsync(null);
    var month = model.Months.Single(row => row.MonthNumber == 9);
    Check(month.BillCount == 0 && month.ImportedIncomeCount == 70 && month.HasDeletableData, "Income-only Delete enabled");
    var deleting = false;
    model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(model.IsDeletingData) && model.IsDeletingData) { deleting = true; Check(model.IsBusy && model.IsProcessing && !model.IsAwaitingDeletionChoice, "Actual deletion has locked spinner/progress"); } };
    var sawConfirmation = false;
    await RunDeletionReview(model,month,["whole-month","keep-untracked","delete"], state =>
    {
        if (!state.DeletionChoices.Any(choice => choice.Key == "delete")) return;
        sawConfirmation = true;
        Check(state.DeletionChoiceMessage.Contains("Expense bills to delete: 0") && state.DeletionChoiceMessage.Contains("Income records to delete: 70"), "Confirmation explicitly lists 0 Expense/70 Income");
        Check(state.DeletionChoices.Single().IsDestructive && state.DeletionChoices.Single().Title == "Delete", "Final destructive button is explicit");
        Check(state.IsDeleteConfirmation && ReferenceEquals(state.DeleteConfirmationChoice,state.DeletionChoices.Single()), "Pinned Delete binds the active confirmation object");
    });
    var refreshed = model.Months.Single(row => row.MonthNumber == 9);
    Check(sawConfirmation && deleting && refreshed.ImportedIncomeCount == 0 && refreshed.UntrackedIncomeCount == 1, "70 credits deleted and count refreshed");
    Check(await income.GetAsync(manual.Id) is not null && await income.GetAsync(otherMonth.Id) is not null && !critical.IsBusy, "Manual Income, other month and operation locks preserved");
});

await Test("New confirmation preserves expense-only whole-month deletion", async () =>
{
    for (var index = 0; index < 2; index++)
        await bills.SaveItemAsync(new BillTransaction { BillDate = new(2022,1,3), ShopId = 1, PaymentModeId = 2, BillValue = 15, Note = $"expense {index}" });
    var model = new MonthlyExpenseDataPageModel(bills,deletion,new ModalErrorHandler()) { Year = 2022 };
    await model.AppearingCommand.ExecuteAsync(null);
    await RunDeletionReview(model,model.Months.Single(row => row.MonthNumber == 1),["delete"], state =>
        Check(state.DeletionChoiceMessage.Contains("Expense bills to delete: 2") && state.DeletionChoiceMessage.Contains("Income records to delete: 0"), "Expense-only counts unchanged"));
    Check(!(await deletion.GetPlanAsync(2022,1)).Records.Any(), "Expense-only whole month removed");
});

await Test("Explicit import selection deletes only the selected Debit/Credit batch", async () =>
{
    var aExpense = new BillTransaction { BillDate = new(2022,2,3), ShopId = 1, PaymentModeId = 2, BillValue = 15, Note = "batch A expense" };
    var aCredit = new IncomeTransaction { TransactionDate = new(2022,2,3), ShopId = 1, PaymentModeId = 1, Amount = 150, Note = "batch A income" };
    var bExpense = new BillTransaction { BillDate = new(2022,2,3), ShopId = 1, PaymentModeId = 2, BillValue = 25, Note = "batch B expense" };
    var bCredit = new IncomeTransaction { TransactionDate = new(2022,2,3), ShopId = 1, PaymentModeId = 1, Amount = 250, Note = "batch B income" };
    var manual = new IncomeTransaction { TransactionDate = new(2022,2,3), ShopId = 1, PaymentModeId = 1, Amount = 350, Note = "manual" };
    await bills.SaveItemAsync(aExpense,"ui-batch-A"); await income.SaveAsync(aCredit,"ui-batch-A");
    await bills.SaveItemAsync(bExpense,"ui-batch-B"); await income.SaveAsync(bCredit,"ui-batch-B"); await income.SaveAsync(manual);
    var model = new MonthlyExpenseDataPageModel(bills,deletion,new ModalErrorHandler()) { Year = 2022 };
    await model.AppearingCommand.ExecuteAsync(null);
    var sawImport = false;
    await RunDeletionReview(model,model.Months.Single(row => row.MonthNumber == 2),["one-import","ui-batch-A","delete"], state =>
    {
        if (state.DeletionChoiceTitle != "Choose an ETL import") return;
        sawImport = true;
        Check(state.DeletionChoices.Count == 2 && state.DeletionChoices.All(choice => choice.Description.Contains("Expense bills: 1 · Income records: 1")), "Import buttons show linked counts");
    });
    Check(sawImport && await bills.GetAsync(aExpense.Id) is null && await income.GetAsync(aCredit.Id) is null, "Selected batch removed on both sides");
    Check(await bills.GetAsync(bExpense.Id) is not null && await income.GetAsync(bCredit.Id) is not null && await income.GetAsync(manual.Id) is not null, "Other batch/manual record retained");
});

await Test("Explicit legacy review requires per-record approval and keeping all legacy Income deletes nothing", async () =>
{
    var legacy = new IncomeTransaction { TransactionDate = new(2022,3,3), ShopId = 1, PaymentModeId = 1, Amount = 150, Note = "older ETL credit" };
    var manual = new IncomeTransaction { TransactionDate = new(2022,3,3), ShopId = 1, PaymentModeId = 1, Amount = 250, Note = "manual income" };
    await income.SaveAsync(legacy); await income.SaveAsync(manual);
    var model = new MonthlyExpenseDataPageModel(bills,deletion,new ModalErrorHandler()) { Year = 2022 };
    await model.AppearingCommand.ExecuteAsync(null);
    var month = model.Months.Single(row => row.MonthNumber == 3);
    await RunDeletionReview(model,month,["keep-untracked"]);
    Check(await income.GetAsync(legacy.Id) is not null && await income.GetAsync(manual.Id) is not null, "Keeping untracked Income is non-destructive");
    await RunDeletionReview(model,month,["review-untracked","include-income","keep-income","delete"]);
    Check(await income.GetAsync(legacy.Id) is null && await income.GetAsync(manual.Id) is not null, "Only individually approved legacy ID deleted");
});

await Test("Scope/import/final cancellation and leaving during preparation release the review gate", async () =>
{
    var model = new MonthlyExpenseDataPageModel(bills,deletion,new ModalErrorHandler()) { Year = 2022 };
    await model.AppearingCommand.ExecuteAsync(null);
    var month = model.Months.Single(row => row.MonthNumber == 2);
    var before = (await deletion.GetPlanAsync(2022,2)).Records.Count;
    await RunDeletionReview(model,month,[null]);
    await RunDeletionReview(model,month,["one-import",null]);
    await RunDeletionReview(model,month,["one-import","ui-batch-B",null]);
    var operation = model.DeleteMonthCommand.ExecuteAsync(month);
    model.CancelDeletionReviewCommand.Execute(null); // Navigation before the plan query/prompt completes.
    await operation.WaitAsync(TimeSpan.FromSeconds(10));
    Check(!model.IsBusy && !model.IsAwaitingDeletionChoice && (await deletion.GetPlanAsync(2022,2)).Records.Count == before, "All cancellation paths retain records and unlock");
    await RunDeletionReview(model,month,["one-import","ui-batch-B",null]);
});

await Test("Stale/foreign choices and repeat taps cannot approve a pending review or change its year", async () =>
{
    var model = new MonthlyExpenseDataPageModel(bills,deletion,new ModalErrorHandler()) { Year = 2022 };
    await model.AppearingCommand.ExecuteAsync(null);
    var month = model.Months.Single(row => row.MonthNumber == 2);
    var displayed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    model.PropertyChanged += (_, args) => { if (args.PropertyName == nameof(model.IsAwaitingDeletionChoice) && model.IsAwaitingDeletionChoice) displayed.TrySetResult(); };
    var operation = model.DeleteMonthCommand.ExecuteAsync(month);
    await displayed.Task.WaitAsync(TimeSpan.FromSeconds(10));
    var before = (await deletion.GetPlanAsync(2022,2)).Records.Count;
    // A value-identical choice from an old panel is not the active UI object.
    var activeChoice = model.DeletionChoices.First();
    model.ChooseDeletionOptionCommand.Execute(activeChoice with { });
    await model.PreviousYearCommand.ExecuteAsync(null);
    await model.NextYearCommand.ExecuteAsync(null);
    await model.DeleteMonthCommand.ExecuteAsync(month);
    Check(model.IsAwaitingDeletionChoice && !operation.IsCompleted && model.Year == 2022, "Stale object/repeated action/year changes ignored while review is locked");
    model.CancelDeletionReviewCommand.Execute(null);
    await operation.WaitAsync(TimeSpan.FromSeconds(10));
    Check(!model.IsBusy && (await deletion.GetPlanAsync(2022,2)).Records.Count == before, "Cancellation after ignored actions releases gate and preserves data");
    model.Progress = 50;
    Check(model.ProgressFraction == 0.5 && model.ProgressText == "50%", "Progress conversion matches the displayed percentage");
});

await Test("UI-level deletion failure rolls back both sides and releases spinner/progress/review locks", async () =>
{
    var expense = new BillTransaction { BillDate = new(2022,4,3), ShopId = 1, PaymentModeId = 2, BillValue = 15, Note = "rollback expense" };
    var credit = new IncomeTransaction { TransactionDate = new(2022,4,3), ShopId = 1, PaymentModeId = 1, Amount = 150, Note = "rollback income" };
    await bills.SaveItemAsync(expense,"ui-rollback"); await income.SaveAsync(credit,"ui-rollback");
    await Sql($"CREATE TRIGGER ui_delete_failure BEFORE DELETE ON income_transaction WHEN OLD.Id={credit.Id} BEGIN SELECT RAISE(ABORT,'UI deletion regression failure'); END;");
    var model = new MonthlyExpenseDataPageModel(bills,deletion,new ModalErrorHandler()) { Year = 2022 };
    await model.AppearingCommand.ExecuteAsync(null);
    try { await ExpectFailure(() => RunDeletionReview(model,model.Months.Single(row => row.MonthNumber == 4),["whole-month","delete"]), "DB failure is surfaced"); }
    finally { await Sql("DROP TRIGGER ui_delete_failure;"); }
    Check(await bills.GetAsync(expense.Id) is not null && await income.GetAsync(credit.Id) is not null, "Partial expense delete rolled back with Income");
    Check(!model.IsBusy && !model.IsDeletingData && !model.IsAwaitingDeletionChoice && !model.IsProcessing && !critical.IsBusy, "Failure releases all locks");
    await RunDeletionReview(model,model.Months.Single(row => row.MonthNumber == 4),["whole-month","delete"]);
});

Console.WriteLine($"PASS: {passed} regression checks. Isolated evidence directory: {testDirectory}");

static async Task RunDeletionReview(MonthlyExpenseDataPageModel model, ExpenseMonthMaintenanceItem month,
    string?[] keys, Action<MonthlyExpenseDataPageModel>? inspectChoice = null)
{
    var remaining = new Queue<string?>(keys);
    void Respond(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(model.IsAwaitingDeletionChoice) || !model.IsAwaitingDeletionChoice) return;
        Check(model.IsBusy && !model.IsProcessing && !model.IsDeletingData, "Awaiting choices blocks the page without spinner/progress");
        inspectChoice?.Invoke(model);
        Check(remaining.Count > 0, $"Unexpected prompt: {model.DeletionChoiceTitle}");
        var key = remaining.Dequeue();
        if (key is null) model.CancelDeletionReviewCommand.Execute(null);
        else model.ChooseDeletionOptionCommand.Execute(model.DeletionChoices.Single(choice => choice.Key == key));
    }
    model.PropertyChanged += Respond;
    try { await model.DeleteMonthCommand.ExecuteAsync(month).WaitAsync(TimeSpan.FromSeconds(10)); }
    finally { model.PropertyChanged -= Respond; }
    Check(remaining.Count == 0, "All expected review steps were reached");
    Check(!model.IsBusy && !model.IsAwaitingDeletionChoice && !model.IsDeletingData && !model.IsProcessing && !model.IsDeleteConfirmation
        && model.DeleteConfirmationChoice is null && model.DeletionChoices.Count == 0, "Review/processing state fully released");
}

static string CreateBankFixture(string directory, string name, int year, bool includeBalanceHeader)
{
    using var builder = new PdfDocumentBuilder();
    var font = builder.AddStandard14Font(Standard14Font.Courier);
    var page = builder.AddPage(595,842);
    void Text(string text, double x, double y) => page.AddText(text,8,new PdfPoint(x,y),font);
    void Right(string? text, double x, double y) { if (!string.IsNullOrEmpty(text)) Text(text,x-text.Length*4.8,y); }
    Text($"EXTRATO DE {year}",55,790);
    Text("DATA",55,765); Text("DATA",85,765); Text("DESCRITIVO",120,765);
    Right("DEBITO",385,765); Right("CREDITO",465,765);
    if (includeBalanceHeader) Right("SALDO",555,765);
    Text("LANC.",55,755); Text("VALOR",85,755); Text("TRANSPORTE",120,755);
    var rows = new (string Description, string? Debit, string? Credit, string? Balance)[] {
        ("TRF DE PRABURAJ VISHVANATHAN",null,"600.00","740.29"),
        ("TRF INCOME WORDING BUT DEBIT","25.00",null,"715.29"),
        ("BALANCE ONLY",null,null,"1000.00"),
        ("ZERO ONLY","0.00","0.00","1000.00"),
        ("LARGE CREDIT",null,"1999.99","-2500.50"),
        ("NO BALANCE DEBIT","50.00",null,null),
        ("SPACE GROUPED CREDIT",null,"2 197.45","1 525.15"),
        ("REF 123.45 NUMBER","10.00",null,"700.00"),
        ("CREDIT TAKES PRECEDENCE","20.00","30.00","888.00"),
        ("COMMA DECIMAL DEBIT","1.234,56",null,"5.000,00"),
        ("NO BALANCE CREDIT",null,"17.00",null),
        ("NEGATIVE ONLY","-3.00","-4.00","22.00"),
        ("TRF DE PRABURAJ VISHVANATHAN",null,"600.00","740.29") };
    var y = 735.0;
    foreach (var row in rows)
    {
        Text("9.14",55,y); Text("9.13",85,y); Text(row.Description,120,y);
        Right(row.Debit,385,y); Right(row.Credit,465,y); Right(row.Balance,555,y);
        y -= 16;
    }
    Text("SALDO FINAL",120,y-16);
    var path = Path.Combine(directory,name+".pdf");
    File.WriteAllBytes(path,builder.Build());
    return path;
}

async Task Test(string name, Func<Task> action) { await action(); passed++; Console.WriteLine($"PASS: {name}"); }
static void Check(bool condition, string description) { if (!condition) throw new InvalidOperationException(description); }
static async Task ExpectFailure(Func<Task> action, string description)
{
    try { await action(); } catch (Exception) { return; }
    throw new InvalidOperationException(description);
}
static async Task Sql(string sql)
{
    await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
    await using var command = connection.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync();
}
static async Task<int> Count(string table)
{
    if (table is not ("Bill_transcation" or "income_transaction")) throw new ArgumentException("Invalid test table");
    await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
    await using var command = connection.CreateCommand(); command.CommandText = $"SELECT COUNT(*) FROM {table}";
    return Convert.ToInt32(await command.ExecuteScalarAsync());
}
async Task<BillTransaction> AddBill(int month, decimal amount, string? batch)
{
    var item = new BillTransaction { BillDate=new(2026,month,2),ShopId=1,PaymentModeId=2,BillValue=amount,Note="test debit" };
    await bills.SaveItemAsync(item,batch); return item;
}
async Task<IncomeTransaction> AddIncome(int month, decimal amount, string? batch)
{
    var item = new IncomeTransaction { TransactionDate=new(2026,month,2),ShopId=1,PaymentModeId=1,Amount=amount,Note="test credit" };
    await income.SaveAsync(item,batch); return item;
}
static string FindRepositoryRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null) { if (File.Exists(Path.Combine(directory.FullName,"MauiApp1","CrocHuntMPluto.csproj"))) return directory.FullName; directory=directory.Parent; }
    throw new InvalidOperationException("Repository root not found");
}
sealed class TestCurrency : ICurrencyService
{
    public DisplayCurrency CurrentCurrency { get; } = new("EUR","Euro","€","pt-PT",false,true);
    public IReadOnlyList<DisplayCurrency> AvailableCurrencies => [CurrentCurrency];
    public event EventHandler? CurrencyChanged { add { } remove { } }
    public void LoadSavedPreference() { }
    public void ApplyCurrency(string code) { }
}
sealed class TestAuthentication : IAuthenticationService
{
    public bool IsAuthenticated => true;
    public UserAccount? CurrentUser => null;
    public event EventHandler? AuthenticationStateChanged { add { } remove { } }
    public Task<AuthenticationResult> RegisterAsync(string name, string username, string password) => throw new NotSupportedException();
    public Task<AuthenticationResult> LoginAsync(string username, string password) => throw new NotSupportedException();
    public Task<AuthenticationResult> AuthenticateLocalUserAfterBiometricAsync(int userId) => throw new NotSupportedException();
    public Task<IReadOnlyList<LocalUserSummary>> GetLocalUsersAsync() => throw new NotSupportedException();
    public Task<AuthenticationResult> ResetPasswordAsync(int userId, string newPassword) => throw new NotSupportedException();
    public void Logout() => throw new NotSupportedException();
}
