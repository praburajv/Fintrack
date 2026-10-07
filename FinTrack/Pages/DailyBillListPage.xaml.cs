using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class DailyBillListPage : ContentPage
{
    public DailyBillListPage(DailyBillListPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
