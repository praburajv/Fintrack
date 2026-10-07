using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class RestoreDataPage : ContentPage
{
    public RestoreDataPage(RestoreDataPageModel model) { InitializeComponent(); BindingContext = model; }
}
