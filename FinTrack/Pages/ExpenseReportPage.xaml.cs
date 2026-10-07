using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class ExpenseReportPage : ContentPage
{
    public ExpenseReportPage(ExpenseReportPageModel model) { InitializeComponent(); BindingContext = model; }

}
