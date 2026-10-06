using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class ExpenseStatementPreviewPage : ContentPage
{
    public ExpenseStatementPreviewPage(ExpenseStatementPreviewPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
