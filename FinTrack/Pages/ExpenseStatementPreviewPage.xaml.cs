using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class ExpenseStatementPreviewPage : ContentPage
{
    public ExpenseStatementPreviewPage(ExpenseStatementPreviewPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
