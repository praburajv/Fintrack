using FinTracker.Models;

namespace FinTracker.Utilities;

public static class EuroFormatter
{
    private static DisplayCurrency _currency = CreateDefaultCurrency();

    private static DisplayCurrency CreateDefaultCurrency()
    {
        var currency = new DisplayCurrency("EUR", "Euro", "€", "pt-PT", symbolBeforeAmount: false, includeSymbolSpace: true);
        ConfigureEuroSeparators(currency);
        return currency;
    }

    public static void Configure(DisplayCurrency currency)
    {
        ArgumentNullException.ThrowIfNull(currency);
        if (currency.Code == "EUR")
            ConfigureEuroSeparators(currency);
        _currency = currency;
    }

    public static string Format(decimal value)
    {
        var currency = _currency;
        return AddSymbol(value.ToString($"N{currency.DecimalDigits}", currency.NumberCulture), currency);
    }

    public static string FormatCalendarTile(decimal value)
    {
        var currency = _currency;
        var amount = Math.Max(0, value);
        if (amount >= 1000)
        {
            var thousands = (double)amount / 1000d;
            return AddSymbol(thousands.ToString(thousands % 1 == 0 ? "0" : "0.#", currency.NumberCulture) + "K", currency);
        }

        // Preserve meaningful cents while keeping the label short enough for a
        // seven-column calendar tile. Whole amounts intentionally omit decimals.
        return AddSymbol(
            amount == decimal.Truncate(amount)
                ? amount.ToString("0", currency.NumberCulture)
                : amount.ToString("0.#", currency.NumberCulture),
            currency);
    }

    public static string FormatCompact(double value)
    {
        var currency = _currency;
        var amount = Math.Max(0, value);
        if (amount >= 1000)
        {
            var thousands = amount / 1000d;
            return AddSymbol(thousands.ToString(thousands % 1 == 0 ? "0" : "0.#", currency.NumberCulture) + "K", currency);
        }

        return AddSymbol(Math.Round(amount).ToString("0", currency.NumberCulture), currency);
    }

    private static void ConfigureEuroSeparators(DisplayCurrency currency)
    {
        currency.NumberCulture.NumberFormat.NumberDecimalSeparator = ",";
        currency.NumberCulture.NumberFormat.NumberGroupSeparator = ".";
    }

    private static string AddSymbol(string amount, DisplayCurrency currency)
    {
        var separator = currency.IncludeSymbolSpace ? " " : string.Empty;
        return currency.SymbolBeforeAmount
            ? currency.Symbol + separator + amount
            : amount + separator + currency.Symbol;
    }
}
