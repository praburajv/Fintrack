using MauiApp1.Models;
using System.Collections.ObjectModel;

namespace MauiApp1.Utilities;

public static class CategoryExpenseGrouping
{
    public static ObservableCollection<CategoryExpenseGroup> Group(IEnumerable<BillTransaction> bills)
    {
        var groups = bills.GroupBy(bill => bill.ShopCategoryId)
            .Select(group =>
            {
                var first = group.First();
                var item = new CategoryExpenseGroup
                {
                    CategoryId = group.Key,
                    CategoryName = string.IsNullOrWhiteSpace(first.ShopCategoryName) ? "Uncategorized" : first.ShopCategoryName,
                    IconFileName = CategoryIconCatalog.FileNameFor(first.ShopCategoryIconKey),
                    IsIncome = first.TransactionType == FinancialTransactionType.Income
                };
                foreach (var bill in group.OrderByDescending(x => x.BillDate).ThenByDescending(x => x.Id))
                    item.Bills.Add(bill);
                return item;
            })
            .OrderByDescending(group => group.TotalExpense)
            .ThenBy(group => group.CategoryName);

        return new ObservableCollection<CategoryExpenseGroup>(groups);
    }
}
