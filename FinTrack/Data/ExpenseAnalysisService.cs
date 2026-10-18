using FinTracker.Models;

namespace FinTracker.Data;

public sealed record ExpenseAnalysisResult(
    IReadOnlyList<CategoryExpenseGroup> Groups,
    decimal OverallTotal,
    IReadOnlyList<CategoryExpenseGroup> IncomeGroups,
    decimal OverallIncome);
public sealed record MonthlyBudgetAnalysisResult(IReadOnlyList<CategoryExpenseGroup> Groups, MonthlyBudgetSummary Summary);

public sealed class ExpenseAnalysisService(BillTransactionRepository bills, IncomeTransactionRepository income, CategoryRepository categories, SetUpKeyValueRepository settings)
{
    public Task<List<PaymentModeExpenseChartData>> GetPaymentModeExpenseTotalsAsync(DateTime startDate, DateTime endExclusive) => bills.GetPaymentModeExpenseTotalsAsync(startDate, endExclusive);
    public Task<List<ShopExpenseChartData>> GetShopVisitsAsync(DateTime startDate, DateTime endExclusive) => bills.GetExpensesByShopAsync(startDate, endExclusive);
    public async Task<MonthlyBudgetAnalysisResult> GetMonthlyBudgetAnalysisAsync(DateTime startDate, DateTime endExclusive)
    {
        var categoryList = await categories.ListAsync();
        var budgetSettings = await settings.GetBudgetSettingsAsync();
        var categoryById = categoryList.ToDictionary(x => x.Id);
        var transactions = await bills.GetBillsForDateRangeAsync(startDate, endExclusive);
        var groups = categoryList.ToDictionary(x => x.Id, x => new CategoryExpenseGroup
        {
            CategoryId = x.Id, CategoryName = x.Name, IconFileName = CategoryIconCatalog.FileNameFor(x.IconKey), Budgeted = x.Budgeted
        });

        foreach (var bill in transactions)
        {
            var category = bill.ShopCategoryId > 0 && categoryById.TryGetValue(bill.ShopCategoryId, out var found) ? found : null;
            var categoryId = category?.Id ?? 0;
            if (!groups.TryGetValue(categoryId, out var group))
            {
                group = new CategoryExpenseGroup { CategoryId = categoryId, CategoryName = category?.Name ?? bill.ShopCategoryName, IconFileName = CategoryIconCatalog.FileNameFor(category?.IconKey ?? bill.ShopCategoryIconKey), Budgeted = category?.Budgeted ?? 0 };
                groups[categoryId] = group;
            }
            group.Bills.Add(bill);
        }

        var visible = groups.Values.Where(x => x.HasBudget || x.HasExpense).OrderByDescending(x => x.TotalExpense).ThenBy(x => x.CategoryName).ToList();
        var highest = visible.Where(x => x.HasExpense).Select(x => x.TotalExpense).DefaultIfEmpty().Max();
        foreach (var group in visible) group.IsHighest = group.HasExpense && group.TotalExpense == highest;
        var actualIncome = (await income.GetMonthlyTotalsAsync(startDate, endExclusive)).Values.Sum();
        var summary = new MonthlyBudgetSummary(
            actualIncome,
            budgetSettings.MonthlyBudget,
            transactions.Sum(x => x.BillValue));
        return new MonthlyBudgetAnalysisResult(visible, summary);
    }

    public async Task<ExpenseAnalysisResult> GetExpenseAnalysisAsync(DateTime startDate, DateTime endExclusive)
    {
        var categoryById = (await categories.ListAsync()).ToDictionary(x => x.Id);
        var transactions = await bills.GetBillsForDateRangeAsync(startDate, endExclusive);
        var groups = new Dictionary<int, CategoryExpenseGroup>();

        foreach (var bill in transactions)
        {
            var category = bill.ShopCategoryId > 0 && categoryById.TryGetValue(bill.ShopCategoryId, out var found) ? found : null;
            var categoryId = category?.Id ?? 0;
            if (!groups.TryGetValue(categoryId, out var group))
            {
                group = new CategoryExpenseGroup
                {
                    CategoryId = categoryId,
                    CategoryName = category?.Name ?? bill.ShopCategoryName,
                    IconFileName = CategoryIconCatalog.FileNameFor(category?.IconKey ?? bill.ShopCategoryIconKey),
                    Budgeted = category?.Budgeted ?? 0
                };
                groups.Add(categoryId, group);
            }
            group.Bills.Add(bill);
        }

        var ordered = groups.Values.OrderByDescending(x => x.TotalExpense).ThenBy(x => x.CategoryName).ToList();
        var highest = ordered.FirstOrDefault()?.TotalExpense;
        foreach (var group in ordered)
            group.IsHighest = highest.HasValue && group.TotalExpense == highest.Value;

        var incomeTransactions = await income.ListAsync(startDate, endExclusive);
        var incomeGroups = new Dictionary<int, CategoryExpenseGroup>();
        foreach (var transaction in incomeTransactions)
        {
            if (!incomeGroups.TryGetValue(transaction.CategoryId, out var group))
            {
                group = new CategoryExpenseGroup
                {
                    CategoryId = transaction.CategoryId,
                    CategoryName = transaction.CategoryName,
                    IconFileName = CategoryIconCatalog.FileNameFor(transaction.CategoryIconKey),
                    IsIncome = true
                };
                incomeGroups.Add(transaction.CategoryId, group);
            }

            group.Bills.Add(new BillTransaction
            {
                Id = transaction.Id,
                BillReferenceNo = transaction.BillReferenceNo,
                BillDate = transaction.TransactionDate,
                ShopId = transaction.ShopId,
                PaymentModeId = transaction.PaymentModeId,
                BillValue = transaction.Amount,
                Note = transaction.Note,
                ShopName = transaction.ShopName,
                PaymentModeName = transaction.PaymentModeName,
                ShopCategoryId = transaction.CategoryId,
                ShopCategoryName = transaction.CategoryName,
                ShopCategoryIconKey = transaction.CategoryIconKey,
                TransactionType = FinancialTransactionType.Income
            });
        }

        var orderedIncome = incomeGroups.Values
            .OrderByDescending(x => x.TotalExpense)
            .ThenBy(x => x.CategoryName)
            .ToList();

        return new ExpenseAnalysisResult(
            ordered,
            ordered.Sum(x => x.TotalExpense),
            orderedIncome,
            orderedIncome.Sum(x => x.TotalExpense));
    }
}
