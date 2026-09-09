using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MauiApp1.PageModels;

public partial class LoginPageModel(IAuthenticationService authentication, ModalErrorHandler errors) : ObservableObject
{
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isPassword = true;
    public string PasswordToggleText => IsPassword ? "Show password" : "Hide password";

    [RelayCommand]
    private void TogglePassword() { IsPassword = !IsPassword; OnPropertyChanged(nameof(PasswordToggleText)); }

    [RelayCommand]
    private async Task Register()
    {
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
            await Shell.Current.Navigation.PopAsync(false);
        await Shell.Current.GoToAsync("register");
    }

    [RelayCommand]
    private async Task Login()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password)) { await Shell.Current.DisplayAlertAsync("Login", "Enter your username and password.", "OK"); return; }
        IsBusy = true;
        try
        {
            var result = await authentication.LoginAsync(Username, Password);
            if (!result.Succeeded) { await Shell.Current.DisplayAlertAsync("Login", result.Message, "OK"); return; }
            Password = string.Empty;
            await Shell.Current.GoToAsync("//main");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }
}
