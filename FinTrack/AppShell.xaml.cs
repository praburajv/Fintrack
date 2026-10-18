using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using FinTracker.Pages;
using FinTracker.Services;
using FinTracker.Pages.Controls;
using FinTracker.Data;
using System.Runtime.CompilerServices;
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
        private readonly FinancialAccountRepository _financialAccounts;
        private readonly FinancialAccountContext _accountContext;
        private bool _closePromptActive;
        private bool _expenseRouteNormalizationInProgress;
        private bool _isClosing;
        private bool _startupRoutingInProgress;
        private bool _logoutInProgress;
        private bool _introductionInProgress;
        private bool _sharedActionInProgress;
        private bool _initializingThemeSelection = true;
        public string? PendingDestinationAfterCompanySelection { get; set; }
        public string? ConsumePendingCompanyDestination()
        {
            var destination = PendingDestinationAfterCompanySelection;
            PendingDestinationAfterCompanySelection = null;
            return destination;
        }
        private readonly ConditionalWeakTable<ContentPage, AppBar> _pageHeaders = new();
        public static bool IsClosing { get; private set; }
        public static bool IsStartupRouting { get; private set; }
        public static bool HasCompletedStartupRouting { get; private set; }

        public AppShell(IApplicationCloseService closeService, ICriticalOperationState criticalOperations, IAuthenticationService authentication, IThemeService themes, IBiometricAuthenticationService biometric, FinancialAccountRepository financialAccounts, FinancialAccountContext accountContext)
        {
            _closeService = closeService;
            _criticalOperations = criticalOperations;
            _authentication = authentication;
            _themes = themes;
            _biometric = biometric;
            _financialAccounts = financialAccounts;
            _accountContext = accountContext;
            IsClosing = false;
            HasCompletedStartupRouting = false;
            BindingContext = this;
            _authentication.AuthenticationStateChanged += OnAuthenticationStateChanged;
            _accountContext.Changed += OnFinancialAccountChanged;
            Navigating += OnShellNavigating;
            Unloaded += OnShellUnloaded;
            InitializeComponent();
            Navigated += OnSharedNavigationChanged;
            UpdateSharedHeader();
            ThemeSegmentedControl.SelectedIndex = _themes.CurrentAppearance == AppTheme.Dark ? 1 : 0;
            _initializingThemeSelection = false;
            Dispatcher.Dispatch(InitializeStartupNavigationAsync);
        }

        private async void InitializeStartupNavigationAsync()
        {
            if (_startupRoutingInProgress)
                return;

            _startupRoutingInProgress = true;
            IsStartupRouting = true;
            try
            {
                await Task.Yield();
                await _financialAccounts.InitializeAsync();
                await _authentication.RestoreSessionAsync();
                _accountContext.Clear();
                var route = _authentication.IsAuthenticated
                    ? "//companies"
                    : WelcomePage.HasCompletedWelcome
                        ? "//login"
                        : "//welcome";
                await GoToAsync(route);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Startup navigation failed: {ex}");
                try
                {
                    await GoToAsync("//login");
                    await ThemeDialog.AlertAsync("Financial account setup", "The database could not be prepared safely. Existing data was not intentionally removed. Restore a verified complete backup or contact support before entering new data.");
                }
                catch (Exception navigationError) { System.Diagnostics.Debug.WriteLine($"Startup recovery navigation failed: {navigationError}"); }
            }
            finally
            {
                IsStartupRouting = false;
                _startupRoutingInProgress = false;
                HasCompletedStartupRouting = true;
                if (_authentication.IsAuthenticated && CurrentPage is FinancialDashboardPage dashboard)
                    await dashboard.LoadAfterStartupAsync();
            }
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
                if (!_authentication.IsAuthenticated) _accountContext.Clear();
                UpdateSharedHeader();
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
                var leaf = NavigationRoutes.Leaf(target);
                if (_accountContext.Current is null && leaf is not ("companies" or "companymanagement" or "welcome" or "introduction" or "login" or "register" or "passwordreset" or "backuprestore" or "restore"))
                {
                    e.Cancel();
                    await GoToAsync("//companies");
                    return;
                }
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
            var protectedRoute = target.Contains("main", StringComparison.OrdinalIgnoreCase) || target.Contains("companies", StringComparison.OrdinalIgnoreCase) || target.Contains("companymanagement", StringComparison.OrdinalIgnoreCase) || target.Contains("reportshub", StringComparison.OrdinalIgnoreCase) || target.Contains("addhub", StringComparison.OrdinalIgnoreCase) || target.Contains("dashcool", StringComparison.OrdinalIgnoreCase) || target.Contains("dashboard", StringComparison.OrdinalIgnoreCase) || target.Contains("myexpense", StringComparison.OrdinalIgnoreCase) || target.Contains("incomeanalysis", StringComparison.OrdinalIgnoreCase) || target.Contains("incomeflow", StringComparison.OrdinalIgnoreCase) || target.Contains("dailydash", StringComparison.OrdinalIgnoreCase) || target.Contains("calendar", StringComparison.OrdinalIgnoreCase) || target.Contains("expenseanalysis", StringComparison.OrdinalIgnoreCase) || target.Contains("monthlybudget", StringComparison.OrdinalIgnoreCase) || target.Contains("budgetreport", StringComparison.OrdinalIgnoreCase) || target.Contains("expensereport", StringComparison.OrdinalIgnoreCase) || target.Contains("billanalysis", StringComparison.OrdinalIgnoreCase) || target.Contains("shoptransactiondates", StringComparison.OrdinalIgnoreCase) || target.Contains("dailybills", StringComparison.OrdinalIgnoreCase);
            if (!protectedRoute || target.Contains("login", StringComparison.OrdinalIgnoreCase))
                return;
            e.Cancel();
            await NavigateFromFlyoutAsync("login");
        }

        private void UnsubscribeFromAuthentication()
        {
            _authentication.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            _accountContext.Changed -= OnFinancialAccountChanged;
            Unloaded -= OnShellUnloaded;
            Navigated -= OnSharedNavigationChanged;
        }

        private async void OnLoginClicked(object? sender, EventArgs e) => await NavigateFromFlyoutAsync("login");

        private void OnFinancialAccountChanged(object? sender, EventArgs e)
        {
            if (!IsClosing && Handler is not null) Dispatcher.Dispatch(UpdateSharedHeader);
        }
        private async void OnRegisterClicked(object? sender, EventArgs e) => await NavigateFromFlyoutAsync("register");
        private async void OnSetupClicked(object? sender, EventArgs e) => await NavigateToSetupAsync();
        private async void OnSetupDataClicked(object? sender, EventArgs e) => await NavigateFromFlyoutAsync("setupdata");
        private async void OnFinancialAccountsClicked(object? sender, EventArgs e)
        {
            if (_criticalOperations.IsBusy)
            { await ThemeDialog.AlertAsync("Please wait", "Finish the current operation before changing company."); return; }
            FlyoutIsPresented = false;
            await GoToAsync("//companymanagement");
        }
        private async void OnShowIntroductionClicked(object? sender, EventArgs e)
        {
            if (_introductionInProgress || _logoutInProgress || _isClosing || IsClosing)
                return;

            _introductionInProgress = true;
            try
            {
                FlyoutIsPresented = false;
                await Task.Yield();
                if (_authentication.IsAuthenticated)
                    await GoToAsync("introduction", new ShellNavigationQueryParameters { ["entry"] = "replay" });
                else
                    await GoToAsync("//welcome");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Show introduction navigation failed: {ex}");
            }
            finally
            {
                _introductionInProgress = false;
            }
        }
        private async void OnLogoutClicked(object? sender, EventArgs e) => await LogoutAsync();

        public async Task LogoutAsync()
        {
            if (_logoutInProgress || _isClosing || IsClosing || _criticalOperations.IsBusy)
                return;

            _logoutInProgress = true;
            try
            {
                FlyoutIsPresented = false;
                await Task.Yield();
                _authentication.Logout();
                _accountContext.Clear();
                PendingDestinationAfterCompanySelection = null;
                await GoToAsync("//login");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Logout navigation failed: {ex}");
            }
            finally
            {
                _logoutInProgress = false;
            }
        }

        private async void OnDbToolsClicked(object? sender, EventArgs e)
        {
            if (_isClosing || IsClosing)
                return;

            try
            {
                FlyoutIsPresented = false;
                await Task.Yield();
                var choice = await ThemeDialog.ChooseAsync("DB Tools", "Choose a database tool.", ["SQL QA", "Users"]);
                if (choice == "SQL QA")
                    await GoToAsync("sqlqa");
                else if (choice == "Users")
                    await GoToAsync("databasetools");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"DB Tools navigation failed: {ex.GetType().Name}");
                await ThemeDialog.AlertAsync("DB Tools", "The selected tool could not be opened.");
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
            var rootRoutes = new[] { "main", "dashcool", "dashboard", "myexpense", "incomeanalysis", "incomeflow", "dailydash", "calendar", "setup", "login" };
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
                await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Setup", "Setup could not be opened. Please try again.", "OK");
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

                await Shell.Current.GoToAsync(Shell.Current is AppShell shell && shell._accountContext.Current is null ? "//companies" : "//main", false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Dashboard navigation failed: {ex.GetType().Name}");
                await Shell.Current.GoToAsync(Shell.Current is AppShell shell && shell._accountContext.Current is null ? "//companies" : "//main", false);
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
                await ThemeDialog.AlertAsync("Please wait", "Please wait until the current operation is complete.");
                return;
            }

            _closePromptActive = true;
            try
            {
                var close = await ThemeDialog.ConfirmAsync(
                    "Close Application?",
                    "Are you sure you want to close FinTracker?",
                    "Close");

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
                    await ThemeDialog.AlertAsync("Please wait", "Please wait until the current operation is complete.");
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
            // The flyout control can announce its initial selection when first laid out,
            // after the constructor. Only a change from an existing selection is a choice.
            if (_initializingThemeSelection || e.OldIndex < 0 || e.NewIndex < 0 || e.OldIndex == e.NewIndex) return;
            _themes.ApplyAppearance(e.NewIndex == 0 ? AppTheme.Light : AppTheme.Dark);
        }

        private string CurrentRoute => NavigationRoutes.Leaf(CurrentState?.Location?.OriginalString);
        public bool CanShowSharedNavigation => IsAuthenticated && NavigationRoutes.ShowShared(CurrentRoute);
        public string SelectedNavigationItem => NavigationRoutes.SelectedItem(CurrentRoute);

        private void OnSharedNavigationChanged(object? sender, ShellNavigatedEventArgs e) => UpdateSharedHeader();

        private void UpdateSharedHeader()
        {
            if (CurrentPage is not ContentPage page) return;
            if (!IsAuthenticated || NavigationRoutes.IsAuthentication(CurrentRoute))
            {
                Shell.SetTitleView(page, null!);
                return;
            }
            // A native pushed toolbar cannot share the same view instance with its root.
            // Cache one instance of the reusable component per page; weak keys avoid retaining popped pages.
            var header = _pageHeaders.GetValue(page, _ => new AppBar
            {
                SearchCommand = new Command(() =>
                {
                    if (CurrentPage is FinancialDashboardPage dashboard) dashboard.ToggleSearch();
                    else (CurrentPage as LandingDashboardPage)?.FocusShopSearch();
                }),
                ProfileCommand = new Command(async () => await NavigateSharedAsync("Profile")),
                MenuCommand = new Command(() => FlyoutIsPresented = true)
            });
            header.ShowSearch = CanShowSharedNavigation && page is LandingDashboardPage or FinancialDashboardPage;
            header.ShowUserProfile = CanShowSharedNavigation;
            // Keep the native Shell hamburger/Back affordance instead of duplicating it.
            header.ShowMenu = false;
            header.ShowNotification = false;
            header.CompanyName = _accountContext.Current?.Name ?? string.Empty;
            var names = CurrentUserName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            header.UserInitials = names.Length == 0 ? "FT" : string.Concat(names.Take(2).Select(n => n[..1])).ToUpperInvariant();
            Shell.SetTitleView(page, header);
        }

        public async Task NavigateSharedAsync(string action)
        {
            if (_sharedActionInProgress || _logoutInProgress || IsClosing || IsStartupRouting || !CanShowSharedNavigation) return;
            _sharedActionInProgress = true;
            try
            {
                if (_criticalOperations.IsBusy)
                {
                    await ThemeDialog.AlertAsync("Please wait", "Please finish the current operation before navigating.");
                    return;
                }
                FlyoutIsPresented = false;
                var route = string.Empty;
                switch (action)
                {
                    case "Home": route = "//main"; break;
                    case "Calendar": route = "//calendar"; break;
                    case "More": FlyoutIsPresented = true; return;
                    case "Reports": route = "//reportshub"; break;
                    case "Add": route = "//addhub"; break;
                    case "Profile":
                        var choice = await ThemeDialog.ChooseAsync(CurrentUserName, "Account actions", ["Setup", "Logout"]);
                        if (choice == "Setup") route = "//setup";
                        else if (choice == "Logout") await LogoutAsync();
                        break;
                }
                // Selection is derived from the actual route after successful navigation.
                if (string.IsNullOrEmpty(route) || !IsAuthenticated || IsClosing || _criticalOperations.IsBusy) return;
                if (route.StartsWith("//", StringComparison.Ordinal) && CurrentState.Location.OriginalString == route) return;
                var dashboard = CurrentPage as FinancialDashboardPage;
                if (dashboard is not null && !dashboard.BeginSharedNavigation($"Opening {action}...")) return;
                try
                {
                    await Task.Yield();
                    await GoToAsync(route);
                }
                finally { dashboard?.EndSharedNavigation(); }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Shared navigation failed: {ex.GetType().Name}");
                if (!IsClosing) await ThemeDialog.AlertAsync("Navigation", "This screen could not be opened. Please try again.");
            }
            finally { _sharedActionInProgress = false; }
        }
    }
}
