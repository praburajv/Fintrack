using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class PaymentModeBillListPage : ContentPage
{
    public PaymentModeBillListPage(PaymentModeBillListPageModel model) { InitializeComponent(); BindingContext = model; }
}
