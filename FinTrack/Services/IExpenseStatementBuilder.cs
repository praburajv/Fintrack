using FinTracker.Models;

namespace FinTracker.Services;

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
