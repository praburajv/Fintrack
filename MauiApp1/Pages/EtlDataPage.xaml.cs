using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class EtlDataPage : ContentPage
{
    public EtlDataPage(EtlDataPageModel model) { InitializeComponent(); BindingContext = model; }
}
