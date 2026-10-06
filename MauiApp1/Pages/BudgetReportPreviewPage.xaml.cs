using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class BudgetReportPreviewPage : ContentPage
{
    public BudgetReportPreviewPage(BudgetReportPreviewPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
