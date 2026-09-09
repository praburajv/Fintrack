using Microsoft.Extensions.DependencyInjection;

namespace MauiApp1
{
    public partial class App : Application
    {
        private readonly MauiApp1.Services.IApplicationCloseService _closeService;
        private readonly MauiApp1.Services.ICriticalOperationState _criticalOperations;

        public App(MauiApp1.Services.IApplicationCloseService closeService, MauiApp1.Services.ICriticalOperationState criticalOperations)
        {
            _closeService = closeService;
            _criticalOperations = criticalOperations;
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell(_closeService, _criticalOperations));
        }
    }
}
