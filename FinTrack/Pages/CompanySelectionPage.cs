using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Pages.Controls;
using FinTracker.Services;

namespace FinTracker.Pages;

/// <summary>Initial company selection. Management remains on FinancialAccountCardsPage.</summary>
public sealed class CompanySelectionPage : ContentPage
{
    private readonly FinancialAccountRepository _repository;
    private readonly FinancialAccountContext _accounts;
    private readonly ICriticalOperationState _operations;
    private readonly IAuthenticationService _authentication;
    private readonly VerticalStackLayout _cards = new() { Spacing = 12 };
    private readonly ProcessingOverlay _overlay = new() { Message = "Loading companies...", ShowProgress = false };
    private bool _working;

    public CompanySelectionPage(FinancialAccountRepository repository, FinancialAccountContext accounts,
        ICriticalOperationState operations, IAuthenticationService authentication)
    {
        _repository = repository; _accounts = accounts; _operations = operations; _authentication = authentication;
        Title = "Choose Company";
        this.SetDynamicResource(BackgroundColorProperty, "Background");
        var heading = new Label { Text = "Choose your company", FontSize = 28, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        Content = new Grid { Children = { new ScrollView { Content = new VerticalStackLayout
        {
            Padding = new Thickness(20, 28), Spacing = 16,
            Children = { heading, new Label { Text = "Select a company to open its financial dashboard." }, _cards }
        } }, _overlay } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_authentication.IsAuthenticated) { await Shell.Current.GoToAsync("//login"); return; }
        if (_working) return;
        _working = true; _overlay.IsBusy = true;
        try
        {
            _cards.Children.Clear();
            var companies = await _repository.ListAsync();
            foreach (var company in companies) _cards.Children.Add(CreateCard(company));
            if (companies.Count == 0)
            {
                _cards.Children.Add(new Label { Text = "No active company is available. Open company management to continue." });
                var manage = new Button { Text = "Company management" };
                manage.Clicked += async (_, _) => await Shell.Current.GoToAsync("//companymanagement");
                _cards.Children.Add(manage);
            }
        }
        catch (Exception ex) { await ThemeDialog.AlertAsync("Choose company", ex.Message); }
        finally { _working = false; _overlay.IsBusy = false; }
    }

    private View CreateCard(FinancialAccount company)
    {
        var name = new Label { Text = company.Name, FontSize = 20, FontAttributes = FontAttributes.Bold, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
        name.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var address = new Label { Text = company.Address, FontSize = 13, MaxLines = 2 };
        address.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(60)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(24)) }, ColumnSpacing = 10 };
        grid.Add(new Image { Source = string.IsNullOrWhiteSpace(company.ProfilePicturePath) ? "et_top_menu_logo.png" : company.ProfilePicturePath, WidthRequest = 52, HeightRequest = 52, Aspect = Aspect.AspectFit });
        grid.Add(new VerticalStackLayout { Spacing = 3, Children = { name, address, new Label { Text = company.CurrencyCode, FontSize = 12 } } }, 1);
        grid.Add(new Label { Text = "›", FontSize = 27, VerticalTextAlignment = TextAlignment.Center }, 2);
        var card = new Border { Content = grid, Padding = new Thickness(16, 18), StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 } };
        card.SetDynamicResource(BackgroundColorProperty, "Surface");
        card.SetDynamicResource(Border.StrokeProperty, "Primary");
        SemanticProperties.SetDescription(card, $"Open {company.Name}, {company.Address}");
        card.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await SelectAsync(company)) });
        return card;
    }

    private async Task SelectAsync(FinancialAccount company)
    {
        if (_working || _operations.IsBusy || !_authentication.IsAuthenticated) return;
        _working = true; _overlay.Message = $"Opening {company.Name}..."; _overlay.IsBusy = true;
        try
        {
            var fresh = await _repository.GetAsync(company.Id);
            if (fresh is null || !fresh.IsActive) throw new InvalidOperationException("This company is no longer active.");
            _accounts.Select(fresh);
            var destination = (Shell.Current as AppShell)?.ConsumePendingCompanyDestination();
            await Shell.Current.GoToAsync(destination is "myexpense" or "setup" ? $"//{destination}" : "//main", false);
        }
        catch (Exception ex) { await ThemeDialog.AlertAsync("Choose company", ex.Message); }
        finally { _working = false; _overlay.IsBusy = false; }
    }
}
