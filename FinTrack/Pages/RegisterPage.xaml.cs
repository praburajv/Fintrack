namespace FinTracker.Pages;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterPageModel model) { InitializeComponent(); BindingContext = model; }
}
