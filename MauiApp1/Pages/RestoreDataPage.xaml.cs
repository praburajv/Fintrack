using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class RestoreDataPage : ContentPage
{
    public RestoreDataPage(RestoreDataPageModel model) { InitializeComponent(); BindingContext = model; }
}
