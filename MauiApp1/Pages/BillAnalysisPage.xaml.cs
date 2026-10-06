using MauiApp1.Models;
using MauiApp1.PageModels;

namespace MauiApp1.Pages;

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
