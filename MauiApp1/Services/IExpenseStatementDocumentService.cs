using MauiApp1.Models;

namespace MauiApp1.Services;

public interface IExpenseStatementDocumentService
{
    Task<string> CreatePdfAsync(ExpenseStatementSnapshot statement, CancellationToken cancellationToken = default);
    Task<string> CreateBudgetPdfAsync(BudgetReportSnapshot report, CancellationToken cancellationToken = default);
}
