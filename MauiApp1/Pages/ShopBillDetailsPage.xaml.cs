using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class ShopBillDetailsPage : ContentPage
{
    public ShopBillDetailsPage(ShopBillDetailsPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
