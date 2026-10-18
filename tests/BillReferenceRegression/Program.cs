using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.PageModels;
using FinTracker.Services;
using FinTracker.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Data.Sqlite;

SQLitePCL.Batteries_V2.Init();
var scratch = Directory.CreateTempSubdirectory("fintracker-reference-");
FileSystem.AppDataDirectory = scratch.FullName;
FileSystem.CacheDirectory = scratch.CreateSubdirectory("cache").FullName;
var passed = 0;
try
{
    await VerifyLegacyReferenceUpgrade(false);
    await VerifyLegacyReferenceUpgrade(true);
    foreach (var valid in new string?[] { null, "A", "000AB123", "ABT56900", "123456789012345", "!@#&-_", "AB_!@#&-001" })
        Check(TransactionInput.IsReference(valid), $"Valid reference {valid ?? "NULL"}");
    foreach (var invalid in new[] { "", " ", "AB 12", "AB/12", "<script>", "A'OR1", "1234567890123456", "A\n", "A\0B", "ÁB", "ＡＢ", "AB\u200B", "A%", "A\\B", "A\"B", "A.B", "A:B", "A;B", "A+B", "A=B", "A[B" })
        Check(!TransactionInput.IsReference(invalid), "Reject malformed reference");
    Check(TransactionInput.NormalizeReference(" 000ab12 ") == "000ab12" && TransactionInput.NormalizeReference("   ") is null, "Trim and preserve zeros/case");
    await Reject(() => Task.FromResult(TransactionInput.NormalizeReference("\tAB123")), "Reference normalization rejects surrounding control characters");
    Check(TransactionInput.ValidateManualNote("October grocery 123\r\nSecond line") is null, "Manual multiline note");
    foreach (var note in new[] { "Bill #123", "<script>", "A\tB", "A\0B", "A\u200BB", new string('A', 1001) })
        Check(TransactionInput.ValidateManualNote(note) is not null, "Reject invalid manual note");
    Check(TransactionInput.ValidateManualNote("Legacy # note", "Legacy # note") is null, "Unchanged historical note is preserved");
    Check(TransactionInput.ValidateManualNote("Changed # note", "Legacy # note") is not null, "Changed historical note must validate");

    await Sql(@"CREATE TABLE Shop(Id INTEGER PRIMARY KEY,Name TEXT NOT NULL UNIQUE,Category TEXT,Category_Id INTEGER);
INSERT INTO Shop VALUES(3,'ABT Store', 'DefaultShop',NULL);
CREATE TABLE Payment_mode(Id INTEGER PRIMARY KEY,Name TEXT NOT NULL UNIQUE); INSERT INTO Payment_mode VALUES(5,'Cash');
CREATE TABLE Bill_transcation(Id INTEGER PRIMARY KEY,Bill_date TEXT NOT NULL,Shop_Id INTEGER,Payment_Mode_Id INTEGER,Bill_value REAL,Note TEXT,
FOREIGN KEY(Shop_Id) REFERENCES Shop(Id),FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));
INSERT INTO Bill_transcation VALUES(9,'2026-10-01T00:00:00.0000000',3,5,10,'Legacy # note');
CREATE TABLE income_transaction(Id INTEGER PRIMARY KEY,Transaction_date TEXT NOT NULL,Shop_Id INTEGER,Payment_Mode_Id INTEGER,Amount REAL,Note TEXT,
FOREIGN KEY(Shop_Id) REFERENCES Shop(Id),FOREIGN KEY(Payment_Mode_Id) REFERENCES Payment_mode(Id));
INSERT INTO income_transaction VALUES(11,'2026-10-02T00:00:00.0000000',3,5,20,'Bank / credit');");
    var currency = new TestCurrency();
    var context = new FinancialAccountContext(currency);
    var operations = new CriticalOperationState();
    var accounts = new FinancialAccountRepository(context, operations, currency);
    await accounts.InitializeAsync();
    var company = (await accounts.ListAsync()).Single();
    context.Select(company);
    // A pre-reference archive exercises restore migration of an existing installation.
    var oldArchive = await ArchiveDatabase("older.hhtbackup");
    foreach (var table in new[] { "Bill_transcation", "income_transaction" })
        await Sql($"ALTER TABLE {table} ADD COLUMN bill_reference_no CHAR(15) NULL CHECK(bill_reference_no IS NULL OR (length(bill_reference_no) BETWEEN 1 AND 15 AND bill_reference_no NOT GLOB '*[^A-Za-z0-9]*' AND instr(bill_reference_no,char(0))=0))");
    var constrainedArchive = await ArchiveDatabase("older-constrained.hhtbackup");
    var bills = new BillTransactionRepository(NullLogger<BillTransactionRepository>.Instance, operations, context);
    var income = new IncomeTransactionRepository(operations, context);
    var shops = new ShopRepository(NullLogger<ShopRepository>.Instance, operations, context);
    var modes = new PaymentModeRepository(NullLogger<PaymentModeRepository>.Instance, operations, context);
    var categories = new CategoryRepository(NullLogger<CategoryRepository>.Instance, operations, context);
    var mappings = new EtlTextMapRepository(NullLogger<EtlTextMapRepository>.Instance, operations, context);
    var recurring = new RecurringBillRepository(operations, context);
    var budgets = new MonthlyBudgetRepository(context);
    var backup = new BackupService(operations, context, accounts, bills, income, categories, shops, modes, mappings, recurring, budgets);
    await Task.WhenAll(bills.EnsureInitializedAsync(), income.EnsureInitializedAsync());
    Check((await bills.GetAsync(9)) is { BillReferenceNo: null, Note: "Legacy # note" }, "Expense migration preserves data");
    Check((await income.GetAsync(11)) is { BillReferenceNo: null, Note: "Bank / credit" }, "Income migration preserves data");
    await using (var db = await Open()) { await BillReferenceSchema.EnsureAsync(db); await BillReferenceSchema.EnsureAsync(db); }
    Check((await bills.ListAsync(2026)).Count == 1, "Reference migration is idempotent");
    var expense = new BillTransaction { BillDate = new(2026, 10, 3), ShopId = 3, PaymentModeId = 5, BillValue = 30, BillReferenceNo = "ABT56900", Note = "October purchase" };
    var credit = new IncomeTransaction { TransactionDate = new(2026, 10, 4), ShopId = 3, PaymentModeId = 5, Amount = 40, BillReferenceNo = "ABT56790", Note = "October income" };
    await bills.SaveItemAsync(expense); await income.SaveAsync(credit);
    Check((await bills.GetAsync(expense.Id))?.BillReferenceNo == "ABT56900" && (await income.GetAsync(credit.Id))?.BillReferenceNo == "ABT56790", "Both references persist and reload");
    expense.BillReferenceNo = "000ab123"; await bills.SaveItemAsync(expense);
    Check((await bills.GetAsync(expense.Id))?.BillReferenceNo == "000ab123", "Reference update preserves zeros and case");
    expense.BillReferenceNo = " "; await bills.SaveItemAsync(expense);
    Check((await bills.GetAsync(expense.Id))?.BillReferenceNo is null, "Clearing reference stores NULL");
    expense.BillReferenceNo = "ABT56900"; await bills.SaveItemAsync(expense);
    var bad = new BillTransaction { BillDate = expense.BillDate, ShopId = 3, PaymentModeId = 5, BillValue = 1, BillReferenceNo = "AB';DROP" };
    await Reject(() => bills.SaveItemAsync(bad), "Repository rejects invalid reference");
    bad.BillReferenceNo = null; bad.Note = "New # note";
    await Reject(() => bills.SaveItemAsync(bad), "Expense repository validates manual notes");
    var badIncome = new IncomeTransaction { TransactionDate = credit.TransactionDate, ShopId = 3, PaymentModeId = 5, Amount = 1, Note = "New # credit" };
    await Reject(() => income.SaveAsync(badIncome), "Income repository validates manual notes");
    var historicalExpense = (await bills.GetAsync(9))!; historicalExpense.BillReferenceNo = "HIST9";
    await bills.SaveItemAsync(historicalExpense);
    Check((await bills.GetAsync(9))?.Note == "Legacy # note", "Unchanged legacy note survives reference edit");
    historicalExpense.Note = "Changed # note";
    await Reject(() => bills.SaveItemAsync(historicalExpense), "Changed historical note is validated by repository");
    var imported = new IncomeTransaction { TransactionDate = new(2025, 1, 1), ShopId = 3, PaymentModeId = 5, Amount = 1, Note = "Bank / reference #123" };
    await income.SaveAsync(imported, "test-import");
    Check((await income.GetAsync(imported.Id))?.Note == imported.Note, "ETL description retains punctuation");
    await Reject(() => Sql("UPDATE income_transaction SET bill_reference_no='BAD:' WHERE Id=11"), "Database rejects invalid reference");
    await Reject(() => Sql("UPDATE Bill_transcation SET bill_reference_no=char(65,0,66) WHERE Id=9"), "Database rejects embedded NUL");

    var search = new DashboardSearchService(bills, income, context);
    var results = await search.SearchAsync(" abt ", 0, default);
    Check(results.Count == 3 && results.Count(x => x.IsReference) == 2, "ABT returns shop plus both income/expense references");
    Check((await search.SearchAsync("abt569", 0, default)).Single().Id == expense.Id, "Reference prefix narrows correctly");
    Check((await search.SearchAsync("Store", 0, default)).Count == 0, "Shop search remains a prefix match");
    Check((await search.SearchAsync("' OR 1=1--", 0, default)).Count == 0, "Search SQL is parameterized");
    var specialShop = new Shop { Name = "50%_ O'Brien", CategoryId = (await categories.ListAsync()).First().Id };
    await shops.SaveItemAsync(specialShop);
    Check((await search.SearchAsync("50%_ O'", 0, default)).Single().ShopId == specialShop.Id, "Shop punctuation and literal LIKE wildcards work");
    Check((await search.SearchAsync("%", 0, default)).Count == 0, "Wildcard input does not match every shop");
    credit.BillReferenceNo = "ABT56900"; await income.SaveAsync(credit);
    Check((await search.SearchAsync("ABT56900", 0, default)).Count(x => x.IsReference) == 2, "Repeated references retain both transaction types");
    credit.BillReferenceNo = "ABT56790"; await income.SaveAsync(credit);
    for (var i = 0; i < 24; i++) await shops.SaveItemAsync(new Shop { Name = $"Page {i:00}", CategoryId = specialShop.CategoryId });
    var first = await search.SearchAsync("Page", 0, default);
    var second = await search.SearchAsync("Page", DashboardSearchService.PageSize, default);
    Check(first.Count == 21 && second.Count == 4 && first.Take(20).Select(x => x.Id).Intersect(second.Select(x => x.Id)).Count() == 0, "Pagination exposes all matches without overlaps");
    using (var cancelled = new CancellationTokenSource()) { cancelled.Cancel(); await Reject(() => search.SearchAsync("ABT", 0, cancelled.Token), "Cancelled searches stop"); }

    foreach (var symbol in "!@#&-_")
    {
        expense.BillReferenceNo = $"Z{symbol}REF001"; credit.BillReferenceNo = $"Z{symbol}REF002";
        await bills.SaveItemAsync(expense); await income.SaveAsync(credit);
        var matches = await search.SearchAsync($"Z{symbol}R", 0, default);
        Check(matches.Count == 2 && matches.All(x => x.IsReference), $"Symbol {symbol} persists and prefix-searches across both transaction tables");
    }
    expense.BillReferenceNo = "AB_123"; credit.BillReferenceNo = "ABX123";
    await bills.SaveItemAsync(expense); await income.SaveAsync(credit);
    Check((await search.SearchAsync("ab_", 0, default)).Single().BillReferenceNo == "AB_123", "Reference underscore is a literal, never a LIKE wildcard");
    expense.BillReferenceNo = "ABT56900"; credit.BillReferenceNo = "ABT56790";
    await bills.SaveItemAsync(expense); await income.SaveAsync(credit);

    var errors = new ModalErrorHandler();
    var incomeAnalytics = new IncomeAnalysisPageModel(income, errors) { ReferenceDate = new(2026, 10, 1) };
    for (var attempt = 0; attempt < 100 && incomeAnalytics.IsBusy; attempt++) await Task.Delay(20);
    Check(!incomeAnalytics.IsBusy && incomeAnalytics.Groups.Any(x => x.Bills.Any(b => b.Id == credit.Id)), "Income analytics loads grouped income transactions");
    var incomeGroup = incomeAnalytics.Groups.First();
    var notifications = new List<string?>();
    incomeGroup.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
    var originalIncomeTotal = incomeGroup.TotalExpense;
    await incomeAnalytics.ToggleCommand.ExecuteAsync(incomeGroup);
    Check(incomeGroup.IsExpanded && incomeGroup.ExpandGlyph == "▲" && notifications.Contains("ExpandGlyph"), "Income category expansion updates details and glyph notifications");
    await incomeAnalytics.ToggleCommand.ExecuteAsync(incomeGroup);
    Check(!incomeGroup.IsExpanded && incomeGroup.ExpandGlyph == "▼" && incomeGroup.TotalExpense == originalIncomeTotal, "Income category collapse preserves its transaction totals");
    var model = new LandingDashboardPageModel(search, context, errors);
    model.SearchText = "Page"; model.SearchText = "abt56"; await SearchSettled(model);
    Check(model.Suggestions.Count == 2 && model.Suggestions.All(x => x.IsReference), "Rapid typing discards stale suggestions");
    model.SelectShopCommand.Execute(model.Suggestions.First());
    await model.OpenAnalysisCommand.ExecuteAsync(null);
    Check(Shell.Current.Routes.Last().Contains("year=2026&reference=ABT"), "Reference navigation carries year and filter");
    model.SearchText = " "; await SearchSettled(model);
    Check(!model.HasSuggestions && !model.ShowNoMatches && !model.CanSearch, "Clearing resets results and selection");
    model.SearchText = "ABT Store"; await SearchSettled(model);
    model.SelectShopCommand.Execute(model.Suggestions.Single());
    await model.OpenAnalysisCommand.ExecuteAsync(null);
    Check(Shell.Current.Routes.Last() == "billanalysis?shopId=3", "Shop selection preserves unfiltered analysis navigation");
    model.SearchText = "No such shop"; await SearchSettled(model);
    Check(model.ShowNoMatches && !model.CanSearch, "No matches disable navigation");
    model.SearchText = "Page"; await SearchSettled(model);
    Check(model.Suggestions.Count == 20 && model.HasMoreResults, "Search model bounds initial suggestions");
    await model.LoadMoreCommand.ExecuteAsync(null);
    Check(model.Suggestions.Count == 24 && !model.HasMoreResults, "Show more exposes remaining matches");
    var analysis = new ShopBillAnalysisService(shops, bills, income);
    var detail = await analysis.GetBillsAsync(3, 2026, null);
    Check(detail.Expenses.Sum(x => x.BillValue) == (await analysis.GetYearAnalysisAsync(3, 2026)).TotalSpent && detail.Income.Sum(x => x.BillValue) == (await analysis.GetIncomeYearAnalysisAsync(3, 2026)).TotalSpent, "Analysis lists reconcile to chart summaries");
    Check((await analysis.GetBillsAsync(3, 2026, "abt56900")).Expenses.Count == 1, "Analysis reference filter is case insensitive");
    var analysisModel = new BillAnalysisPageModel(analysis, context, errors);
    analysisModel.ApplyQueryAttributes(new Dictionary<string, object> { ["shopId"] = "3", ["year"] = "2026", ["reference"] = "ABT56900" });
    await analysisModel.AppearingCommand.ExecuteAsync(null);
    Check(analysisModel.ExpenseBills.Count == 1 && analysisModel.IncomeBills.Count == 0, "Selected reference filters analysis lists");
    await analysisModel.ClearReferenceFilterCommand.ExecuteAsync(null);
    Check(analysisModel.ExpenseBills.Count == 2 && analysisModel.IncomeBills.Count == 2, "Clear filter restores full lists");

    var otherCompany = await accounts.CreateAsync(new FinancialAccount { Name = "Other", CurrencyCode = "EUR" }, false);
    model.SearchText = "ABT";
    context.Select(otherCompany); await SearchSettled(model);
    Check(model.SearchText.Length == 0 && !model.HasSuggestions && !model.CanSearch, "Company change automatically clears search state");
    Check((await search.SearchAsync("ABT", 0, default)).Count == 0, "Search isolates company data");
    model.ResetSearch();
    Check(!model.HasSuggestions && !model.CanSearch, "Company reset clears selection");
    context.Select(company);

    await HistoryChecks.Run(bills, income, shops, categories, modes, context, accounts, Check);

    var selected = await backup.CreateSelectedMonthsBackupAsync(2026, [10]);
    var selectedFile = Path.Combine(scratch.FullName, "selected.hhtbackup"); File.Copy(selected.Path, selectedFile);
    expense.BillReferenceNo = "CHANGED"; await bills.SaveItemAsync(expense);
    await backup.RestoreSelectedMonthsBackupAsync(selectedFile);
    Check((await bills.ListAsync(2026)).Any(x => x.BillReferenceNo == "ABT56900") && (await bills.ListAsync(2026)).Any(x => x.Note == "Legacy # note"), "Selected restore preserves references and historical notes");
    Check((await income.GetAsync(credit.Id))?.BillReferenceNo == "ABT56790", "Selected restore preserves existing income scope");
    var corruptSelected = MutateSelected(selectedFile, "invalid-selected.hhtbackup", b => b["BillReferenceNo"] = "BAD:");
    var count = (await bills.ListAsync(2026)).Count;
    await Reject(() => backup.RestoreSelectedMonthsBackupAsync(corruptSelected), "Invalid selected reference rejected before delete");
    Check((await bills.ListAsync(2026)).Count == count, "Rejected restore leaves bills intact");
    var oldSelected = MutateSelected(selectedFile, "older-selected.hhtbackup", b => b.Remove("BillReferenceNo"));
    await backup.RestoreSelectedMonthsBackupAsync(oldSelected);
    Check((await bills.ListAsync(2026)).All(x => x.BillReferenceNo is null), "Older selected archive restores with NULL references");
    await backup.RestoreSelectedMonthsBackupAsync(selectedFile);
    var complete = await backup.CreateBackupAsync();
    var completeFile = Path.Combine(scratch.FullName, "complete.hhtbackup"); File.Copy(complete.Path, completeFile);
    credit.BillReferenceNo = "CHANGED"; await income.SaveAsync(credit);
    await backup.RestoreBackupAsync(completeFile); context.Select((await accounts.ListAsync()).Single(x => x.Id == company.Id));
    Check((await bills.ListAsync(2026)).Any(x => x.BillReferenceNo == "ABT56900") && (await income.GetAsync(credit.Id))?.BillReferenceNo == "ABT56790", "Complete restore round trips both references");
    var invalidComplete = await CorruptComplete(completeFile);
    Check(!(await backup.ValidateBackupAsync(invalidComplete)).IsValid, "Complete backup validation rejects malformed references");
    await Reject(() => backup.RestoreBackupAsync(invalidComplete), "Invalid complete restore is rejected before replacement");
    Check((await income.GetAsync(credit.Id))?.BillReferenceNo == "ABT56790", "Rejected complete restore preserves live data");
    await Reject(() => backup.RestoreBackupAsync(oldArchive, progress: new ThrowProgress()), "Failure after replacement triggers rollback");
    context.Select((await accounts.ListAsync()).Single(x => x.Id == company.Id));
    Check((await income.GetAsync(credit.Id))?.BillReferenceNo == "ABT56790", "Safety rollback preserves references");

    var symbolExpense = (await bills.ListAsync(2026)).Single(x => x.BillReferenceNo == "ABT56900");
    symbolExpense.BillReferenceNo = "AB_!@#&-001"; credit.BillReferenceNo = "IN_!@#&-002";
    await bills.SaveItemAsync(symbolExpense); await income.SaveAsync(credit);
    var symbolSelected = await backup.CreateSelectedMonthsBackupAsync(2026, [10]);
    var symbolSelectedFile = Path.Combine(scratch.FullName, "symbols-selected.hhtbackup"); File.Copy(symbolSelected.Path, symbolSelectedFile);
    symbolExpense.BillReferenceNo = "CHANGED"; await bills.SaveItemAsync(symbolExpense);
    await backup.RestoreSelectedMonthsBackupAsync(symbolSelectedFile);
    Check((await bills.ListAsync(2026)).Any(x => x.BillReferenceNo == "AB_!@#&-001"), "Selected-month backup/restore preserves every permitted reference symbol");
    var symbolComplete = await backup.CreateBackupAsync();
    var symbolCompleteFile = Path.Combine(scratch.FullName, "symbols-complete.hhtbackup"); File.Copy(symbolComplete.Path, symbolCompleteFile);
    credit.BillReferenceNo = "CHANGED"; await income.SaveAsync(credit);
    await backup.RestoreBackupAsync(symbolCompleteFile); context.Select((await accounts.ListAsync()).Single(x => x.Id == company.Id));
    Check((await bills.ListAsync(2026)).Any(x => x.BillReferenceNo == "AB_!@#&-001") && (await income.GetAsync(credit.Id))?.BillReferenceNo == "IN_!@#&-002", "Complete backup/restore preserves symbols in both transaction tables");
    Check((await search.SearchAsync("IN_!@#&-", 0, default)).Single().Id == credit.Id, "Restored income symbol reference remains searchable");
    model.SearchText = "IN_!@#&-"; await SearchSettled(model);
    model.SelectShopCommand.Execute(model.Suggestions.Single());
    await model.OpenAnalysisCommand.ExecuteAsync(null);
    Check(Shell.Current.Routes.Last().Contains("reference=" + Uri.EscapeDataString("IN_!@#&-002")), "Selecting a symbol reference URL-encodes reserved query characters");
    analysisModel.ApplyQueryAttributes(new Dictionary<string, object> { ["shopId"] = "3", ["year"] = "2026", ["reference"] = "IN_!@#&-002" });
    await analysisModel.AppearingCommand.ExecuteAsync(null);
    Check(analysisModel.IncomeBills.Single().BillReferenceNo == "IN_!@#&-002" && analysisModel.ExpenseBills.Count == 0, "Selected symbol reference filters the correct analysis records");
    await backup.RestoreBackupAsync(oldArchive); context.Select((await accounts.ListAsync()).Single());
    Check((await bills.GetAsync(9)) is { BillReferenceNo: null, Note: "Legacy # note" } && (await income.GetAsync(11)) is { BillReferenceNo: null, Note: "Bank / credit" }, "Older complete archive migrates both tables without losing notes");
    await backup.RestoreBackupAsync(constrainedArchive); context.Select((await accounts.ListAsync()).Single());
    var upgradedExpense = (await bills.GetAsync(9))!; var upgradedIncome = (await income.GetAsync(11))!;
    upgradedExpense.BillReferenceNo = "!@#&-_EXP"; upgradedIncome.BillReferenceNo = "!@#&-_INC";
    await bills.SaveItemAsync(upgradedExpense); await income.SaveAsync(upgradedIncome);
    Check((await bills.GetAsync(9))?.BillReferenceNo == "!@#&-_EXP" && (await income.GetAsync(11))?.BillReferenceNo == "!@#&-_INC", "Restoring an older alphanumeric-only archive upgrades constraints in both tables");
    Check(errors.Errors.Count == 0, "No page-model errors");
    Console.WriteLine($"{passed} bill-reference regression checks passed.");
}
finally
{
    SqliteConnection.ClearAllPools();
    var resolved = Path.GetFullPath(scratch.FullName);
    if (resolved.StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) scratch.Delete(true);
}

void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); passed++; Console.WriteLine("PASS: " + message); }
async Task Reject(Func<Task> action, string message) { var rejected = false; try { await action(); } catch (Exception ex) when (ex is ArgumentException or SqliteException or InvalidDataException or OperationCanceledException) { rejected = true; } Check(rejected, message); }
async Task<SqliteConnection> Open() { var db = new SqliteConnection(Constants.DatabasePath); await db.OpenAsync(); return db; }
async Task Sql(string sql) { await using var db = await Open(); await using var cmd = db.CreateCommand(); cmd.CommandText = sql; await cmd.ExecuteNonQueryAsync(); }
async Task SearchSettled(LandingDashboardPageModel model) { for (var i = 0; i < 100 && model.IsSearching; i++) await Task.Delay(20); Check(!model.IsSearching, "Search settles"); }
async Task<string> ArchiveDatabase(string name)
{
    var snapshot = Path.Combine(scratch.FullName, "old.db3");
    await using (var db = await Open()) { await using var target = new SqliteConnection($"Data Source={snapshot};Pooling=False"); await target.OpenAsync(); db.BackupDatabase(target); }
    var metadata = new BackupMetadata { ShopCount = 1, PaymentModeCount = 3, BillCount = 1, DatabaseSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(snapshot))) };
    var path = Path.Combine(scratch.FullName, name);
    using var zip = ZipFile.Open(path, ZipArchiveMode.Create); zip.CreateEntryFromFile(snapshot, Constants.DatabaseFilename);
    await using var output = zip.CreateEntry("metadata.json").Open(); await JsonSerializer.SerializeAsync(output, metadata);
    return path;
}
string MutateSelected(string source, string name, Action<JsonObject> mutate)
{
    var path = Path.Combine(scratch.FullName, name); File.Copy(source, path);
    using var zip = ZipFile.Open(path, ZipArchiveMode.Update); var entry = zip.GetEntry("selected-months.json")!;
    JsonObject document; using (var input = entry.Open()) document = JsonNode.Parse(input)!.AsObject();
    foreach (var bill in document["Bills"]!.AsArray()) mutate(bill!.AsObject());
    entry.Delete(); using var output = new StreamWriter(zip.CreateEntry("selected-months.json").Open()); output.Write(document.ToJsonString());
    return path;
}
async Task<string> CorruptComplete(string source)
{
    var path = Path.Combine(scratch.FullName, "invalid-complete.hhtbackup"); File.Copy(source, path);
    var dbPath = Path.Combine(scratch.FullName, "invalid.db3");
    using (var zip = ZipFile.OpenRead(source)) zip.GetEntry(Constants.DatabaseFilename)!.ExtractToFile(dbPath);
    await using (var db = new SqliteConnection($"Data Source={dbPath};Pooling=False"))
    {
        await db.OpenAsync(); await using var command = db.CreateCommand();
        command.CommandText = "PRAGMA ignore_check_constraints=ON; DROP TRIGGER IF EXISTS TR_income_transaction_ReferenceUpdate; UPDATE income_transaction SET bill_reference_no='BAD:'";
        await command.ExecuteNonQueryAsync();
    }
    using var archive = ZipFile.Open(path, ZipArchiveMode.Update);
    BackupMetadata metadata; using (var input = archive.GetEntry("metadata.json")!.Open()) metadata = JsonSerializer.Deserialize<BackupMetadata>(input)!;
    metadata.DatabaseSha256 = Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(dbPath)));
    archive.GetEntry("metadata.json")!.Delete(); archive.GetEntry(Constants.DatabaseFilename)!.Delete();
    archive.CreateEntryFromFile(dbPath, Constants.DatabaseFilename);
    await using var output = archive.CreateEntry("metadata.json").Open(); await JsonSerializer.SerializeAsync(output, metadata);
    return path;
}

async Task VerifyLegacyReferenceUpgrade(bool injectFailure)
{
    await using var db = new SqliteConnection("Data Source=:memory:"); await db.OpenAsync();
    async Task Exec(string sql) { await using var command = db.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
    async Task<object?> Value(string sql) { await using var command = db.CreateCommand(); command.CommandText = sql; return await command.ExecuteScalarAsync(); }
    await Exec("PRAGMA foreign_keys=ON; CREATE TABLE Audit(Value TEXT);");
    foreach (var table in new[] { "Bill_transcation", "income_transaction" })
    {
        await Exec($@"CREATE TABLE {table}(Id INTEGER PRIMARY KEY AUTOINCREMENT, FinancialAccountId INTEGER NOT NULL DEFAULT 7,
Extra TEXT NOT NULL DEFAULT 'preserved' CHECK(length(Extra)>0), bill_reference_no CHAR(15) NULL CHECK(bill_reference_no IS NULL OR (length(bill_reference_no) BETWEEN 1 AND 15 AND bill_reference_no NOT GLOB '*[^A-Za-z0-9]*' AND instr(bill_reference_no,char(0))=0)));
INSERT INTO {table}(Id,bill_reference_no) VALUES(1,'000Ab12'),(500,NULL); DELETE FROM {table} WHERE Id=500;
CREATE INDEX IX_{table}_Extra ON {table}(Extra);
CREATE TRIGGER TR_{table}_Audit AFTER UPDATE ON {table} BEGIN INSERT INTO Audit VALUES(NEW.Extra); END;
CREATE TRIGGER TR_{table}_ReferenceInsert BEFORE INSERT ON {table} WHEN NEW.bill_reference_no GLOB '*[^A-Za-z0-9]*' BEGIN SELECT RAISE(ABORT,'Old rule'); END;
CREATE TRIGGER TR_{table}_ReferenceUpdate BEFORE UPDATE OF bill_reference_no ON {table} WHEN NEW.bill_reference_no GLOB '*[^A-Za-z0-9]*' BEGIN SELECT RAISE(ABORT,'Old rule'); END;
CREATE VIEW View_{table} AS SELECT Id,bill_reference_no FROM {table};
CREATE TABLE Child_{table}(ParentId INTEGER REFERENCES {table}(Id) ON DELETE CASCADE); INSERT INTO Child_{table} VALUES(1);");
    }
    if (injectFailure)
    {
        await Exec("PRAGMA ignore_check_constraints=ON; DROP TRIGGER TR_income_transaction_ReferenceUpdate; UPDATE income_transaction SET bill_reference_no='BAD:'; PRAGMA ignore_check_constraints=OFF;");
        await Reject(() => BillReferenceSchema.EnsureAsync(db), "Failed legacy migration is rejected atomically");
        Check(Convert.ToInt64(await Value("SELECT count(*) FROM sqlite_master WHERE type='table' AND sql LIKE '%[^A-Za-z0-9]*%';")) == 2, "Failure rolls both tables back to their original constraints");
        Check(Convert.ToInt64(await Value("SELECT count(*) FROM sqlite_master WHERE name LIKE '%ReferenceV2%';")) == 0 && Convert.ToInt64(await Value("SELECT count(*) FROM Bill_transcation")) == 1, "Failed rebuild leaves original rows and no replacement table");
    }
    else
    {
        await BillReferenceSchema.EnsureAsync(db); await BillReferenceSchema.EnsureAsync(db);
        foreach (var table in new[] { "Bill_transcation", "income_transaction" })
        {
            Check((string?)await Value($"SELECT bill_reference_no FROM {table} WHERE Id=1") == "000Ab12", "Legacy migration preserves IDs, zeros and case");
            Check(Convert.ToInt64(await Value($"SELECT count(*) FROM Child_{table}")) == 1 && Convert.ToInt64(await Value($"SELECT count(*) FROM View_{table}")) == 1, "Legacy migration preserves referencing rows and views");
            await Exec($"UPDATE {table} SET bill_reference_no='AB_!@#&-123' WHERE Id=1; INSERT INTO {table}(bill_reference_no) VALUES('!@#&-_');");
            Check(Convert.ToInt64(await Value($"SELECT MAX(Id) FROM {table}")) == 501, "Legacy migration preserves deleted AUTOINCREMENT high-water mark");
            Check((string?)await Value($"SELECT Extra FROM {table} WHERE Id=501") == "preserved" && Convert.ToInt64(await Value($"SELECT count(*) FROM sqlite_master WHERE name='IX_{table}_Extra'")) == 1, "Legacy migration retains unrelated defaults and indexes");
            foreach (var invalid in new[] { "BAD:", "AB/12", "", "1234567890123456" })
                await Reject(() => Exec($"UPDATE {table} SET bill_reference_no='{invalid}' WHERE Id=1"), "Migrated database rejects unsupported characters/lengths");
            await Reject(() => Exec($"INSERT INTO {table}(bill_reference_no) VALUES('BAD:')"), "Migrated database rejects invalid direct inserts");
            await Reject(() => Exec($"UPDATE {table} SET Extra='' WHERE Id=1"), "Migration retains unrelated CHECK constraints");
        }
        Check(Convert.ToInt64(await Value("SELECT count(*) FROM Audit")) == 2, "Custom triggers survive without firing during migration copies");
        Check(await Value("PRAGMA foreign_key_check") is null, "Legacy migration preserves referential integrity");
    }
    Check(Convert.ToInt64(await Value("PRAGMA foreign_keys")) == 1 && Convert.ToInt64(await Value("PRAGMA legacy_alter_table")) == 0, "Migration restores connection settings on success and failure");
}
sealed class ThrowProgress : IProgress<double> { public void Report(double value) { if (value >= 90) throw new OperationCanceledException("Injected restore failure"); } }
