using System.ComponentModel;
using Microsoft.Maui.Controls.Shapes;

namespace FinTracker.Pages;

/// <summary>Standalone page, intentionally not registered in Shell or connected to settings services.</summary>
public sealed class GlobalDisplaySettingsPage : ContentPage
{
    private readonly GlobalDisplaySettingsPageModel _draft;
    private readonly ContentView _preview = new();
    private readonly Label _validation = new() { FontSize = 13 };
    private bool _animating;

    public GlobalDisplaySettingsPage() : this(new GlobalDisplaySettingsPageModel()) { }
    public GlobalDisplaySettingsPage(GlobalDisplaySettingsPageModel draft)
    {
        _draft = draft;
        BindingContext = draft;
        Title = "Global Display Settings";
        SetDynamicResource(BackgroundColorProperty, "Background");
        var content = new VerticalStackLayout { Padding = 18, Spacing = 18, MaximumWidthRequest = 720, HorizontalOptions = LayoutOptions.Fill };
        content.Add(Heading("Global display settings"));
        content.Add(new Label { Text = "Build your display preferences here. This standalone draft does not change the app, your data or saved settings.", FontSize = 14 });
        content.Add(Heading("Live preview")); content.Add(_preview); content.Add(_validation);
        var play = new Button { Text = "Play preview animation", FontSize = 14 };
        play.Clicked += async (_, _) =>
        {
            if (_animating || !_draft.Animation) return;
            _animating = true;
            try { await _preview.TranslateToAsync(10, 0, 150, Easing.CubicOut); await _preview.TranslateToAsync(0, 0, 150, Easing.CubicOut); }
            finally { _preview.TranslationX = 0; _animating = false; }
        };
        play.SetBinding(IsEnabledProperty, nameof(draft.Animation)); content.Add(play);
        content.Add(Section("Appearance", PickerRow("Theme", draft.Themes, nameof(draft.Theme))));
        content.Add(Section("Brand and status colors",
            ColorRow("Primary", nameof(draft.PrimaryColor)), ColorRow("Secondary", nameof(draft.SecondaryColor)),
            ColorRow("Success", nameof(draft.SuccessColor)), ColorRow("Danger", nameof(draft.DangerColor)), ColorRow("Warning", nameof(draft.WarningColor))));
        content.Add(Section("Typography and layout",
            PickerRow("Font family", draft.Fonts, nameof(draft.FontFamily)), SliderRow("Text size", 12, 24, nameof(draft.FontSize)),
            SliderRow("Corner radius", 0, 28, nameof(draft.BorderRadius)), SliderRow("Spacing", 4, 24, nameof(draft.Spacing)),
            SwitchRow("Show shadows", nameof(draft.ShowShadows)), SwitchRow("Animation", nameof(draft.Animation))));
        content.Add(Section("Formatting and language",
            PickerRow("Display currency", draft.Currencies, nameof(draft.CurrencyCode)),
            PickerRow("Number format", draft.NumberFormats, nameof(draft.CurrencyFormat)),
            PickerRow("Date format", draft.DateFormats, nameof(draft.DateFormat)), PickerRow("Language / locale", draft.Locales, nameof(draft.Localization)),
            new Label { Text = "Currency formatting does not convert amounts. Locale changes the preview date only; screen translations will be wired later.", FontSize = 12 }));
        var status = new Label { FontSize = 13 }; status.SetBinding(Label.TextProperty, nameof(draft.Status)); content.Add(status);
        var actions = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 12 };
        actions.Add(new Button { Text = "Reset draft", FontSize = 14, Command = draft.ResetDraftCommand });
        actions.Add(new Button { Text = "Validate draft", FontSize = 14, Command = draft.PreviewDraftCommand }, 1); content.Add(actions);
        content.Add(new Label { Text = "App-wide Apply: not connected yet.", FontSize = 12, HorizontalTextAlignment = TextAlignment.Center });
        Content = new ScrollView { Content = content };
        RenderPreview();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing(); _draft.PropertyChanged -= OnDraftChanged; _draft.PropertyChanged += OnDraftChanged;
        if (Application.Current is not null) Application.Current.RequestedThemeChanged += OnRequestedThemeChanged;
        RenderPreview();
    }
    protected override void OnDisappearing()
    {
        _draft.PropertyChanged -= OnDraftChanged;
        if (Application.Current is not null) Application.Current.RequestedThemeChanged -= OnRequestedThemeChanged;
        base.OnDisappearing();
    }
    private void OnDraftChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName != nameof(_draft.Status)) RenderPreview(); }
    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => RenderPreview();

    private void RenderPreview()
    {
        _validation.Text = _draft.Validate() ?? "Example values only — no live financial data.";
        // Invalid/incomplete input must never reach Color.FromArgb or crash the preview.
        if (_draft.Validate() is not null) return;
        var dark = _draft.Theme == "Dark" || (_draft.Theme == "System" && Application.Current?.RequestedTheme == AppTheme.Dark);
        var surface = Color.FromArgb(dark ? "#1D2939" : "#FFFFFF");
        var text = Color.FromArgb(dark ? "#F2F4F7" : "#101828");
        var stack = new VerticalStackLayout { Spacing = _draft.Spacing };
        Label Sample(string value, string? color = null, bool bold = false) => new()
        {
            Text = value, TextColor = color is null ? text : Color.FromArgb(color), FontFamily = _draft.FontFamily,
            FontSize = _draft.FontSize, FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None
        };
        stack.Add(Sample("FinTracker", _draft.PrimaryColor, true));
        stack.Add(Sample("Display preview", _draft.SecondaryColor));
        stack.Add(Sample(_draft.FormatAmount(1234.56m), bold: true));
        stack.Add(Sample(_draft.FormatDate(new DateTime(2026, 4, 25))));
        stack.Add(Sample("Income  + " + _draft.FormatAmount(250m), _draft.SuccessColor));
        stack.Add(Sample("Expense  − " + _draft.FormatAmount(48m), _draft.DangerColor));
        stack.Add(Sample("Reminder: example transaction", _draft.WarningColor));
        var card = new Border
        {
            Content = stack, Padding = _draft.Spacing, BackgroundColor = surface,
            Stroke = Color.FromArgb(_draft.PrimaryColor), StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = _draft.BorderRadius }
        };
        if (_draft.ShowShadows) card.Shadow = new Shadow { Brush = Brush.Black, Opacity = 0.15f, Radius = 8, Offset = new Point(0, 3) };
        _preview.Content = card;
    }

    private static Label Heading(string text) => new() { Text = text, FontFamily = "OpenSansSemibold", FontSize = 19 };
    private static Border Section(string title, params View[] rows)
    {
        var stack = new VerticalStackLayout { Spacing = 12 }; stack.Add(Heading(title));
        foreach (var row in rows) stack.Add(row);
        var border = new Border { Content = stack, Padding = 16, StrokeThickness = 0, StrokeShape = new RoundRectangle { CornerRadius = 16 } };
        border.SetDynamicResource(BackgroundColorProperty, "Surface"); return border;
    }
    private static View PickerRow(string title, IReadOnlyList<string> choices, string property)
    {
        var picker = new Picker { Title = title, ItemsSource = choices.ToList(), FontSize = 15 };
        picker.SetBinding(Picker.SelectedItemProperty, new Binding(property, BindingMode.TwoWay));
        SemanticProperties.SetDescription(picker, title);
        return new VerticalStackLayout { Children = { new Label { Text = title, FontSize = 13 }, picker } };
    }
    private static View ColorRow(string title, string property)
    {
        var entry = new Entry { Placeholder = "#RRGGBB", MaxLength = 7, FontSize = 15 };
        entry.SetBinding(Entry.TextProperty, new Binding(property, BindingMode.TwoWay));
        SemanticProperties.SetDescription(entry, title + " hexadecimal color");
        return new VerticalStackLayout { Children = { new Label { Text = title, FontSize = 13 }, entry } };
    }
    private static View SliderRow(string title, double min, double max, string property)
    {
        var slider = new Slider { Minimum = min, Maximum = max };
        slider.SetBinding(Slider.ValueProperty, new Binding(property, BindingMode.TwoWay));
        SemanticProperties.SetDescription(slider, title);
        var label = new Label { FontSize = 13 }; label.SetBinding(Label.TextProperty, new Binding(property, stringFormat: title + ": {0:0}"));
        return new VerticalStackLayout { Children = { label, slider } };
    }
    private static View SwitchRow(string title, string property)
    {
        var toggle = new Switch(); toggle.SetBinding(Switch.IsToggledProperty, new Binding(property, BindingMode.TwoWay));
        SemanticProperties.SetDescription(toggle, title);
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        grid.Add(new Label { Text = title, VerticalOptions = LayoutOptions.Center, FontSize = 15 }); grid.Add(toggle, 1); return grid;
    }
}
