using System.Collections.Concurrent;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.PageModels;
using FinTracker.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

SQLitePCL.Batteries_V2.Init();
var scratch = Directory.CreateTempSubdirectory("fintracker-sql-etl-");
FileSystem.AppDataDirectory = scratch.FullName;
FileSystem.CacheDirectory = scratch.CreateSubdirectory("cache").FullName;
var passed = 0;
try
{
    var operations = new CriticalOperationState();
    var currency = new TestCurrency();
    var context = new FinancialAccountContext(currency);
    var accounts = new FinancialAccountRepository(context, operations, currency);
    await accounts.InitializeAsync();
    var firstCompany = (await accounts.ListAsync()).Single(); context.Select(firstCompany);
    var categories = new CategoryRepository(NullLogger<CategoryRepository>.Instance, operations, context);
    var shops = new ShopRepository(NullLogger<ShopRepository>.Instance, operations, context);
    var modes = new PaymentModeRepository(NullLogger<PaymentModeRepository>.Instance, operations, context);
    var bills = new BillTransactionRepository(NullLogger<BillTransactionRepository>.Instance, operations, context);
    var income = new IncomeTransactionRepository(operations, context);
    var maps = new EtlTextMapRepository(NullLogger<EtlTextMapRepository>.Instance, operations, context);
    var mapping = new EtlTextMappingService();
    var importer = new EtlPdfImportService(bills, income, shops, categories, modes, context, operations, maps, mapping, NullLogger<EtlPdfImportService>.Instance);
    var setup = await Setup();
    await bills.EnsureInitializedAsync(); await income.EnsureInitializedAsync();
    await Sql($"ALTER TABLE ETL_TEXT_MAP DROP COLUMN FinancialAccountId; INSERT INTO ETL_TEXT_MAP(LOOKUPTEXT,SHOP_ID) VALUES('LEGACY LOOKUP',{setup.Vault});");
    Check((await maps.ListAsync()).Single().LookupText == "LEGACY LOOKUP", "Old ETL mapping schema repairs ownership using the referenced shop");
    maps.InvalidateAfterDatabaseRestore();
    Check((await maps.ListAsync()).Count == 1, "ETL mapping schema repair is idempotent");

    var schema = new SqlSchemaMetadataService();
    var analyzer = new ReadOnlySqlAnalyzerService();
    var sqlModel = new ReadOnlySqlAnalyzerPageModel(analyzer, schema, new SqlQueryBuilderService());
    await sqlModel.RefreshTableStructuresAsync();
    Check(sqlModel.TableStructureQueries.Contains("PRAGMA table_info(Bill_transcation)") && sqlModel.TableStructureQueries.Contains("PRAGMA table_info(Shop)") && sqlModel.TableStructureQueries.Contains("PRAGMA table_info(income_transaction)"), "Selector displays complete PRAGMA statements");
    Check(sqlModel.TableStructureQueries.Contains("PRAGMA table_info(sqlite_sequence)"), "Selector includes SQLite-maintained tables");
    foreach (var query in sqlModel.TableStructureQueries.ToArray())
    {
        sqlModel.SelectedTableStructureQuery = query;
        Check(sqlModel.SqlText == query, "Selection fills SQL editor");
        await sqlModel.ExecuteQueryCommand.ExecuteAsync(null);
        Check(!sqlModel.HasError && sqlModel.Columns.Contains("name") && sqlModel.Columns.Contains("type") && sqlModel.Columns.Contains("pk"), "Selected table structure executes through read-only analyzer");
    }
    sqlModel.SelectedTableStructureQuery = "PRAGMA table_info(Bill_transcation)";
    await sqlModel.ExecuteQueryCommand.ExecuteAsync(null);
    Check(sqlModel.Rows.Any(row => row.Contains("bill_reference_no")), "Table structure includes the new reference column");
    await Sql("CREATE TABLE \"Order Details\"(Id INTEGER,Value TEXT); CREATE TABLE \"Update\"(Id INTEGER); CREATE TABLE \"a\"\"; DROP TABLE Shop;--\"(Id INTEGER); CREATE VIEW StructureView AS SELECT 1 AS Id;");
    await sqlModel.RefreshTableStructuresAsync();
    Check(sqlModel.TableStructureQueries.Contains("PRAGMA table_info(\"Order Details\")") && !sqlModel.TableStructureQueries.Any(x => x.Contains("StructureView")), "Dynamic refresh discovers tables with spaces and excludes views");
    foreach (var query in sqlModel.TableStructureQueries.Where(x => x.Contains('"')).ToArray())
    {
        var result = await analyzer.ExecuteAsync(query);
        Check(result.ErrorMessage is null && result.RowsReturned > 0, "Quoted and keyword table names execute safely");
    }
    Check((await shops.ListAsync()).Any(), "Schema queries cannot execute SQL embedded in table names");
    foreach (var sql in new[] { "DROP TABLE Shop", "PRAGMA writable_schema=ON", "PRAGMA table_info(Shop); DROP TABLE Shop", "PRAGMA table_info(\"Shop\") = 1" })
        Check((await analyzer.ExecuteAsync(sql)).ErrorMessage is not null, "Mutating or compound SQL remains rejected");
    await Sql("DROP TABLE \"Order Details\"; CREATE TABLE RestoredTable(Id INTEGER);");
    await sqlModel.RefreshTableStructuresAsync();
    Check(sqlModel.TableStructureQueries.Contains("PRAGMA table_info(RestoredTable)") && !sqlModel.TableStructureQueries.Any(x => x.Contains("Order Details")), "Refresh drops stale names after database structure changes");
    sqlModel.TablesQueryCommand.Execute(null);
    Check(sqlModel.SqlText == ReadOnlySqlAnalyzerPageModel.TablesSql, "Tables shortcut preserved");
    sqlModel.CountsQueryCommand.Execute(null);
    Check(sqlModel.SqlText == ReadOnlySqlAnalyzerPageModel.CountsSql, "Counts shortcut preserved");

    var savedIncome = new IncomeTransaction { TransactionDate = new(2028, 9, 14, 15, 30, 0), ShopId = setup.Vault, PaymentModeId = setup.Credit, Amount = 600, Note = "  trf  de praburaj vishvanathan  ", BillReferenceNo = "KEEP001" };
    await income.SaveAsync(savedIncome, "existing-source");
    var mapped = new Shop { Name = "Mapped vendor", CategoryId = setup.Category }; await shops.SaveItemAsync(mapped);
    await maps.SaveItemAsync(new EtlTextMap { LookupText = "REF 123.45", ShopId = mapped.Id });
    var rows = new BankRow[]
    {
        new("9.14","TRF DE PRABURAJ VISHVANATHAN",null,"600.00","740.29"),
        new("9.14","TRF INCOME WORDING BUT DEBIT","25.00",null,"715.29"),
        new("9.14","BALANCE ONLY",null,null,"1000.00"),
        new("9.14","ZERO ONLY","0.00","0.00","1000.00"),
        new("9.14","LARGE CREDIT",null,"1999.99","-2500.50"),
        new("9.14","NO BALANCE DEBIT","50.00",null,null),
        new("9.14","SPACE GROUPED CREDIT",null,"2 197.45","1 525.15"),
        new("9.14","REF 123.45 NUMBER","10.00",null,"700.00"),
        new("9.14","CREDIT TAKES PRECEDENCE","20.00","30.00","888.00"),
        new("9.14","COMMA DECIMAL DEBIT","1.234,56",null,"5.000,00"),
        new("9.14","NO BALANCE CREDIT",null,"17.00",null),
        new("9.14","NEGATIVE ONLY","-3.00","-4.00","22.00"),
        new("9.14","TRF DE PRABURAJ VISHVANATHAN",null,"600.00","740.29")
    };
    var fixture = Fixture("classification", 2028, rows);
    var stages = new ConcurrentQueue<string>();
    var first = await importer.ImportAsync(new FileResult(fixture), setup.Debit, setup.Credit, new InlineProgress<string>(stages.Enqueue));
    Check(first is { Imported: 4, IncomeImported: 4, DuplicatesSkipped: 2, InvalidOrErrors: 0 }, $"Normalized date, amount and description skip existing/repeated rows: {first}");
    Check((await income.GetAsync(savedIncome.Id)) is { BillReferenceNo: "KEEP001", Note: "trf  de praburaj vishvanathan" }, "Duplicate detection does not overwrite existing reference or note");
    Check((await bills.ListAsync(2028)).Any(x => x.ShopId == mapped.Id && x.BillValue == 10), "Existing ETL mapping applies to inserted expenses");
    Check((await income.ListAsync(new(2028, 1, 1), new(2029, 1, 1))).Select(x => x.Amount).Order().SequenceEqual(new decimal[] { 600, 1999.99m, 2197.45m, 30, 17 }.Order()), "Credit column remains separate from debit and balance");
    Check((await bills.ListAsync(2028)).Select(x => x.BillValue).Order().SequenceEqual(new decimal[] { 25, 50, 10, 1234.56m }.Order()), "Debit and localized amounts preserve existing parsing");
    Check(stages.Any(x => x.StartsWith("Reading PDF")) && stages.Contains("Parsing transactions...") && stages.Contains("Checking duplicates...") && stages.Any(x => x.StartsWith("Saving transactions")), "Import reports all processing stages");
    var again = await importer.ImportAsync(new FileResult(fixture), setup.Debit, setup.Credit);
    Check(again is { Imported: 0, IncomeImported: 0, DuplicatesSkipped: 10, InvalidOrErrors: 0 }, "Reimport skips every accepted row, including remapped expenses");
    await Sql($"UPDATE income_transaction SET Transaction_date='2028-09-14' WHERE Id={savedIncome.Id}; UPDATE Bill_transcation SET Bill_date='2028-09-14 14:30:00' WHERE Note='REF 123.45 NUMBER';");
    var legacyDates = await importer.ImportAsync(new FileResult(fixture), setup.Debit, setup.Credit);
    Check(legacyDates is { Imported: 0, IncomeImported: 0, DuplicatesSkipped: 10 }, "Date-range lookup includes legacy date-only and space-separated timestamps");

    var overlap = Fixture("overlap", 2028,
    [ new("9.14","TRF DE PRABURAJ VISHVANATHAN",null,"600.00"), new("9.14","NEW CREDIT",null,"601.00"),
      new("9.15","TRF DE PRABURAJ VISHVANATHAN",null,"600.00"), new("9.14","TRF DE PRABURAJ VISHVANATHAN EXTRA",null,"600.00"),
      new("9.14","TRF DE PRABURAJ VISHVANATHAN","600.00",null) ]);
    var overlapResult = await importer.ImportAsync(new FileResult(overlap), setup.Debit, setup.Credit);
    Check(overlapResult is { Imported: 1, IncomeImported: 3, DuplicatesSkipped: 1 }, "Different date, amount, description and type remain distinct");
    var concurrentPdf = Fixture("concurrent", 2031, [new("9.14","CONCURRENT DEBIT","10.00",null), new("9.14","CONCURRENT CREDIT",null,"20.00")]);
    var secondImporter = new EtlPdfImportService(bills, income, shops, categories, modes, context, operations, maps, mapping, NullLogger<EtlPdfImportService>.Instance);
    var concurrent = await Task.WhenAll(importer.ImportAsync(new FileResult(concurrentPdf), setup.Debit, setup.Credit), secondImporter.ImportAsync(new FileResult(concurrentPdf), setup.Debit, setup.Credit));
    Check(concurrent.Sum(x => x.Imported) == 1 && concurrent.Sum(x => x.IncomeImported) == 1 && concurrent.Sum(x => x.DuplicatesSkipped) == 2, "Shared gate serializes overlapping imports across importer instances");
    Check(!operations.IsBusy, "Concurrent imports release critical-operation guards");

    var retryPdf = Fixture("retry", 2032, [new("9.14","RETRY ROW","10.00",null), new("9.14","RETRY ROW","10.00",null)]);
    await Sql("CREATE TRIGGER TestRejectEtl BEFORE INSERT ON Bill_transcation WHEN NEW.Note='RETRY ROW' BEGIN SELECT RAISE(ABORT,'Injected save failure'); END;");
    var rejected = await importer.ImportAsync(new FileResult(retryPdf), setup.Debit, setup.Credit);
    Check(rejected is { Imported: 0, DuplicatesSkipped: 0, InvalidOrErrors: 2 }, "Failed saves are errors, not duplicate candidates");
    await Sql("DROP TRIGGER TestRejectEtl;");
    var retried = await importer.ImportAsync(new FileResult(retryPdf), setup.Debit, setup.Credit);
    Check(retried is { Imported: 1, DuplicatesSkipped: 1, InvalidOrErrors: 0 }, "Same-PDF repeats skip only after a successful insertion");
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel(); await Reject(() => importer.ImportAsync(new FileResult(fixture), setup.Debit, setup.Credit, cancellationToken: cancelled.Token), "Cancelled import never starts");
    }
    using (var cancelParsing = new CancellationTokenSource())
    {
        var progress = new InlineProgress<string>(value => { if (value == "Parsing transactions...") cancelParsing.Cancel(); });
        await Reject(() => importer.ImportAsync(new FileResult(fixture), setup.Debit, setup.Credit, progress, cancelParsing.Token), "Cancellation interrupts parsing");
    }
    Check(!operations.IsBusy, "Cancellation releases the gate and screen-operation guard");
    var opened = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var heldPdf = new FileResult(concurrentPdf) { Reader = async () => { opened.SetResult(); await release.Task; return File.OpenRead(concurrentPdf); } };
    var heldImport = importer.ImportAsync(heldPdf, setup.Debit, setup.Credit);
    await opened.Task.WaitAsync(TimeSpan.FromSeconds(10));
    try
    {
        using var queuedCancellation = new CancellationTokenSource();
        var queued = secondImporter.ImportAsync(new FileResult(concurrentPdf), setup.Debit, setup.Credit, cancellationToken: queuedCancellation.Token);
        queuedCancellation.Cancel();
        await Reject(() => queued, "Waiting import can be cancelled without releasing another import's gate");
        Check(operations.IsBusy, "Active import keeps its critical-operation guard after queued cancellation");
    }
    finally { release.SetResult(); await heldImport; }
    Check(!operations.IsBusy, "Queued cancellation and active import both clean up");

    var secondCompany = await accounts.CreateAsync(new FinancialAccount { Name = "Second company", CurrencyCode = "EUR" }, false);
    context.Select(secondCompany); var secondSetup = await Setup();
    var isolated = await importer.ImportAsync(new FileResult(concurrentPdf), secondSetup.Debit, secondSetup.Credit);
    Check(isolated is { Imported: 1, IncomeImported: 1, DuplicatesSkipped: 0 }, "Duplicate checks are isolated by company");
    context.Select(firstCompany);

    var large = Fixture("large", 2033, Enumerable.Range(0, 300).Select(i => new BankRow("9.14", $"LOAD ROW {i:0000}", "1.00", null)).ToArray());
    var errors = new ModalErrorHandler();
    await UiPump.RunAsync(async () =>
    {
        var uiThread = Environment.CurrentManagedThreadId;
        var workerThread = 0;
        var pdf = new FileResult(large) { Reader = () => { workerThread = Environment.CurrentManagedThreadId; return Task.FromResult<Stream>(File.OpenRead(large)); } };
        FilePicker.Default.Next = pdf;
        var model = new EtlDataPageModel(importer, modes, categories, shops, context, operations, errors);
        await model.LoadPaymentModesAsync();
        model.SelectedExpensePaymentMode = model.PaymentModes.Single(x => x.Id == setup.Debit);
        model.SelectedIncomePaymentMode = model.PaymentModes.Single(x => x.Id == setup.Credit);
        var uiChanges = true; var visibleStages = new List<string>();
        model.PropertyChanged += (_, e) => { uiChanges &= Environment.CurrentManagedThreadId == uiThread; if (e.PropertyName == nameof(model.StatusText)) visibleStages.Add(model.StatusText); };
        var importing = model.ImportCommand.ExecuteAsync(null);
        var ticks = 0;
        while (!importing.IsCompleted) { await Task.Delay(10); ticks++; if (ticks > 3000) throw new TimeoutException("Import did not complete."); }
        await importing;
        Check(workerThread != uiThread && ticks > 0, "Large PDF runs on a worker while UI remains responsive");
        Check(uiChanges && visibleStages.Any(x => x.StartsWith("Reading PDF")), "Progress updates marshal to the UI synchronization context");
        Check(model.StatusText == "Import completed." && model.SummaryText.Contains("Expenses: 300") && !model.IsBusy && !operations.IsBusy, "Success unlocks screen and retains accurate summary");
        await Task.Delay(30);
        Check(model.StatusText == "Import completed.", "Delayed progress cannot overwrite final status");
        FilePicker.Default.Next = null;
        await model.ImportCommand.ExecuteAsync(null);
        Check(!model.IsBusy && !operations.IsBusy, "Cancelled PDF selection never locks the screen");
        var badPath = Path.Combine(scratch.FullName, "bad.pdf"); await File.WriteAllTextAsync(badPath, "Not a PDF");
        FilePicker.Default.Next = new FileResult(badPath);
        await model.ImportCommand.ExecuteAsync(null);
        Check(model.StatusText == "Import failed." && !model.IsBusy && !operations.IsBusy && errors.Errors.Count == 1, "Invalid PDF releases the overlay and reports failure");
    });
    Console.WriteLine($"{passed} SQL/ETL regression checks passed.");

    async Task<(int Vault, int Category, int Debit, int Credit)> Setup()
    {
        var category = new Category { Name = "ETL_DT", IconKey = "default_shop" }; await categories.InsertAsync(category);
        var vault = new Shop { Name = "ETL_DATA_VAULT", CategoryId = category.Id }; await shops.SaveItemAsync(vault);
        var debit = new PaymentMode { Name = "PDF Debit" }; var credit = new PaymentMode { Name = "PDF Credit" };
        await modes.SaveItemAsync(debit); await modes.SaveItemAsync(credit);
        return (vault.Id, category.Id, debit.Id, credit.Id);
    }
}
finally
{
    SqliteConnection.ClearAllPools();
    if (Path.GetFullPath(scratch.FullName).StartsWith(Path.GetFullPath(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase)) scratch.Delete(true);
}

void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); passed++; Console.WriteLine("PASS: " + message); }
async Task Reject(Func<Task> action, string message) { var rejected = false; try { await action(); } catch (OperationCanceledException) { rejected = true; } Check(rejected, message); }
async Task Sql(string sql) { await using var db = new SqliteConnection(Constants.DatabasePath); await db.OpenAsync(); await using var command = db.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
string Fixture(string name, int year, IReadOnlyList<BankRow> rows)
{
    using var builder = new PdfDocumentBuilder();
    var font = builder.AddStandard14Font(Standard14Font.Courier);
    var page = builder.AddPage(595, 842);
    void Text(string text, double x, double y) => page.AddText(text, 8, new PdfPoint(x, y), font);
    void Right(string? text, double x, double y) { if (!string.IsNullOrEmpty(text)) Text(text, x - text.Length * 4.8, y); }
    void Header() { Text("DATA", 55, 765); Text("DATA", 85, 765); Text("DESCRITIVO", 120, 765); Right("DEBITO", 385, 765); Right("CREDITO", 465, 765); Right("SALDO", 555, 765); Text("LANC.", 55, 755); Text("VALOR", 85, 755); Text("TRANSPORTE", 120, 755); }
    Text($"EXTRATO DE {year}", 55, 790); Header(); var y = 735.0;
    foreach (var row in rows)
    {
        if (y < 150) { page = builder.AddPage(595, 842); Header(); y = 735; }
        Text(row.Date, 55, y); Text(row.Date, 85, y); Text(row.Description, 120, y); Right(row.Debit, 385, y); Right(row.Credit, 465, y); Right(row.Balance, 555, y); y -= 16;
    }
    Text("SALDO FINAL", 120, y - 16);
    var path = Path.Combine(scratch.FullName, name + ".pdf"); File.WriteAllBytes(path, builder.Build()); return path;
}
sealed record BankRow(string Date, string Description, string? Debit, string? Credit, string? Balance = null);
