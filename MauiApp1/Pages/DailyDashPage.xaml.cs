using MauiApp1.Models;
using MauiApp1.PageModels;
using MauiApp1.Pages.Controls;

namespace MauiApp1.Pages;

public partial class DailyDashPage : ContentPage
{
    private bool _navigationInProgress;
    public DailyDashPage(DailyDashPageModel model) { InitializeComponent(); BindingContext = model; }
    private async void OnPaymentModeSelected(object? sender, PaymentModeExpenseChartData item)
    {
        if (_navigationInProgress || sender is not PaymentModeExpenseChart chart) return;
        _navigationInProgress = true;
        try
        {
            var name = Uri.EscapeDataString(item.PaymentModeName);
            await Shell.Current.GoToAsync($"paymentmodebills?paymentModeId={item.PaymentModeId}&paymentModeName={name}&start={chart.StartDate:yyyy-MM-dd}&end={chart.EndDate:yyyy-MM-dd}");
        }
        catch { _navigationInProgress = false; }
    }
    protected override void OnAppearing() { base.OnAppearing(); _navigationInProgress = false; }
}
