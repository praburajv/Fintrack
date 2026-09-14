namespace MauiApp1.Pages;

public partial class LoginPage : ContentPage
{
    private readonly LoginPageModel _model;
    public LoginPage(LoginPageModel model) { InitializeComponent(); _model = model; BindingContext = model; Appearing += OnAppearing; }
    private async void OnAppearing(object? sender, EventArgs e) => await _model.OnAppearingAsync();
}
