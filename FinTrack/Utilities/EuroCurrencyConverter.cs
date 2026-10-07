using System.Globalization;

namespace FinTracker.Utilities;

public sealed class EuroCurrencyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            decimal amount => EuroFormatter.Format(amount),
            double amount => EuroFormatter.Format((decimal)amount),
            float amount => EuroFormatter.Format((decimal)amount),
            string formatted => formatted,
            _ => EuroFormatter.Format(0)
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
