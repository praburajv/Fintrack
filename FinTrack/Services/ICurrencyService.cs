using FinTracker.Models;

namespace FinTracker.Services;

public interface ICurrencyService
{
    IReadOnlyList<DisplayCurrency> AvailableCurrencies { get; }
    DisplayCurrency CurrentCurrency { get; }
    event EventHandler? CurrencyChanged;
    void LoadSavedPreference();
    void ApplyCurrency(string currencyCode);
}
