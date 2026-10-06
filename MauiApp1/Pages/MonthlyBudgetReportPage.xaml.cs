using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class MonthlyBudgetReportPage : ContentPage
{
    public MonthlyBudgetReportPage(MonthlyBudgetReportPageModel model) { InitializeComponent(); BindingContext = model; }
}
