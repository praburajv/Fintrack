using Microsoft.Extensions.DependencyInjection;

namespace MauiApp1
{
    public partial class App : Application
    {
        private readonly MauiApp1.Services.IApplicationCloseService _closeService;
        private readonly MauiApp1.Services.ICriticalOperationState _criticalOperations;
        private readonly MauiApp1.Services.IAuthenticationService _authentication;
        private readonly MauiApp1.Services.IThemeService _themes;
        private readonly MauiApp1.Services.ICurrencyService _currencies;
        private readonly MauiApp1.Services.IBiometricAuthenticationService _biometric;

        public App(MauiApp1.Services.IApplicationCloseService closeService, MauiApp1.Services.ICriticalOperationState criticalOperations, MauiApp1.Services.IAuthenticationService authentication, MauiApp1.Services.IThemeService themes, MauiApp1.Services.ICurrencyService currencies, MauiApp1.Services.IBiometricAuthenticationService biometric)
        {
            _closeService = closeService;
            _criticalOperations = criticalOperations;
            _authentication = authentication;
            _themes = themes;
            _currencies = currencies;
            _biometric = biometric;
            InitializeComponent();
            _themes.LoadSavedPreferences();
            _currencies.LoadSavedPreference();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell(_closeService, _criticalOperations, _authentication, _themes, _biometric));
        }
    }
}
