using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace FinTracker.PageModels;

public partial class LoginPageModel(IAuthenticationService authentication, IBiometricAuthenticationService biometric, ModalErrorHandler errors) : ObservableObject, IQueryAttributable
{
    private string? _returnRoute;
    private bool _automaticPromptShown;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isPassword = true;
    [ObservableProperty] private bool _isBiometricAvailable;
    public string PasswordToggleText => IsPassword ? "Show password" : "Hide password";
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _returnRoute = query.TryGetValue("returnTo", out var route) ? route?.ToString() : null;
    }

    public async Task OnAppearingAsync()
    {
        await RefreshBiometricAvailabilityAsync();
        if (_automaticPromptShown || !IsBiometricAvailable) return;
        _automaticPromptShown = true;
        await LoginWithBiometricAsync(false);
    }

    private async Task RefreshBiometricAvailabilityAsync()
    {
        var configuredUserId = await biometric.GetConfiguredUserIdAsync();
        IsBiometricAvailable = configuredUserId.HasValue
            && await biometric.GetAvailabilityAsync() == BiometricAvailability.Available;
    }

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
    private Task ForgotPassword() => Shell.Current.GoToAsync("passwordreset");

    [RelayCommand]
    private async Task Login()
    {
        if (IsBusy) return;
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password)) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Login", "Enter your username and password.", "OK"); return; }
        IsBusy = true;
        try
        {
            var result = await authentication.LoginAsync(Username, Password);
            if (!result.Succeeded) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Login", result.Message, "OK"); return; }
            Password = string.Empty;
            var returnRoute = _returnRoute;
            _returnRoute = null;
            if (Shell.Current is AppShell shell && returnRoute is "myexpense" or "setup")
                shell.PendingDestinationAfterCompanySelection = returnRoute;
            await AppShell.NavigateToDashboardAsync();
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task LoginWithBiometric() => LoginWithBiometricAsync(true);

    private async Task LoginWithBiometricAsync(bool showUnavailableMessage)
    {
        if (IsBusy) return;
        var configuredUserId = await biometric.GetConfiguredUserIdAsync();
        if (!configuredUserId.HasValue)
        {
            IsBiometricAvailable = false;
            return;
        }

        IsBusy = true;
        try
        {
            var result = await biometric.AuthenticateAsync();
            if (result.Status != BiometricAuthenticationStatus.Success)
            {
                if (result.Status == BiometricAuthenticationStatus.Unavailable && showUnavailableMessage && !string.IsNullOrWhiteSpace(result.Message))
                    await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Fingerprint Login", result.Message, "OK");
                else if (result.Status == BiometricAuthenticationStatus.LockedOut && showUnavailableMessage)
                    await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Fingerprint Login", result.Message, "OK");
                return;
            }

            var login = await authentication.AuthenticateLocalUserAfterBiometricAsync(configuredUserId.Value);
            if (!login.Succeeded)
            {
                await biometric.ClearConfigurationAsync();
                IsBiometricAvailable = false;
                await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Fingerprint Login", "The configured local account no longer exists. Please login or register.", "OK");
                return;
            }

            var returnRoute = _returnRoute;
            _returnRoute = null;
            if (Shell.Current is AppShell shell && returnRoute is "myexpense" or "setup")
                shell.PendingDestinationAfterCompanySelection = returnRoute;
            await AppShell.NavigateToDashboardAsync();
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }
}
