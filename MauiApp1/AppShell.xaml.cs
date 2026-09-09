using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using MauiApp1.Services;
using Font = Microsoft.Maui.Font;

namespace MauiApp1
{
    public partial class AppShell : Shell
    {
        private readonly IApplicationCloseService _closeService;
        private readonly ICriticalOperationState _criticalOperations;
        private readonly IAuthenticationService _authentication;
        private bool _closePromptActive;
        private bool _isClosing;
        public static bool IsClosing { get; private set; }

        public AppShell(IApplicationCloseService closeService, ICriticalOperationState criticalOperations, IAuthenticationService authentication)
        {
            _closeService = closeService;
            _criticalOperations = criticalOperations;
            _authentication = authentication;
            IsClosing = false;
            BindingContext = this;
            _authentication.AuthenticationStateChanged += OnAuthenticationStateChanged;
            Unloaded += OnShellUnloaded;
            InitializeComponent();
            var currentTheme = Application.Current!.RequestedTheme;
            ThemeSegmentedControl.SelectedIndex = currentTheme == AppTheme.Light ? 0 : 1;
        }

        public bool IsAuthenticated => _authentication.IsAuthenticated;
        public bool IsLoggedOut => !IsAuthenticated;
        public string CurrentUserName => _authentication.CurrentUser?.Name ?? string.Empty;
        public string VersionText => $"Version {AppInfo.Current.VersionString}";

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

        private void UnsubscribeFromAuthentication()
        {
            _authentication.AuthenticationStateChanged -= OnAuthenticationStateChanged;
            Unloaded -= OnShellUnloaded;
        }

        private async void OnLoginClicked(object? sender, EventArgs e) => await NavigateFromFlyoutAsync("login");
        private async void OnRegisterClicked(object? sender, EventArgs e) => await NavigateFromFlyoutAsync("register");
        private void OnLogoutClicked(object? sender, EventArgs e) => _authentication.Logout();

        private async Task NavigateFromFlyoutAsync(string route)
        {
            if (_isClosing || IsClosing)
                return;
            FlyoutIsPresented = false;
            await Task.Yield();
            if (_isClosing || IsClosing)
                return;
            await GoToAsync(route);
        }


        private async void OnBackupRestoreClicked(object? sender, EventArgs e)
        {
            await DisplayAlertAsync(
                "Unavailable",
                "Backup & Restore is currently unavailable.",
                "OK");
        }
        private async void OnCloseApplicationClicked(object? sender, EventArgs e)
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
            Application.Current!.UserAppTheme = e.NewIndex == 0 ? AppTheme.Light : AppTheme.Dark;
        }
    }
}
