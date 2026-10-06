namespace MauiApp1.Pages;

public partial class EtlDataManagePage : ContentPage
{
    public EtlDataManagePage(EtlDataManagePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
