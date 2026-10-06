using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class ExpenseReportPage : ContentPage
{
    public ExpenseReportPage(ExpenseReportPageModel model) { InitializeComponent(); BindingContext = model; }

}
