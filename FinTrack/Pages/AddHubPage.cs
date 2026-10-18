using FinTracker.Pages.Controls;
using FinTracker.Services;

namespace FinTracker.Pages;

/// <summary>Shared Add destination; existing entry routes remain the only editors.</summary>
public sealed class AddHubPage : ContentPage
{
    private readonly FinancialAccountContext _accounts;
    private bool _navigating;

    public AddHubPage(FinancialAccountContext accounts)
    {
        _accounts = accounts;
        Title = "Add";
        this.SetDynamicResource(BackgroundColorProperty, "Background");
        var heading = new Label { Text = "Add", FontSize = 34, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var subtitle = new Label { Text = "Record a new financial activity", FontSize = 16 };
        subtitle.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
        var company = new Label { FontSize = 13 };
        company.SetDynamicResource(Label.TextColorProperty, "Primary");
        _companyLabel = company;
        var stack = new VerticalStackLayout { Padding = new Thickness(20, 24), Spacing = 18,
            Children = { heading, subtitle, company, CreateCard("↓", "Add Income", "#BDF4D9", "#00A769", "Income"),
                CreateCard("↑", "Add Expense", "#FFD9DE", "#EC3F4A", "Expense") } };
        var grid = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        grid.Add(new ScrollView { Content = stack }, 0, 0);
        grid.Add(new BottomTaskBar(), 0, 1);
        Content = grid;
    }

    private readonly Label _companyLabel;
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _companyLabel.Text = _accounts.Current?.Name ?? "Choose a company first";
    }

    private View CreateCard(string icon, string title, string backgroundHex, string accentHex, string type)
    {
        var accent = Color.FromArgb(accentHex);
        var circle = new Border { Content = new Label { Text = icon, FontSize = 52, FontAttributes = FontAttributes.Bold,
                TextColor = accent, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center },
            WidthRequest = 110, HeightRequest = 110, BackgroundColor = Color.FromArgb(backgroundHex), StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 55 } };
        var label = new Label { Text = title, FontSize = 25, FontAttributes = FontAttributes.Bold, HorizontalTextAlignment = TextAlignment.Center };
        label.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var arrow = new Label { Text = "›", FontSize = 25, HorizontalTextAlignment = TextAlignment.End };
        arrow.SetDynamicResource(Label.TextColorProperty, "Primary");
        var content = new VerticalStackLayout { Spacing = 16, Children = { circle, label, arrow } };
        circle.HorizontalOptions = LayoutOptions.Center;
        var card = new Border { Content = content, Padding = new Thickness(24), StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 } };
        card.SetDynamicResource(Border.BackgroundColorProperty, "Surface");
        card.SetDynamicResource(Border.StrokeProperty, "Primary");
        SemanticProperties.SetDescription(card, title);
        card.GestureRecognizers.Add(new TapGestureRecognizer { Command = new Command(async () => await OpenAsync(type)) });
        return card;
    }

    private async Task OpenAsync(string type)
    {
        if (_navigating || _accounts.Current is null) return;
        _navigating = true;
        try { await Shell.Current.GoToAsync($"bill?type={type}"); }
        finally { _navigating = false; }
    }
}
