using MauiApp1.Models;

namespace MauiApp1.Services;

public interface IExpenseStatementBuilder
{
    ExpenseStatementSnapshot Build(
        ExpensePeriodType periodType,
        string periodTitle,
        DateTime startDate,
        DateTime endExclusive,
        IEnumerable<CategoryExpenseGroup> groups,
        string? statementTitle = null,
        IEnumerable<ShopExpenseChartData>? shopVisits = null);
}
