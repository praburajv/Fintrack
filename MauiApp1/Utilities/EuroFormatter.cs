using System.Globalization;

namespace MauiApp1.Utilities;

public static class EuroFormatter
{
    private static readonly CultureInfo EuroCulture = CreateEuroCulture();

    private static CultureInfo CreateEuroCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("pt-PT").Clone();
        culture.NumberFormat.NumberDecimalSeparator = ",";
        culture.NumberFormat.NumberGroupSeparator = ".";
        return culture;
    }

    public static string Format(decimal value) => value.ToString("N2", EuroCulture) + " €";

    public static string FormatCalendarTile(decimal value)
    {
        var amount = Math.Max(0, value);
        if (amount >= 1000)
        {
            var thousands = (double)amount / 1000d;
            return thousands.ToString(thousands % 1 == 0 ? "0" : "0.#", EuroCulture) + "K €";
        }

        // Preserve meaningful cents while keeping the label short enough for a
        // seven-column calendar tile. Whole amounts intentionally omit decimals.
        return amount == decimal.Truncate(amount)
            ? amount.ToString("0", EuroCulture) + " €"
            : amount.ToString("0.#", EuroCulture) + " €";
    }

    public static string FormatCompact(double value)
    {
        var amount = Math.Max(0, value);
        if (amount >= 1000)
        {
            var thousands = amount / 1000d;
            return thousands.ToString(thousands % 1 == 0 ? "0" : "0.#", EuroCulture) + "K €";
        }

        return Math.Round(amount).ToString("0", EuroCulture) + " €";
    }
}
