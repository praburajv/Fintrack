namespace FinTracker.Pages;

public partial class SetupPage : ContentPage
{
    public SetupPage(SetupPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
        Appearing += async (_, _) =>
        {
            try { await model.RefreshBiometricAsync(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Setup biometric availability check failed: {ex.GetType().Name}");
                model.DisableBiometricControls();
            }
        };
    }

    private async void OnLoginClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//login?returnTo=setup");
    private async void OnRegisterClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("register");
    private async void OnExpensesClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync("//myexpense");
    private async void OnLogoutClicked(object? sender, EventArgs e)
    {
        if (Shell.Current is AppShell shell) await shell.LogoutAsync();
    }
}
