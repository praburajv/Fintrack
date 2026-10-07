using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class EtlTextMapPage : ContentPage
{
    public EtlTextMapPage(EtlTextMapPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
