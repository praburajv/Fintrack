namespace FinTracker.Pages;

public partial class IncomeExpenseFlowPage : ContentPage
{
    public IncomeExpenseFlowPage(PageModels.IncomeExpenseFlowPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
