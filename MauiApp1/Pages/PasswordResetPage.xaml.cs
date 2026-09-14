namespace MauiApp1.Pages;

public partial class PasswordResetPage : ContentPage
{
    public PasswordResetPage(PasswordResetPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
