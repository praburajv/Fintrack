using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class EtlDataPage : ContentPage
{
    public EtlDataPage(EtlDataPageModel model) { InitializeComponent(); BindingContext = model; }
}
