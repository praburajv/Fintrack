using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class ShopBillDetailsPage : ContentPage
{
    public ShopBillDetailsPage(ShopBillDetailsPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
