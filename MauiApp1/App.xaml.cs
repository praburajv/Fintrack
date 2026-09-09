using Microsoft.Extensions.DependencyInjection;

namespace MauiApp1
{
    public partial class App : Application
    {
        private readonly MauiApp1.Services.IApplicationCloseService _closeService;
        private readonly MauiApp1.Services.ICriticalOperationState _criticalOperations;
        private readonly MauiApp1.Services.IAuthenticationService _authentication;

        public App(MauiApp1.Services.IApplicationCloseService closeService, MauiApp1.Services.ICriticalOperationState criticalOperations, MauiApp1.Services.IAuthenticationService authentication)
        {
            _closeService = closeService;
            _criticalOperations = criticalOperations;
            _authentication = authentication;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell(_closeService, _criticalOperations, _authentication));
        }
    }
}
