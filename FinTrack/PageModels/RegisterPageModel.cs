using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FinTracker.PageModels;

public partial class RegisterPageModel(IAuthenticationService authentication, ModalErrorHandler errors) : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isPassword = true;
    public string PasswordToggleText => IsPassword ? "Show password" : "Hide password";

    [RelayCommand]
    private void TogglePassword() { IsPassword = !IsPassword; OnPropertyChanged(nameof(PasswordToggleText)); }

    [RelayCommand]
    private async Task Login()
    {
        if (Shell.Current.Navigation.NavigationStack.Count > 1)
            await Shell.Current.Navigation.PopAsync(false);
        await Shell.Current.GoToAsync("//login");
    }

    [RelayCommand]
    private async Task Register()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Username)) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Create Account", "Name and username are required.", "OK"); return; }
        if (Password.Length < LocalAuthenticationService.MinimumPasswordLength) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Create Account", $"Password must be at least {LocalAuthenticationService.MinimumPasswordLength} characters.", "OK"); return; }
        if (Password != ConfirmPassword) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Create Account", "Passwords do not match.", "OK"); return; }
        IsBusy = true;
        try
        {
            var result = await authentication.RegisterAsync(Name, Username, Password);
            if (!result.Succeeded) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Create Account", result.Message, "OK"); return; }
            Password = string.Empty; ConfirmPassword = string.Empty;
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Account created", result.Message, "OK");
            if (Shell.Current.Navigation.NavigationStack.Count > 1)
                await Shell.Current.Navigation.PopAsync(false);
            await Shell.Current.GoToAsync("//login");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }
}
