using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class EtlDataPage : ContentPage
{
    public EtlDataPage(EtlDataPageModel model) { InitializeComponent(); BindingContext = model; }
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is EtlDataPageModel model) await model.LoadPaymentModesAsync();
    }
}
