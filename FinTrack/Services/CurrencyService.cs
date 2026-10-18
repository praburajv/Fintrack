using FinTracker.Models;
using FinTracker.Utilities;
using Microsoft.Maui.Storage;

namespace FinTracker.Services;

public sealed class CurrencyService : ICurrencyService
{
    private const string CurrencyPreferenceKey = "DisplayCurrencyCode";
    private const string DefaultCurrencyCode = "EUR";
    private string? _accountOverride;

    private static readonly IReadOnlyList<DisplayCurrency> SupportedCurrencies =
    [
        new("EUR", "Euro", "€", "pt-PT", symbolBeforeAmount: false, includeSymbolSpace: true),
        new("USD", "US Dollar", "$", "en-US", symbolBeforeAmount: true, includeSymbolSpace: false),
        new("GBP", "British Pound", "£", "en-GB", symbolBeforeAmount: true, includeSymbolSpace: false),
        new("INR", "Indian Rupee", "₹", "en-IN", symbolBeforeAmount: true, includeSymbolSpace: false),
        new("LKR", "Sri Lankan Rupee", "Rs", "en-LK", symbolBeforeAmount: true, includeSymbolSpace: true),
        new("CAD", "Canadian Dollar", "C$", "en-CA", symbolBeforeAmount: true, includeSymbolSpace: false),
        new("AUD", "Australian Dollar", "A$", "en-AU", symbolBeforeAmount: true, includeSymbolSpace: false),
        // Keep two decimal places for every option so changing the display currency
        // never hides or rounds part of an existing stored amount.
        new("JPY", "Japanese Yen", "¥", "ja-JP", symbolBeforeAmount: true, includeSymbolSpace: false)
    ];

    public CurrencyService()
    {
        CurrentCurrency = FindCurrency(DefaultCurrencyCode);
        EuroFormatter.Configure(CurrentCurrency);
    }

    public IReadOnlyList<DisplayCurrency> AvailableCurrencies => SupportedCurrencies;
    public DisplayCurrency CurrentCurrency { get; private set; }
    public event EventHandler? CurrencyChanged;

    public void LoadSavedPreference()
    {
        var savedCode = Preferences.Get(CurrencyPreferenceKey, DefaultCurrencyCode);
        ApplyCurrencyCore(savedCode, save: false);
    }

    public void ApplyCurrency(string currencyCode) => ApplyCurrencyCore(currencyCode, save: true);
    public void ApplyAccountCurrency(string currencyCode)
    {
        _accountOverride = currencyCode;
        ApplyCurrencyCore(currencyCode, save: false);
    }
    public void ClearAccountCurrency()
    {
        _accountOverride = null;
        LoadSavedPreference();
    }

    private void ApplyCurrencyCore(string currencyCode, bool save)
    {
        var selectedCurrency = FindCurrency(_accountOverride ?? currencyCode);
        CurrentCurrency = selectedCurrency;
        EuroFormatter.Configure(selectedCurrency);

        if (Application.Current is not null)
        {
            Application.Current.Resources["DisplayCurrencySymbol"] = selectedCurrency.Symbol;
            Application.Current.Resources["CurrencyExpenseAxisTitle"] = $"Expense ({selectedCurrency.Symbol})";
            Application.Current.Resources["CurrencyBillValueHint"] = $"Bill value ({selectedCurrency.Symbol})";
        }

        if (save)
            Preferences.Set(CurrencyPreferenceKey, selectedCurrency.Code);

        CurrencyChanged?.Invoke(this, EventArgs.Empty);
    }

    private static DisplayCurrency FindCurrency(string? currencyCode)
        => SupportedCurrencies.FirstOrDefault(currency =>
               string.Equals(currency.Code, currencyCode, StringComparison.OrdinalIgnoreCase))
           ?? SupportedCurrencies[0];
}
