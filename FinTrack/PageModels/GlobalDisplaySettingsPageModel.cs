using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;

namespace FinTracker.PageModels;

/// <summary>No service or preference writes: every edit belongs only to this page's draft.</summary>
public partial class GlobalDisplaySettingsPageModel : ObservableObject
{
    public IReadOnlyList<string> Themes { get; } = ["System", "Light", "Dark"];
    public IReadOnlyList<string> Fonts { get; } = ["OpenSansRegular", "OpenSansSemibold", "SegoeSemibold"];
    public IReadOnlyList<string> Currencies { get; } = ["EUR", "USD", "GBP", "INR", "LKR", "CAD", "AUD", "JPY"];
    public IReadOnlyList<string> NumberFormats { get; } = ["1,234.56", "1.234,56", "1 234,56"];
    public IReadOnlyList<string> DateFormats { get; } = ["dd/MM/yyyy", "MM/dd/yyyy", "yyyy-MM-dd", "dd MMM yyyy"];
    public IReadOnlyList<string> Locales { get; } = ["en-GB", "en-US", "pt-PT", "ta-IN"];

    [ObservableProperty] private string _theme = "System";
    [ObservableProperty] private string _primaryColor = "#175CD3";
    [ObservableProperty] private string _secondaryColor = "#FF8500";
    [ObservableProperty] private string _successColor = "#027A48";
    [ObservableProperty] private string _dangerColor = "#B42318";
    [ObservableProperty] private string _warningColor = "#B54708";
    [ObservableProperty] private string _fontFamily = "OpenSansRegular";
    [ObservableProperty] private double _fontSize = 16;
    [ObservableProperty] private double _borderRadius = 16;
    [ObservableProperty] private double _spacing = 12;
    [ObservableProperty] private bool _showShadows = true;
    [ObservableProperty] private bool _animation = true;
    [ObservableProperty] private string _currencyCode = "EUR";
    [ObservableProperty] private string _currencyFormat = "1,234.56";
    [ObservableProperty] private string _dateFormat = "dd/MM/yyyy";
    [ObservableProperty] private string _localization = "en-GB";
    [ObservableProperty] private string _status = "Preview only. Nothing is saved or applied to other screens.";

    public GlobalDisplaySettings CreateSnapshot() => new()
    {
        Theme = Theme, PrimaryColor = PrimaryColor, SecondaryColor = SecondaryColor,
        SuccessColor = SuccessColor, DangerColor = DangerColor, WarningColor = WarningColor,
        FontFamily = FontFamily, FontSize = FontSize, BorderRadius = BorderRadius, Spacing = Spacing,
        ShowShadows = ShowShadows, Animation = Animation, CurrencyCode = CurrencyCode,
        CurrencyFormat = CurrencyFormat, DateFormat = DateFormat, Localization = Localization
    };

    public string? Validate()
    {
        if (new[] { PrimaryColor, SecondaryColor, SuccessColor, DangerColor, WarningColor }.Any(c => !IsValidColor(c)))
            return "Use six-digit hexadecimal colors, such as #175CD3.";
        if (!Themes.Contains(Theme) || !Fonts.Contains(FontFamily) || !Currencies.Contains(CurrencyCode)
            || !NumberFormats.Contains(CurrencyFormat) || !DateFormats.Contains(DateFormat) || !Locales.Contains(Localization))
            return "Select one of the available display options.";
        if (!double.IsFinite(FontSize) || FontSize is < 12 or > 24 || !double.IsFinite(BorderRadius)
            || BorderRadius is < 0 or > 28 || !double.IsFinite(Spacing) || Spacing is < 4 or > 24)
            return "Text size, corner radius or spacing is outside the supported range.";
        return null;
    }

    public static bool IsValidColor(string? color) => color is { Length: 7 } && color[0] == '#'
        && color.AsSpan(1).ToArray().All(Uri.IsHexDigit);

    public string FormatAmount(decimal amount)
    {
        var cultureName = CurrencyFormat switch { "1.234,56" => "de-DE", "1 234,56" => "fr-FR", _ => "en-US" };
        return $"{CurrencyCode} {amount.ToString("N2", CultureInfo.GetCultureInfo(cultureName))}";
    }
    public string FormatDate(DateTime date) => date.ToString(DateFormats.Contains(DateFormat) ? DateFormat : "dd/MM/yyyy",
        CultureInfo.GetCultureInfo(Locales.Contains(Localization) ? Localization : "en-GB"));

    [RelayCommand]
    private void PreviewDraft() => Status = Validate() ?? "Draft is valid. App-wide Apply and saving will be connected later.";

    [RelayCommand]
    private void ResetDraft()
    {
        var defaults = new GlobalDisplaySettings();
        Theme = defaults.Theme; PrimaryColor = defaults.PrimaryColor; SecondaryColor = defaults.SecondaryColor;
        SuccessColor = defaults.SuccessColor; DangerColor = defaults.DangerColor; WarningColor = defaults.WarningColor;
        FontFamily = defaults.FontFamily; FontSize = defaults.FontSize; BorderRadius = defaults.BorderRadius;
        Spacing = defaults.Spacing; ShowShadows = defaults.ShowShadows; Animation = defaults.Animation;
        CurrencyCode = defaults.CurrencyCode; CurrencyFormat = defaults.CurrencyFormat;
        DateFormat = defaults.DateFormat; Localization = defaults.Localization;
        Status = "Draft reset. Saved application preferences are unchanged.";
    }
}
