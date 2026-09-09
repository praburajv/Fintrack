namespace MauiApp1.Pages;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginPageModel model) { InitializeComponent(); BindingContext = model; }
}
