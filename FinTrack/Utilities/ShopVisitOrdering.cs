using FinTracker.Models;

namespace FinTracker.Utilities;

public static class ShopVisitOrdering
{
    public static ShopExpenseChartData[] Order(IEnumerable<ShopExpenseChartData> shops, bool ascending) =>
        (ascending ? shops.OrderBy(shop => shop.VisitCount) : shops.OrderByDescending(shop => shop.VisitCount))
            .ThenBy(shop => shop.ShopName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(shop => shop.ShopId).ToArray();
}
