using Microsoft.Extensions.DependencyInjection;

namespace FinTracker
{
    public partial class App : Application
    {
        private readonly FinTracker.Services.IApplicationCloseService _closeService;
        private readonly FinTracker.Services.ICriticalOperationState _criticalOperations;
        private readonly FinTracker.Services.IAuthenticationService _authentication;
        private readonly FinTracker.Services.IThemeService _themes;
        private readonly FinTracker.Services.ICurrencyService _currencies;
        private readonly FinTracker.Services.IBiometricAuthenticationService _biometric;
        private readonly FinTracker.Data.FinancialAccountRepository _financialAccounts;
        private readonly FinTracker.Services.FinancialAccountContext _accountContext;

        public App(FinTracker.Services.IApplicationCloseService closeService, FinTracker.Services.ICriticalOperationState criticalOperations, FinTracker.Services.IAuthenticationService authentication, FinTracker.Services.IThemeService themes, FinTracker.Services.ICurrencyService currencies, FinTracker.Services.IBiometricAuthenticationService biometric, FinTracker.Data.FinancialAccountRepository financialAccounts, FinTracker.Services.FinancialAccountContext accountContext)
        {
            _closeService = closeService;
            _criticalOperations = criticalOperations;
            _authentication = authentication;
            _themes = themes;
            _currencies = currencies;
            _biometric = biometric;
            _financialAccounts = financialAccounts;
            _accountContext = accountContext;
            InitializeComponent();
            _themes.LoadSavedPreferences();
            _currencies.LoadSavedPreference();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell(_closeService, _criticalOperations, _authentication, _themes, _biometric, _financialAccounts, _accountContext));
        }
    }
}
