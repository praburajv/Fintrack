namespace FinTracker.Models;

/// <summary>Display-only draft contract. It is not persisted or applied to the app yet.</summary>
public sealed record GlobalDisplaySettings
{
    public string Theme { get; init; } = "System";
    public string PrimaryColor { get; init; } = "#175CD3";
    public string SecondaryColor { get; init; } = "#FF8500";
    public string SuccessColor { get; init; } = "#027A48";
    public string DangerColor { get; init; } = "#B42318";
    public string WarningColor { get; init; } = "#B54708";
    public string FontFamily { get; init; } = "OpenSansRegular";
    public double FontSize { get; init; } = 16;
    public double BorderRadius { get; init; } = 16;
    public double Spacing { get; init; } = 12;
    public bool ShowShadows { get; init; } = true;
    public bool Animation { get; init; } = true;
    public string CurrencyCode { get; init; } = "EUR";
    public string CurrencyFormat { get; init; } = "1,234.56";
    public string DateFormat { get; init; } = "dd/MM/yyyy";
    public string Localization { get; init; } = "en-GB";
}
