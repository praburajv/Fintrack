using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class MonthlyBudgetReportPage : ContentPage
{
    public MonthlyBudgetReportPage(MonthlyBudgetReportPageModel model) { InitializeComponent(); BindingContext = model; }
}
