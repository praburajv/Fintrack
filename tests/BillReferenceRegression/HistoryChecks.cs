using System.Text;
using System.Xml.Linq;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Services;
using FinTracker.Utilities;

internal static class HistoryChecks
{
    public static async Task Run(BillTransactionRepository bills, IncomeTransactionRepository income, ShopRepository shops,
        CategoryRepository categories, PaymentModeRepository modes, FinancialAccountContext accounts, FinancialAccountRepository companies, Action<bool, string> check)
    {
        var original = accounts.Current!;
        var company = await companies.CreateAsync(new FinancialAccount { Name = "History Verification", CurrencyCode = "EUR" }, true);
        accounts.Select(company);
        try
        {
            var category = (await categories.ListAsync()).First();
            var mode = new PaymentMode { Name = "History Cash" };
            await modes.SaveItemAsync(mode);
            var first = new Shop { Name = "Alpha History", CategoryId = category.Id };
            var second = new Shop { Name = "Beta History", CategoryId = category.Id };
            await shops.SaveItemAsync(first); await shops.SaveItemAsync(second);
            var year = DateTime.Today.Year;
            foreach (var (shop, date, amount, reference) in new[]
            {
                (first, new DateTime(year, 1, 2), 10m, "A_!@#&-001"),
                (first, new DateTime(year, 1, 2), 10m, "A_!@#&-002"),
                (first, new DateTime(year, 3, 4), 30m, "A_!@#&-003"),
                (second, new DateTime(year - 2, 12, 31), 40m, "B001"),
                (first, new DateTime(year - 1, 1, 1), 100m, "EXCLUDED")
            })
            {
                await bills.SaveItemAsync(new BillTransaction { BillDate = date, ShopId = shop.Id, PaymentModeId = mode.Id, BillValue = amount, BillReferenceNo = reference, Note = "History fixture" });
                await income.SaveAsync(new IncomeTransaction { TransactionDate = date, ShopId = shop.Id, PaymentModeId = mode.Id, Amount = amount * 2, BillReferenceNo = reference, Note = "Income history fixture" });
            }
            var history = new HistoryReportService(bills, income, accounts);
            foreach (var type in Enum.GetValues<FinancialTransactionType>())
            {
                var multiple = type == FinancialTransactionType.Expense ? 1m : 2m;
                foreach (var order in Enum.GetValues<HistoryBillOrder>())
                {
                    var report = await history.SearchAsync(type, HistorySearchMode.Category, category.Id, category.Name, [year, year - 2, year], order);
                    check(report.Count == 4 && report.Total == 90m * multiple && report.Years.SequenceEqual(new[] { year, year - 2 }), "History exact nonconsecutive year selection and deduplication reconcile");
                    check(report.Groups.Count == 2 && report.Groups.All(x => x.Children.Count == 0), "Category history groups by shop with complete bills");
                    var alpha = report.Groups.Single(x => x.Title == first.Name);
                    check(alpha.Total == 50 * multiple && alpha.Count == 3 && alpha.Bills.All(x => x.ShopId == first.Id), "Shop subtotals match their grouped transactions");
                    var expected = order switch
                    {
                        HistoryBillOrder.AmountHighest => alpha.Bills.OrderByDescending(x => x.Amount).ThenByDescending(x => x.Date).ThenBy(x => x.Id),
                        HistoryBillOrder.AmountLowest => alpha.Bills.OrderBy(x => x.Amount).ThenByDescending(x => x.Date).ThenBy(x => x.Id),
                        HistoryBillOrder.DateOldest => alpha.Bills.OrderBy(x => x.Date).ThenBy(x => x.Id),
                        _ => alpha.Bills.OrderByDescending(x => x.Date).ThenBy(x => x.Id)
                    };
                    check(alpha.Bills.Select(x => x.Id).SequenceEqual(expected.Select(x => x.Id)), "History sorting is stable across duplicate dates and amounts");
                    var shopReport = await history.SearchAsync(type, HistorySearchMode.Shop, first.Id, first.Name, [year, year - 1], order);
                    check(shopReport.Count == 4 && shopReport.Total == 150m * multiple && shopReport.Groups.Select(x => x.Title).SequenceEqual(new[] { year.ToString(), (year - 1).ToString() }), "Shop history isolates the selected shop and orders year groups");
                    check(shopReport.Groups.First().Children.Count == 2 && shopReport.Groups.First().Total == 50m * multiple, "Shop history month subtotals reconcile to year totals");
                    check(shopReport.Groups.Sum(x => x.Count) == shopReport.Count && shopReport.Groups.SelectMany(x => x.Children).Sum(x => x.Total) == shopReport.Total, "History nested totals do not double count rows");
                }
                var pdfReport = await history.SearchAsync(type, HistorySearchMode.Category, category.Id, category.Name, [year, year - 2], HistoryBillOrder.AmountLowest);
                var pdf = await new ExpenseStatementDocumentService().CreateHistoryPdfAsync(pdfReport);
                var text = Encoding.ASCII.GetString(await File.ReadAllBytesAsync(pdf));
                check(Path.GetFileName(pdf).StartsWith(type + "-History-") && text.StartsWith("%PDF-1.4"), "History PDF uses the correct transaction type and valid header");
                check(text.Contains("A_!@#&-001") && text.Contains("A_!@#&-002") && text.Contains("B001") && !text.Contains("EXCLUDED"), "History export includes all selected bills and symbols, excluding unselected years");
                check(text.Contains("History Verification") && text.Contains("Selected years: " + year) && text.Contains("Amount lowest first"), "History PDF preserves company, years and selected ordering");
                var evidence = Path.Combine(Path.GetTempPath(), $"FinTrack-History-{type}-verification.pdf");
                File.Copy(pdf, evidence, true);
            }
            var longBill = new HistoryBill(1, 1, new string('W', 180), 1, DateTime.Today, "Cash", "!@#&-_", new string('A', 1000), 12.34m);
            var longReport = new HistoryReport(company.Id, company.Name, "EUR", FinancialTransactionType.Income, HistorySearchMode.Shop, first.Name, [year], HistoryBillOrder.DateNewest,
                HistoryGrouping.Build(Enumerable.Range(1, 100).Select(id => longBill with { Id = id }), HistorySearchMode.Shop, HistoryBillOrder.DateNewest));
            var longPdf = await new ExpenseStatementDocumentService().CreateHistoryPdfAsync(longReport);
            check(Encoding.ASCII.GetString(await File.ReadAllBytesAsync(longPdf)).Split("/Type /Page ").Length > 3, "History PDF paginates large grouped results and long text");
            File.Copy(longPdf, Path.Combine(Path.GetTempPath(), "FinTrack-History-long-verification.pdf"), true);
            var selections = Enumerable.Range(1, 120).Select(i => (object)new Shop { Id = i, Name = $"Shop {i:000}", CategoryName = "Category" }).ToArray();
            check(SelectionSearch.Filter(selections, "").Count() == 120, "Empty selector search returns the complete list without a fixed result cap");
            check(SelectionSearch.Filter(selections, " sHoP 01 ").Count() == 10, "Selector prefix filtering ignores case and surrounding spaces");
            check(!SelectionSearch.Filter(selections, "No match").Any() && SelectionSearch.Filter(selections, "").Count() == 120, "No matches and clearing restore the complete selector list");
            check(SelectionSearch.Detail(selections[0]) == "Category" && SelectionSearch.Name(new Category { Name = "Travel" }) == "Travel", "Selectors expose shop category detail and category names");
            check(SelectionSearch.SameItem(new Shop { Id = 1, FinancialAccountId = 2 }, new Shop { Id = 1, FinancialAccountId = 2 })
                && !SelectionSearch.SameItem(new Shop { Id = 1, FinancialAccountId = 2 }, new Shop { Id = 1, FinancialAccountId = 3 }), "Refreshing selector lists preserves IDs within the same company only");
            using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            try { await history.SearchAsync(FinancialTransactionType.Income, HistorySearchMode.Shop, first.Id, first.Name, [year], HistoryBillOrder.DateNewest, cancelled.Token); check(false, "Cancellation"); }
            catch (OperationCanceledException) { check(true, "Cancelled history requests produce no report"); }
            foreach (var years in new[] { Array.Empty<int>(), new[] { year - 5 }, new[] { year + 1 } })
            {
                try { await history.SearchAsync(FinancialTransactionType.Income, HistorySearchMode.Shop, first.Id, first.Name, years, HistoryBillOrder.DateNewest); check(false, "Invalid history years"); }
                catch (ArgumentException) { check(true, "History rejects empty and unavailable year selections"); }
            }
            accounts.Select(original);
            var isolated = await history.SearchAsync(FinancialTransactionType.Income, HistorySearchMode.Shop, first.Id, first.Name, [year], HistoryBillOrder.DateNewest);
            check(isolated.Count == 0, "History cannot read another company's transaction IDs");
        }
        finally { accounts.Select(original); }

        var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));
        var app = Path.Combine(root, "FinTrack");
        XNamespace maui = "http://schemas.microsoft.com/dotnet/2021/maui";
        var entry = XDocument.Load(Path.Combine(app, "Pages/BillEntryPage.xaml"));
        check(entry.Descendants().Any(x => x.Name.LocalName == "SearchableSelector" && ((string?)x.Attribute("SelectedItem"))?.Contains("Shop") == true), "Shared income/expense entry binds the selected shop object");
        var dashboard = XDocument.Load(Path.Combine(app, "Pages/FinancialDashboardPage.xaml"));
        check(dashboard.Descendants().Any(x => x.Name.LocalName == "IncomeCategoryChart" && (string?)x.Attribute("CategorySelected") == "OnIncomeChartCategorySelected"), "Dashboard income pie has independent income navigation");
        check(File.ReadAllText(Path.Combine(app, "PageModels/FinancialDashboardPageModel.cs")).Contains("&type=Income"), "Income category drilldown retains the income transaction type");
        check(File.ReadAllText(Path.Combine(app, "Pages/ReportsHubPage.cs")).Contains("history?type={type}&mode={mode}"), "Report hub links preserve history transaction type and mode");
    }
}
