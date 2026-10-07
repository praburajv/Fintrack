using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class BudgetReportPreviewPage : ContentPage
{
    public BudgetReportPreviewPage(BudgetReportPreviewPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
