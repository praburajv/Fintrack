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
        private bool _closePromptActive;

        public AppShell(IApplicationCloseService closeService, ICriticalOperationState criticalOperations)
        {
            _closeService = closeService;
            _criticalOperations = criticalOperations;
            InitializeComponent();
            var currentTheme = Application.Current!.RequestedTheme;
            ThemeSegmentedControl.SelectedIndex = currentTheme == AppTheme.Light ? 0 : 1;
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
                    _closeService.Close();
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
