using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class ShopTransactionDatesPage : ContentPage
{
    public ShopTransactionDatesPage(ShopTransactionDatesPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
