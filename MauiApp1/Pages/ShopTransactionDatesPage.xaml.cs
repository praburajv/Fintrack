using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class ShopTransactionDatesPage : ContentPage
{
    public ShopTransactionDatesPage(ShopTransactionDatesPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
