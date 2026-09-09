using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class DailyBillListPage : ContentPage
{
    public DailyBillListPage(DailyBillListPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
