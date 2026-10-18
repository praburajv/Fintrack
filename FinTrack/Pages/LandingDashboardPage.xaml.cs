using Microsoft.Extensions.DependencyInjection;

namespace FinTracker.Pages;
public partial class LandingDashboardPage : ContentPage
{
    private bool _navigationInProgress;
    private bool _loginRedirectInProgress;
    public void FocusShopSearch() => ShopSearchEntry.Focus();
    public LandingDashboardPage(LandingDashboardPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
        Loaded += OnDashboardLoaded;
    }

    private void OnDashboardLoaded(object? sender, EventArgs e)
    {
        // Android can re-apply window insets during the login-to-Shell transition.
        // Reapply the Dashboard window state after the first layout pass as well.
        ApplyRoundedImageClips();
    }

    private void OnDashboardContentSizeChanged(object? sender, EventArgs e)
    {
        var contentWidth = DashboardContent.Width;
        if (contentWidth <= 0)
            return;

        // Match each container to its source image so AspectFill does not crop
        // the baked-in titles at the left or right edges.
        ExpenseRidePanel.HeightRequest = contentWidth * 887d / 1774d;
        var boxWidth = (contentWidth - 8d) / 2d;
        DailyDashPanel.HeightRequest = boxWidth * 388d / 400d;
        ExpenseEntryPanel.HeightRequest = boxWidth * 1210d / 1300d;
        CalendarPanel.HeightRequest = boxWidth;
        AnalysisPanel.HeightRequest = boxWidth * 1214d / 1295d;
        BudgetPanel.HeightRequest = boxWidth;
        MonthReportPanel.HeightRequest = boxWidth;

        ApplyRoundedImageClips();
        Dispatcher.Dispatch(ApplyRoundedImageClips);
    }

    private void ApplyRoundedImageClips()
    {
        ApplyRoundedImageClip(ExpenseRideImage, 18);
        ApplyRoundedImageClip(DailyDashImage, 14);
        ApplyRoundedImageClip(ExpenseEntryImage, 14);
        ApplyRoundedImageClip(CalendarImage, 14);
        ApplyRoundedImageClip(AnalysisImage, 14);
        ApplyRoundedImageClip(BudgetImage, 14);
        ApplyRoundedImageClip(MonthReportImage, 14);
    }

    private static void ApplyRoundedImageClip(Image image, double radius)
    {
        if (image.Width <= 0 || image.Height <= 0)
            return;

        image.Clip = new Microsoft.Maui.Controls.Shapes.RoundRectangleGeometry
        {
            CornerRadius = new CornerRadius(radius),
            Rect = new Rect(0, 0, image.Width, image.Height)
        };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (AppShell.IsStartupRouting)
            return;
        _navigationInProgress = false;
        var authentication = Handler?.MauiContext?.Services.GetService<IAuthenticationService>();
        if (authentication is not null && !authentication.IsAuthenticated && !_loginRedirectInProgress)
        {
            _loginRedirectInProgress = true;
            try
            {
                await Shell.Current.GoToAsync("//login");
            }
            catch (Exception ex)
            {
                _loginRedirectInProgress = false;
                System.Diagnostics.Debug.WriteLine($"Startup login navigation failed: {ex}");
            }
        }
    }

    private async void OnDailyDashClicked(object? sender, TappedEventArgs e) => await NavigateAsync("//dailydash");
    private async void OnExpenseEntryClicked(object? sender, TappedEventArgs e) => await NavigateAsync("//myexpense?mode=DashboardRecentMonths");
    private async void OnCalendarClicked(object? sender, TappedEventArgs e) => await NavigateAsync("//calendar");
    private async void OnAnalysisClicked(object? sender, TappedEventArgs e) => await NavigateAsync($"expenseanalysis?reference={DateTime.Today:yyyy-MM-dd}&period=Monthly");
    private async void OnBudgetClicked(object? sender, TappedEventArgs e) => await NavigateAsync($"monthlybudget?reference={DateTime.Today:yyyy-MM-dd}");
    private async void OnMonthReportClicked(object? sender, TappedEventArgs e) => await NavigateAsync($"expensereport?reference={DateTime.Today:yyyy-MM-dd}&period=Monthly");
    private async Task NavigateAsync(string route)
    {
        if (_navigationInProgress) return;
        _navigationInProgress = true;
        try { Shell.Current.FlyoutIsPresented = false; await Shell.Current.GoToAsync(route); }
        catch { _navigationInProgress = false; }
    }
}
