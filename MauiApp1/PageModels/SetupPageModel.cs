using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MauiApp1.PageModels;

public partial class SetupPageModel : ObservableObject
{
    private readonly IAuthenticationService _authentication;
    private readonly IThemeService _themes;
    private readonly IBiometricAuthenticationService _biometric;
    [ObservableProperty] private string _selectedTheme = "Red";
    [ObservableProperty] private string _selectedAppearance = "System";
    [ObservableProperty] private bool _isBiometricEnabled;
    [ObservableProperty] private bool _isBiometricAvailable;

    public bool IsAuthenticated => _authentication.IsAuthenticated;
    public string[] AvailableThemes { get; } = ["Red", "Blue", "Green", "Purple"];
    public string[] AvailableAppearances { get; } = ["System", "Light", "Dark"];

    public SetupPageModel(IAuthenticationService authentication, IThemeService themes, IBiometricAuthenticationService biometric)
    {
        _authentication = authentication;
        _themes = themes;
        _biometric = biometric;
        _selectedTheme = themes.CurrentColorTheme;
        _selectedAppearance = themes.CurrentAppearance switch
        {
            AppTheme.Light => "Light",
            AppTheme.Dark => "Dark",
            _ => "System"
        };
        authentication.AuthenticationStateChanged += (_, _) => OnPropertyChanged(nameof(IsAuthenticated));
    }

    public async Task RefreshBiometricAsync()
    {
        var configuredId = await _biometric.GetConfiguredUserIdAsync();
        IsBiometricEnabled = configuredId == _authentication.CurrentUser?.Id;
        IsBiometricAvailable = await _biometric.GetAvailabilityAsync() == BiometricAvailability.Available;
    }

    public void DisableBiometricControls() => IsBiometricAvailable = false;

    [RelayCommand]
    private async Task ToggleBiometric()
    {
        if (!_authentication.IsAuthenticated || _authentication.CurrentUser is null) return;
        if (IsBiometricEnabled)
        {
            await _biometric.ClearConfigurationAsync();
            IsBiometricEnabled = false;
            return;
        }

        var availability = await _biometric.GetAvailabilityAsync();
        if (availability == BiometricAvailability.NoEnrollment)
        {
            await Shell.Current.DisplayAlertAsync("Fingerprint Login", "No fingerprint is currently configured on this device. Configure fingerprint authentication in Android Settings and try again.", "OK");
            return;
        }
        if (availability != BiometricAvailability.Available)
        {
            await Shell.Current.DisplayAlertAsync("Fingerprint Login", "Fingerprint authentication is currently unavailable.", "OK");
            return;
        }

        var existing = await _biometric.GetConfiguredUserIdAsync();
        if (existing.HasValue && existing.Value != _authentication.CurrentUser.Id
            && !await Shell.Current.DisplayAlertAsync("Enable Fingerprint Login", "This will replace the currently configured biometric login account.", "Enable", "Cancel"))
            return;

        var result = await _biometric.AuthenticateAsync();
        if (result.Status != BiometricAuthenticationStatus.Success) return;
        await _biometric.SetConfiguredUserIdAsync(_authentication.CurrentUser.Id);
        IsBiometricEnabled = true;
    }

    [RelayCommand]
    private void ApplyTheme()
    {
        _themes.ApplyColorTheme(SelectedTheme);
        _themes.ApplyAppearance(Enum.TryParse<AppTheme>(SelectedAppearance, true, out var appearance) ? appearance : AppTheme.Unspecified);
    }

    [RelayCommand]
    private void SelectTheme(string theme) => SelectedTheme = theme;

}
