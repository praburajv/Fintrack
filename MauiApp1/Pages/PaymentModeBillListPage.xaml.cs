using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class PaymentModeBillListPage : ContentPage
{
    public PaymentModeBillListPage(PaymentModeBillListPageModel model) { InitializeComponent(); BindingContext = model; }
}
