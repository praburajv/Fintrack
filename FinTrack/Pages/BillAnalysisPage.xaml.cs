using FinTracker.Models;
using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class BillAnalysisPage : ContentPage
{
    public BillAnalysisPage(BillAnalysisPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    private async void OnMonthSelected(object? sender, MonthlyVisitData month)
    {
        if (BindingContext is BillAnalysisPageModel model)
            await model.OpenMonthCommand.ExecuteAsync(month);
    }
}
