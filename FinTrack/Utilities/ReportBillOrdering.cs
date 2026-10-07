using FinTracker.Models;

namespace FinTracker.Utilities;

public static class ReportBillOrdering
{
    public static IOrderedEnumerable<BillTransaction> Order(IEnumerable<BillTransaction> bills) =>
        bills.OrderBy(bill => bill.ShopName, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(bill => bill.BillDate)
            .ThenByDescending(bill => bill.Id);
}
