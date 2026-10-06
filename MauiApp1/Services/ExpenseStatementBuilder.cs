using MauiApp1.Models;
using MauiApp1.Utilities;

namespace MauiApp1.Services;

public sealed class ExpenseStatementBuilder(ICurrencyService currencies) : IExpenseStatementBuilder
{
    public ExpenseStatementSnapshot Build(
        ExpensePeriodType periodType,
        string periodTitle,
        DateTime startDate,
        DateTime endExclusive,
        IEnumerable<CategoryExpenseGroup> groups,
        string? statementTitle = null,
        IEnumerable<ShopExpenseChartData>? shopVisits = null)
    {
        var serialNumber = 0;
        var categories = groups.Select(group =>
        {
            var rows = ReportBillOrdering.Order(group.Bills)
                .Select(bill => new ExpenseStatementRow
                {
                    SerialNumber = ++serialNumber,
                    BillDate = bill.BillDate,
                    ShopName = bill.ShopName,
                    PaymentModeName = bill.PaymentModeName,
                    Value = bill.BillValue,
                    ValueText = EuroFormatter.Format(bill.BillValue)
                })
                .ToArray();

            return new ExpenseStatementCategory
            {
                CategoryName = group.CategoryName,
                Rows = rows,
                CategoryTotalText = EuroFormatter.Format(rows.Sum(row => row.Value))
            };
        }).Where(category => category.Rows.Count > 0).ToArray();

        var currency = currencies.CurrentCurrency;
        return new ExpenseStatementSnapshot
        {
            PeriodType = periodType,
            PeriodTitle = periodTitle,
            StatementTitle = statementTitle ?? (periodType switch
            {
                ExpensePeriodType.Daily => "Daily Expense Statement",
                ExpensePeriodType.Weekly => "Weekly Expense Statement",
                ExpensePeriodType.Monthly => "Monthly Expense Statement",
                ExpensePeriodType.Yearly => "Yearly Expense Statement",
                _ => "Expense Statement"
            }),
            StartDate = startDate,
            EndExclusive = endExclusive,
            CurrencyCode = currency.Code,
            CurrencySymbol = currency.Symbol,
            Categories = categories,
            ShopVisits = shopVisits?.ToArray() ?? [],
            OverallTotalText = EuroFormatter.Format(categories.Sum(category => category.Rows.Sum(row => row.Value)))
        };
    }
}
