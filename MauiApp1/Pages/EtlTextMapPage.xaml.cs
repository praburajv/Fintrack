using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class EtlTextMapPage : ContentPage
{
    public EtlTextMapPage(EtlTextMapPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
