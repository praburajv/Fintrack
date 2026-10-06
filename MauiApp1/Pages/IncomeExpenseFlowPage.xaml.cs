namespace MauiApp1.Pages;

public partial class IncomeExpenseFlowPage : ContentPage
{
    public IncomeExpenseFlowPage(PageModels.IncomeExpenseFlowPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
