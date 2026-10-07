using FinTracker.Models;

namespace FinTracker.Services;

public interface IExpenseStatementDocumentService
{
    Task<string> CreatePdfAsync(ExpenseStatementSnapshot statement, CancellationToken cancellationToken = default);
    Task<string> CreateBudgetPdfAsync(BudgetReportSnapshot report, CancellationToken cancellationToken = default);
}
