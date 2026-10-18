using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Services;
using FinTracker.Pages.Controls;

namespace FinTracker.Pages;

/// <summary>The explicit company decision shown after each fresh launch or sign-in.</summary>
public sealed class FinancialAccountCardsPage : ContentPage
{
    private readonly FinancialAccountRepository _repository;
    private readonly FinancialAccountContext _context;
    private readonly ICriticalOperationState _operations;
    private readonly ICurrencyService _currencies;
    private readonly IAuthenticationService _authentication;
    private readonly VerticalStackLayout _cards = new() { Spacing = 12 };
    private readonly VerticalStackLayout _inactive = new() { Spacing = 8 };
    private readonly ProcessingOverlay _overlay = new() { Message = "Loading companies...", ShowProgress = false };
    private bool _working;

    public FinancialAccountCardsPage(FinancialAccountRepository repository, FinancialAccountContext context, ICriticalOperationState operations, ICurrencyService currencies, IAuthenticationService authentication)
    {
        _repository = repository;
        _context = context;
        _operations = operations;
        _currencies = currencies;
        _authentication = authentication;
        Title = "Choose Company";
        this.SetDynamicResource(BackgroundColorProperty, "Background");

        var title = new Label { Text = "Choose your company", FontSize = 28, FontAttributes = FontAttributes.Bold };
        title.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var subtitle = new Label { Text = "Select a company to open its financial dashboard.", FontSize = 14 };
        subtitle.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
        var add = new Button { Text = "+ Create company", HorizontalOptions = LayoutOptions.Fill };
        add.Clicked += async (_, _) => await CreateCompanyAsync();
        var content = new VerticalStackLayout { Padding = new Thickness(20, 28), Spacing = 16, Children = { title, subtitle, _cards, add, _inactive } };
        Content = new Grid { Children = { new ScrollView { Content = content }, _overlay } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_authentication.IsAuthenticated) { await Shell.Current.GoToAsync("//login"); return; }
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        if (_working) return;
        _working = true;
        _overlay.Message = "Loading companies...";
        _overlay.IsBusy = true;
        try
        {
            var accounts = await _repository.ListAsync();
            _cards.Children.Clear();
            foreach (var account in accounts) _cards.Children.Add(CreateCard(account));
            var inactive = await _repository.ListInactiveAsync();
            _inactive.Children.Clear();
            if (inactive.Count > 0)
            {
                var label = new Label { Text = "Inactive companies", FontSize = 18, FontAttributes = FontAttributes.Bold };
                label.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
                _inactive.Children.Add(label);
                foreach (var account in inactive)
                {
                    var button = new Button { Text = $"Reactivate {account.Name}" };
                    button.Clicked += async (_, _) => await ReactivateAsync(account);
                    _inactive.Children.Add(button);
                }
            }
            if (accounts.Count == 0) await ThemeDialog.AlertAsync("Financial accounts", "No active company is available. Create one to continue.");
        }
        catch (Exception ex)
        {
            await ThemeDialog.AlertAsync("Financial accounts", $"Companies could not be loaded: {ex.Message}");
        }
        finally { _overlay.IsBusy = false; _working = false; }
    }

    private View CreateCard(FinancialAccount account)
    {
        var logo = new Image { Source = string.IsNullOrWhiteSpace(account.ProfilePicturePath) ? "et_top_menu_logo.png" : account.ProfilePicturePath, WidthRequest = 52, HeightRequest = 52, Aspect = Aspect.AspectFit };
        var name = new Label { Text = account.Name, FontSize = 20, FontAttributes = FontAttributes.Bold, MaxLines = 1, LineBreakMode = LineBreakMode.TailTruncation };
        name.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var address = new Label { Text = account.Address, FontSize = 13, MaxLines = 2, LineBreakMode = LineBreakMode.TailTruncation };
        address.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
        var currency = new Label { Text = account.CurrencyCode, FontSize = 12 };
        currency.SetDynamicResource(Label.TextColorProperty, "Primary");
        var details = new VerticalStackLayout { Spacing = 3, Children = { name, address, currency } };
        var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(new GridLength(60)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(24)) }, ColumnSpacing = 10 };
        grid.Add(logo, 0); grid.Add(details, 1);
        var arrow = new Label { Text = "›", FontSize = 27, VerticalTextAlignment = TextAlignment.Center };
        arrow.SetDynamicResource(Label.TextColorProperty, "Primary"); grid.Add(arrow, 2);
        var card = new Border { Content = grid, Padding = new Thickness(16, 18), StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 } };
        card.SetDynamicResource(Border.BackgroundColorProperty, "Surface");
        card.SetDynamicResource(Border.StrokeProperty, "Primary");
        SemanticProperties.SetDescription(card, $"Open {account.Name}, {account.Address}");
        card.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await SelectAsync(account)) });
        var edit = new Button { Text = "Edit company", FontSize = 12, HorizontalOptions = LayoutOptions.End };
        edit.Clicked += async (_, _) => await EditCompanyAsync(account);
        var actions = new HorizontalStackLayout { Spacing = 8, HorizontalOptions = LayoutOptions.End, Children = { edit } };
        if (!account.IsDefault)
        {
            var deactivate = new Button { Text = "Deactivate", FontSize = 12 };
            deactivate.Clicked += async (_, _) => await DeactivateAsync(account);
            actions.Children.Add(deactivate);
        }
        return new VerticalStackLayout { Spacing = 2, Children = { card, actions } };
    }

    private async Task SelectAsync(FinancialAccount account)
    {
        if (_working || _operations.IsBusy || !_authentication.IsAuthenticated) return;
        _working = true;
        _overlay.Message = $"Opening {account.Name}..."; _overlay.IsBusy = true;
        try
        {
            var fresh = await _repository.GetAsync(account.Id);
            if (fresh is null || !fresh.IsActive) throw new InvalidOperationException("This company is no longer active.");
            _context.Select(fresh);
            var destination = (Shell.Current as AppShell)?.ConsumePendingCompanyDestination();
            await Shell.Current.GoToAsync(destination is "myexpense" or "setup" ? $"//{destination}" : "//main", false);
        }
        catch (Exception ex) { await ThemeDialog.AlertAsync("Choose company", ex.Message); }
        finally { _overlay.IsBusy = false; _working = false; }
    }

    private async Task CreateCompanyAsync()
    {
        if (_working || _operations.IsBusy || !_authentication.IsAuthenticated) return;
        var name = await ThemeDialog.PromptAsync("New company", "Company name", maxLength: 100);
        if (string.IsNullOrWhiteSpace(name)) return;
        var address = await ThemeDialog.PromptAsync("New company", "Address", maxLength: 250);
        if (address is null) return;
        var currency = await ThemeDialog.ChooseAsync("Display currency", "No conversion is performed.", _currencies.AvailableCurrencies.Select(x => x.Code).ToArray());
        if (currency is null) return;
        var categoryChoice = await ThemeDialog.ChooseAsync("Categories", "Choose how to set up this company's categories. Budgets start at zero.", ["Copy from company", "Use 38 built-in categories"]);
        if (categoryChoice is null) return;
        var copy = categoryChoice == "Copy from company";
        var candidates = await _repository.ListAsync();
        FinancialAccount? source = _context.Current;
        if (copy && source is null && candidates.Count > 1)
        {
            var sourceName = await ThemeDialog.ChooseAsync("Copy categories", "Choose the source company.", candidates.Select(x => x.Name).ToArray());
            source = candidates.FirstOrDefault(x => x.Name == sourceName);
            if (source is null) return;
        }
        source ??= candidates.FirstOrDefault();
        _working = true;
        _overlay.Message = "Creating company..."; _overlay.IsBusy = true;
        try
        {
            // On the first company-card visit, the migration's default company is the source.
            if (source is null) throw new InvalidOperationException("No source company is available.");
            var previous = _context.Current;
            _context.Select(source);
            try { await _repository.CreateAsync(new FinancialAccount { Name = name.Trim(), Address = address.Trim(), CurrencyCode = currency,
                LogoPath = "et_top_menu_logo.png", ProfilePicturePath = "et_top_menu_logo.png" }, copy);
            }
            finally { if (previous is null) _context.Clear(); else _context.Select(previous); }
        }
        catch (Exception ex) { await ThemeDialog.AlertAsync("New company", ex.Message); }
        finally { _overlay.IsBusy = false; _working = false; }
        await RefreshAsync();
    }

    private async Task EditCompanyAsync(FinancialAccount account)
    {
        if (_working || _operations.IsBusy || !_authentication.IsAuthenticated) return;
        var name = await ThemeDialog.PromptAsync("Edit company", "Company name", account.Name, 100);
        if (string.IsNullOrWhiteSpace(name)) return;
        var address = await ThemeDialog.PromptAsync("Edit company", "Address", account.Address, 250);
        if (address is null) return;
        var currency = await ThemeDialog.ChooseAsync("Display currency", "No conversion is performed.", _currencies.AvailableCurrencies.Select(x => x.Code).ToArray());
        if (currency is null) return;
        _working = true; _overlay.Message = "Saving company..."; _overlay.IsBusy = true;
        try
        {
            await _repository.UpdateAsync(new FinancialAccount { Id = account.Id, Name = name.Trim(), Address = address.Trim(), CurrencyCode = currency,
                LogoPath = account.LogoPath, ProfilePicturePath = account.ProfilePicturePath, IsActive = true, IsDefault = account.IsDefault });
        }
        catch (Exception ex) { await ThemeDialog.AlertAsync("Edit company", ex.Message); }
        finally { _working = false; _overlay.IsBusy = false; }
        await RefreshAsync();
    }

    private async Task DeactivateAsync(FinancialAccount account)
    {
        if (_working || _operations.IsBusy || !_authentication.IsAuthenticated) return;
        if (!await ThemeDialog.ConfirmAsync("Deactivate company?", $"Hide {account.Name} from company selection? Its financial data will be preserved and it can be reactivated later.", "Deactivate")) return;
        _working = true; _overlay.Message = "Deactivating company..."; _overlay.IsBusy = true;
        try { await _repository.DeactivateAsync(account.Id); }
        catch (Exception ex) { await ThemeDialog.AlertAsync("Deactivate company", ex.Message); }
        finally { _working = false; _overlay.IsBusy = false; }
        await RefreshAsync();
    }

    private async Task ReactivateAsync(FinancialAccount account)
    {
        if (_working || _operations.IsBusy || !_authentication.IsAuthenticated) return;
        _working = true; _overlay.Message = "Reactivating company..."; _overlay.IsBusy = true;
        try { await _repository.ReactivateAsync(account.Id); }
        catch (Exception ex) { await ThemeDialog.AlertAsync("Reactivate company", ex.Message); }
        finally { _working = false; _overlay.IsBusy = false; }
        await RefreshAsync();
    }
}
