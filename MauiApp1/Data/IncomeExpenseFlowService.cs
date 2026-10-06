using MauiApp1.Models;

namespace MauiApp1.Data;

public sealed class IncomeExpenseFlowService(BillTransactionRepository bills, IncomeTransactionRepository income)
{
    public async Task<List<FinancialFlowMonth>> LoadAsync(int year, int? month)
    {
        var expenseRows = await bills.GetExpensesByMonthAsync(year);
        var incomeRows = await income.GetMonthlyChartAsync(year);
        var rows = Enumerable.Range(1, 12).Select(index => new FinancialFlowMonth
        {
            Year = year,
            MonthNumber = index,
            SerialNumber = index,
            Income = incomeRows.First(item => item.MonthNumber == index).TotalValue,
            Expense = expenseRows.First(item => item.MonthNumber == index).TotalValue
        });
        return (month.HasValue ? rows.Where(item => item.MonthNumber == month.Value) : rows).ToList();
    }
}
