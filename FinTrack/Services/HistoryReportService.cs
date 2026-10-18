using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Utilities;

namespace FinTracker.Services;

/// <summary>Read-only history built from the same company-scoped repositories as entry and analytics.</summary>
public sealed class HistoryReportService(BillTransactionRepository bills, IncomeTransactionRepository income, FinancialAccountContext accounts)
{
    public async Task<HistoryReport> SearchAsync(FinancialTransactionType type, HistorySearchMode mode, int selectedId,
        string selectionName, IEnumerable<int> selectedYears, HistoryBillOrder order, CancellationToken token = default)
    {
        var account = accounts.Current ?? throw new InvalidOperationException("Choose a company.");
        var years = selectedYears.Distinct().OrderByDescending(x => x).ToArray();
        var currentYear = DateTime.Today.Year;
        if (selectedId <= 0 || years.Length == 0 || years.Any(x => x < currentYear - 4 || x > currentYear))
            throw new ArgumentException("Select an item and at least one of the available five years.");
        var rows = new List<HistoryBill>();
        foreach (var year in years)
        {
            token.ThrowIfCancellationRequested();
            if (accounts.Current?.Id != account.Id) throw new OperationCanceledException("The selected company changed.");
            var start = new DateTime(year, 1, 1); var end = start.AddYears(1);
            if (type == FinancialTransactionType.Expense)
            {
                var items = await bills.GetBillsForDateRangeAsync(start, end);
                rows.AddRange(items.Where(x => mode == HistorySearchMode.Shop ? x.ShopId == selectedId : x.ShopCategoryId == selectedId)
                    .Select(x => new HistoryBill(x.Id, x.ShopId, x.ShopName, x.ShopCategoryId, x.BillDate, x.PaymentModeName, x.BillReferenceNo, x.Note, x.BillValue)));
            }
            else
            {
                var items = await income.ListAsync(start, end, mode == HistorySearchMode.Shop ? selectedId : null);
                rows.AddRange(items.Where(x => mode == HistorySearchMode.Shop || x.CategoryId == selectedId)
                    .Select(x => new HistoryBill(x.Id, x.ShopId, x.ShopName, x.CategoryId, x.TransactionDate, x.PaymentModeName, x.BillReferenceNo, x.Note, x.Amount)));
            }
            token.ThrowIfCancellationRequested();
            if (accounts.Current?.Id != account.Id) throw new OperationCanceledException("The selected company changed.");
        }
        return new HistoryReport(account.Id, account.Name, account.CurrencyCode, type, mode, selectionName, years, order, HistoryGrouping.Build(rows, mode, order));
    }
}
