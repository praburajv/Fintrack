using FinTracker.Data;
using FinTracker.Models;

namespace FinTracker.Services;

public sealed class ShopBillAnalysisService(ShopRepository shops, BillTransactionRepository bills)
{
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
        if (!years.Contains(DateTime.Today.Year)) years.Add(DateTime.Today.Year);
        return years.Distinct().OrderByDescending(year => year).ToArray();
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
