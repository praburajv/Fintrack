using FinTracker.Data;
using FinTracker.Models;

namespace FinTracker.Services;

public sealed class ShopBillAnalysisService(ShopRepository shops, BillTransactionRepository bills, IncomeTransactionRepository income)
{
    public async Task<(IReadOnlyList<BillTransaction> Expenses, IReadOnlyList<BillTransaction> Income)> GetBillsAsync(int shopId, int year, string? reference)
    {
        var expenses = await bills.GetBillsByShopAndYearAsync(shopId, year);
        var credits = await income.ListAsync(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1), shopId);
        bool Matches(string? value) => string.IsNullOrEmpty(reference) || string.Equals(value, reference, StringComparison.OrdinalIgnoreCase);
        return (expenses.Where(x => Matches(x.BillReferenceNo)).OrderByDescending(x => x.BillDate).ThenByDescending(x => x.Id).ToArray(),
            credits.Where(x => Matches(x.BillReferenceNo)).OrderByDescending(x => x.TransactionDate).ThenByDescending(x => x.Id).Select(x => new BillTransaction
            {
                Id = x.Id, BillDate = x.TransactionDate, BillValue = x.Amount, Note = x.Note,
                BillReferenceNo = x.BillReferenceNo, ShopId = x.ShopId, ShopName = x.ShopName,
                PaymentModeId = x.PaymentModeId, PaymentModeName = x.PaymentModeName,
                ShopCategoryId = x.CategoryId, ShopCategoryName = x.CategoryName, ShopCategoryIconKey = x.CategoryIconKey,
                TransactionType = FinancialTransactionType.Income
            }).ToArray());
    }

    public async Task<ShopBillAnalysisResult> GetYearAnalysisAsync(int shopId, int year)
    {
        if (shopId <= 0) throw new ArgumentOutOfRangeException(nameof(shopId));
        if (year is < 1 or > 9998) throw new ArgumentOutOfRangeException(nameof(year));

        var shopTask = shops.GetAsync(shopId);
        var visitsTask = bills.GetShopMonthlyVisitsAsync(shopId, year);
        await Task.WhenAll(shopTask, visitsTask);
        var shop = await shopTask ?? throw new InvalidOperationException("The selected shop no longer exists.");
        var monthly = await visitsTask;
        var visitCount = monthly.Sum(item => item.VisitCount);
        var totalSpent = monthly.Sum(item => item.TotalSpent);
        return new ShopBillAnalysisResult
        {
            ShopId = shop.Id,
            ShopName = shop.Name,
            Year = year,
            VisitCount = visitCount,
            TotalSpent = totalSpent,
            MonthlyVisits = monthly
        };
    }

    public async Task<IReadOnlyList<int>> GetAvailableYearsAsync(int shopId)
    {
        var years = await bills.GetShopTransactionYearsAsync(shopId);
        years.AddRange((await income.ListAsync(DateTime.MinValue, DateTime.MaxValue, shopId)).Select(x => x.TransactionDate.Year));
        if (!years.Contains(DateTime.Today.Year)) years.Add(DateTime.Today.Year);
        return years.Distinct().OrderByDescending(year => year).ToArray();
    }

    public async Task<ShopBillAnalysisResult> GetIncomeYearAnalysisAsync(int shopId, int year)
    {
        var shop = await shops.GetAsync(shopId) ?? throw new InvalidOperationException("The selected shop no longer exists.");
        var rows = (await income.ListAsync(new DateTime(year, 1, 1), new DateTime(year + 1, 1, 1), shopId)).ToArray();
        var monthly = Enumerable.Range(1, 12).Select(month => new MonthlyVisitData
        {
            MonthNumber = month,
            MonthName = new DateTime(2000, month, 1).ToString("MMM", System.Globalization.CultureInfo.CurrentCulture),
            VisitCount = rows.Count(x => x.TransactionDate.Month == month),
            TotalSpent = rows.Where(x => x.TransactionDate.Month == month).Sum(x => x.Amount),
            Color = ChartColorPalette.ForMonth(month)
        }).ToArray();
        return new ShopBillAnalysisResult { ShopId = shop.Id, ShopName = shop.Name, Year = year,
            VisitCount = rows.Length, TotalSpent = rows.Sum(x => x.Amount), MonthlyVisits = monthly };
    }

    public async Task<(Shop Shop, IReadOnlyList<ShopTransactionDateSummary> Dates)> GetIncomeTransactionDatesAsync(int shopId, int year, int month)
    {
        var shop = await shops.GetAsync(shopId) ?? throw new InvalidOperationException("The selected shop no longer exists.");
        var from = new DateTime(year, month, 1);
        var rows = (await income.ListAsync(from, from.AddMonths(1), shopId));
        var dates = rows.GroupBy(x => x.TransactionDate.Date).OrderBy(x => x.Key)
            .Select(group => new ShopTransactionDateSummary { Date = group.Key, BillCount = group.Count(), TotalSpent = group.Sum(x => x.Amount) }).ToArray();
        return (shop, dates);
    }

    public async Task<(Shop Shop, IReadOnlyList<ShopTransactionDateSummary> Dates)> GetTransactionDatesAsync(int shopId, int year, int month)
    {
        if (shopId <= 0) throw new ArgumentOutOfRangeException(nameof(shopId));
        if (year is < 1 or > 9998) throw new ArgumentOutOfRangeException(nameof(year));
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        var shopTask = shops.GetAsync(shopId);
        var datesTask = bills.GetShopTransactionDatesAsync(shopId, year, month);
        await Task.WhenAll(shopTask, datesTask);
        return (await shopTask ?? throw new InvalidOperationException("The selected shop no longer exists."), await datesTask);
    }
}
