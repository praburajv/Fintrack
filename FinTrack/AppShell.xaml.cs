using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using FinTracker.Services;
using Font = Microsoft.Maui.Font;

namespace FinTracker
{
    public partial class AppShell : Shell
    {
        private readonly IApplicationCloseService _closeService;
        private readonly ICriticalOperationState _criticalOperations;
        private readonly IAuthenticationService _authentication;
        private readonly IThemeService _themes;
        private readonly IBiometricAuthenticationService _biometric;
        private bool _closePromptActive;
        private bool _expenseRouteNormalizationInProgress;
        private bool _isClosing;
        public static bool IsClosing { get; private set; }

        public AppShell(IApplicationCloseService closeService, ICriticalOperationState criticalOperations, IAuthenticationService authentication, IThemeService themes, IBiometricAuthenticationService biometric)
        {
            _closeService = closeService;
            _criticalOperations = criticalOperations;
            _authentication = authentication;
            _themes = themes;
            _biometric = biometric;
            IsClosing = false;
            BindingContext = this;
            _authentication.AuthenticationStateChanged += OnAuthenticationStateChanged;
            Navigating += OnShellNavigating;
            Unloaded += OnShellUnloaded;
            InitializeComponent();
            ThemeSegmentedControl.SelectedIndex = _themes.CurrentAppearance == AppTheme.Dark ? 1 : 0;
            Dispatcher.Dispatch(async () =>
            {
                try
                {
                    if (await _biometric.GetConfiguredUserIdAsync() is not null)
                        await GoToAsync("login");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Startup authentication check failed: {ex}");
                }
            });
        }

        public bool IsAuthenticated => _authentication.IsAuthenticated;
        public bool IsLoggedOut => !IsAuthenticated;
        public string CurrentUserName => _authentication.CurrentUser?.Name ?? string.Empty;
        public string VersionText => $"Version {AppInfo.Current.VersionString}";
        // Explicit test-phase switch. Set to false before a public production release.
        private const bool EnableDatabaseTools = true;
        public bool IsDeveloperBuild => EnableDatabaseTools;

        private void OnAuthenticationStateChanged(object? sender, EventArgs e)
        {
            if (_isClosing || IsClosing || Handler is null)
                return;

            try
            {
                OnPropertyChanged(nameof(IsAuthenticated));
                OnPropertyChanged(nameof(IsLoggedOut));
                OnPropertyChanged(nameof(CurrentUserName));
            }
            catch (ObjectDisposedException)
            {
                UnsubscribeFromAuthentication();
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("disposed", StringComparison.OrdinalIgnoreCase))
            {
                UnsubscribeFromAuthentication();
            }
        }

        private void OnShellUnloaded(object? sender, EventArgs e) => UnsubscribeFromAuthentication();

        private async void OnShellNavigating(object? sender, ShellNavigatingEventArgs e)
        {
            if (_isClosing || IsClosing)
                return;
            var target = e.Target.Location.OriginalString;
            if (_authentication.IsAuthenticated)
            {
                if (!_expenseRouteNormalizationInProgress
                    && target.Contains("myexpense", StringComparison.OrdinalIgnoreCase)
                    && !target.Contains("mode=", StringComparison.OrdinalIgnoreCase))
                {
                    e.Cancel();
                    _expenseRouteNormalizationInProgress = true;
                    try { await GoToAsync("//myexpense?mode=FullYear"); }
                    finally { _expenseRouteNormalizationInProgress = false; }
                    return;
                }
                return;
            }
            var protectedRoute = target.Contains("main", StringComparison.OrdinalIgnoreCase) || target.Contains("dashboard", StringComparison.OrdinalIgnoreCase) || target.Contains("myexpense", StringComparison.OrdinalIgnoreCase) || target.Contains("incomeanalysis", StringComparison.OrdinalIgnoreCase) || target.Contains("incomeflow", StringComparison.OrdinalIgnoreCase) || target.Contains("dailydash", StringComparison.OrdinalIgnoreCase) || target.Contains("calendar", StringComparison.OrdinalIgnoreCase) || target.Contains("expenseanalysis", StringComparison.OrdinalIgnoreCase) || target.Contains("monthlybudget", StringComparison.OrdinalIgnoreCase) || target.Contains("budgetreport", StringComparison.OrdinalIgnoreCase) || target.Contains("expensereport", StringComparison.OrdinalIgnoreCase) || target.Contains("billanalysis", StringComparison.OrdinalIgnoreCase) || target.Contains("shoptransactiondates", StringComparison.OrdinalIgnoreCase) || target.Contains("dailybills", StringComparison.OrdinalIgnoreCase);
            if (!protectedRoute || target.Contains("login", StringComparison.OrdinalIgnoreCase))
                return;
            e.Cancel();
            await NavigateFromFlyoutAsync("login");
        }

        private void UnsubscribeFromAuthentication()
        {
            _authentication.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            Unloaded -= OnShellUnloaded;
        }

        private async void OnLoginClicked(object? sender, EventArgs e) => await NavigateFromFlyoutAsync("login");
        private async void OnRegisterClicked(object? sender, EventArgs e) => await NavigateFromFlyoutAsync("register");
        private async void OnSetupClicked(object? sender, EventArgs e) => await NavigateToSetupAsync();
        private async void OnSetupDataClicked(object? sender, EventArgs e) => await NavigateFromFlyoutAsync("setupdata");
        private async void OnLogoutClicked(object? sender, EventArgs e)
        {
            _authentication.Logout();
            await GoToAsync("//login");
        }

        private async void OnDbToolsClicked(object? sender, EventArgs e)
        {
            if (_isClosing || IsClosing)
                return;

            try
            {
                FlyoutIsPresented = false;
                await Task.Yield();
                var choice = await DisplayActionSheetAsync("DB Tools", "Cancel", null, "SQL QA", "Users");
                if (choice == "SQL QA")
                    await GoToAsync("sqlqa");
                else if (choice == "Users")
                    await GoToAsync("databasetools");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Tools navigation failed: {ex.GetType().Name}");
                await DisplayAlertAsync("DB Tools", "The selected tool could not be opened.", "OK");
            }
        }

        private async Task NavigateFromFlyoutAsync(string route)
        {
            if (_isClosing || IsClosing)
                return;
            FlyoutIsPresented = false;
            await Task.Yield();
            if (_isClosing || IsClosing)
                return;
            var rootRoutes = new[] { "main", "dashboard", "myexpense", "incomeanalysis", "incomeflow", "dailydash", "calendar", "setup" };
            await GoToAsync(rootRoutes.Contains(route, StringComparer.OrdinalIgnoreCase) ? $"//{route}" : route);
        }

        public static async Task NavigateToSetupAsync()
        {
            if (IsClosing || Shell.Current is null)
                return;

            var currentRoute = Shell.Current.CurrentState?.Location?.OriginalString ?? string.Empty;
            if (currentRoute.Contains("/setup", StringComparison.OrdinalIgnoreCase)
                || string.Equals(currentRoute, "setup", StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                Shell.Current.FlyoutIsPresented = false;
                await Task.Yield();
                await Shell.Current.GoToAsync("//setup");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Setup navigation failed: {ex.GetType().Name}");
                await Shell.Current.DisplayAlertAsync("Setup", "Setup could not be opened. Please try again.", "OK");
            }
        }

        public static async Task NavigateToDashboardAsync()
        {
            if (IsClosing || Shell.Current is null)
                return;

            try
            {
                Shell.Current.FlyoutIsPresented = false;
                await Task.Yield();
                var route = Shell.Current.CurrentState?.Location?.OriginalString ?? string.Empty;
                var rootRoute = route.Split('?', 2)[0].Trim('/');
                if (string.Equals(rootRoute, "main", StringComparison.OrdinalIgnoreCase))
                    return;

                await Shell.Current.GoToAsync("//main", false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dashboard navigation failed: {ex.GetType().Name}");
                await Shell.Current.GoToAsync("//main", false);
            }
        }


        private async void OnBackupRestoreClicked(object? sender, EventArgs e)
        {
            await NavigateFromFlyoutAsync("backuprestore");
        }
        private async void OnCloseApplicationClicked(object? sender, EventArgs e) => await CloseApplicationAsync();

        public async Task CloseApplicationAsync()
        {
            if (_closePromptActive)
                return;

            if (_criticalOperations.IsBusy)
            {
                await DisplayAlertAsync("Please wait", "Please wait until the current operation is complete.", "OK");
                return;
            }

            _closePromptActive = true;
            try
            {
                var close = await DisplayAlertAsync(
                    "Close Application?",
                    "Are you sure you want to close Household Expense Tracker?",
                    "Close",
                    "Cancel");

                if (close && !_criticalOperations.IsBusy)
                {
                    _isClosing = true;
                    IsClosing = true;
                    UnsubscribeFromAuthentication();
                    FlyoutIsPresented = false;
                    try
                    {
                        _closeService.Close();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Application close failed during Activity teardown: {ex.GetType().Name}");
                    }
                    return;
                }
                else if (close)
                    await DisplayAlertAsync("Please wait", "Please wait until the current operation is complete.", "OK");
            }
            finally
            {
                _closePromptActive = false;
            }
        }
        public static async Task DisplaySnackbarAsync(string message)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#FF3300"),
                TextColor = Colors.White,
                ActionButtonTextColor = Colors.Yellow,
                CornerRadius = new CornerRadius(0),
                Font = Font.SystemFontOfSize(18),
                ActionButtonFont = Font.SystemFontOfSize(14)
            };

            var snackbar = Snackbar.Make(message, visualOptions: snackbarOptions);

            await snackbar.Show(cancellationTokenSource.Token);
        }

        public static async Task DisplayToastAsync(string message)
        {
            // Toast is currently not working in MCT on Windows
            if (OperatingSystem.IsWindows())
                return;

            var toast = Toast.Make(message, textSize: 18);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await toast.Show(cts.Token);
        }

        private void SfSegmentedControl_SelectionChanged(object? sender, Syncfusion.Maui.Toolkit.SegmentedControl.SelectionChangedEventArgs e)
        {
            _themes.ApplyAppearance(e.NewIndex == 0 ? AppTheme.Light : AppTheme.Dark);
        }
    }
}
