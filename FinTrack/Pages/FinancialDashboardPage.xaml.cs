using FinTracker.Models;
using FinTracker.PageModels;
using FinTracker.Services;
using Microsoft.Maui.Controls.Shapes;

namespace FinTracker.Pages;

public partial class FinancialDashboardPage : ContentPage
{
    private readonly FinancialDashboardPageModel _model;
    private readonly IAuthenticationService _authentication;
    private readonly ModalErrorHandler _errors;
    private bool _navigationInProgress;
    private bool _loadedForThisAppearance;

    public FinancialDashboardPage(FinancialDashboardPageModel model, IAuthenticationService authentication, ModalErrorHandler errors)
    {
        InitializeComponent();
        _model = model;
        _authentication = authentication;
        _errors = errors;
        BindingContext = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _navigationInProgress = false;
        if (!AppShell.HasCompletedStartupRouting || AppShell.IsStartupRouting) return;
        if (!_authentication.IsAuthenticated)
        {
            await Shell.Current.GoToAsync("//login");
            return;
        }
        await LoadAfterStartupAsync();
    }

    public async Task LoadAfterStartupAsync()
    {
        if (_loadedForThisAppearance || !_authentication.IsAuthenticated) return;
        _loadedForThisAppearance = true;
        await _model.OnNavigatedToAsync();
    }

    public void ToggleSearch()
    {
        if (_model.IsProcessing || _model.Search.IsOpeningAnalysis) return;
        _model.IsSearchVisible = !_model.IsSearchVisible;
        if (_model.IsSearchVisible)
            Dispatcher.Dispatch(() => ShopSearchEntry.Focus());
        else
            ShopSearchEntry.Unfocus();
    }

    public bool BeginSharedNavigation(string message)
    {
        if (_navigationInProgress || _model.IsProcessing || _model.Search.IsOpeningAnalysis) return false;
        _navigationInProgress = true;
        _model.ProcessingMessage = message;
        _model.IsOpeningLink = true;
        return true;
    }

    public void EndSharedNavigation()
    {
        _model.IsOpeningLink = false;
        _navigationInProgress = false;
    }

    private void OnGreetingArtworkSizeChanged(object? sender, EventArgs e)
    {
        if (sender is not Grid artwork || artwork.Width <= 0 || artwork.Height <= 0) return;

        artwork.Clip = new RoundRectangleGeometry(
            new CornerRadius(17),
            new Rect(0, 0, artwork.Width, artwork.Height));
    }

    protected override void OnDisappearing()
    {
        ShopSearchEntry.Unfocus();
        _loadedForThisAppearance = false;
        _model.OnNavigatedAway();
        base.OnDisappearing();
    }

    private async void OnChartCategorySelected(object? sender, CategoryExpenseChartData category)
        => await OpenAsync(() => _model.OpenCategoryAsync(category.CategoryId, category.CategoryName), "Opening category...");

    private async void OnCategoryTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid { BindingContext: DashboardCategoryRow row })
            await OpenAsync(() => _model.OpenCategoryAsync(row.Id, row.Name), "Opening category...");
    }

    private async void OnTransactionTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid { BindingContext: DashboardTransactionRow row })
            await OpenAsync(() => _model.OpenTransactionAsync(row), "Opening transaction...");
    }

    private async void OnIncomeChartCategorySelected(object? sender, CategoryExpenseChartData category)
        => await OpenAsync(() => _model.OpenIncomeCategoryAsync(category.CategoryId, category.CategoryName), "Opening income category...");

    private async void OnIncomeCategoryTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Grid { BindingContext: DashboardCategoryRow row })
            await OpenAsync(() => _model.OpenIncomeCategoryAsync(row.Id, row.Name), "Opening income category...");
    }

    private async Task OpenAsync(Func<Task> navigate, string message)
    {
        if (!BeginSharedNavigation(message)) return;
        try
        {
            await Task.Yield();
            await navigate();
        }
        catch (Exception ex)
        {
            _errors.HandleError(ex);
        }
        finally { EndSharedNavigation(); }
    }
}
