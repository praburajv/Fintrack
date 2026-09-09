using System.Globalization;

namespace MauiApp1.Utilities;

public sealed class EuroCompactConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value switch
        {
            decimal amount => EuroFormatter.FormatCalendarTile(amount),
            double amount => EuroFormatter.FormatCalendarTile((decimal)amount),
            float amount => EuroFormatter.FormatCalendarTile((decimal)amount),
            _ => EuroFormatter.FormatCalendarTile(0)
        };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
