namespace FinTracker.Pages;

public partial class EtlDataManagePage : ContentPage
{
    public EtlDataManagePage(EtlDataManagePageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
