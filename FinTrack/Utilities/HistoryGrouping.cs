using FinTracker.Models;

namespace FinTracker.Utilities;

public static class HistoryGrouping
{
    public static IOrderedEnumerable<HistoryBill> Order(IEnumerable<HistoryBill> bills, HistoryBillOrder order) => (order switch
    {
        HistoryBillOrder.DateNewest => bills.OrderByDescending(x => x.Date),
        HistoryBillOrder.DateOldest => bills.OrderBy(x => x.Date),
        HistoryBillOrder.AmountHighest => bills.OrderByDescending(x => x.Amount).ThenByDescending(x => x.Date),
        HistoryBillOrder.AmountLowest => bills.OrderBy(x => x.Amount).ThenByDescending(x => x.Date),
        _ => throw new ArgumentOutOfRangeException(nameof(order))
    }).ThenBy(x => x.Id);

    public static IReadOnlyList<HistoryGroup> Build(IEnumerable<HistoryBill> bills, HistorySearchMode mode, HistoryBillOrder order)
    {
        var rows = bills.ToArray();
        if (mode == HistorySearchMode.Category)
            return rows.GroupBy(x => x.ShopId).Select(g => new HistoryGroup(g.First().ShopName, Order(g, order).ToArray(), []))
                .OrderBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ToArray();
        return rows.GroupBy(x => x.Date.Year).OrderByDescending(g => g.Key)
            .Select(year => new HistoryGroup(year.Key.ToString(), [], year.GroupBy(x => x.Date.Month).OrderByDescending(g => g.Key)
                .Select(month => new HistoryGroup(new DateTime(year.Key, month.Key, 1).ToString("MMMM"), Order(month, order).ToArray(), [])).ToArray())).ToArray();
    }
}
