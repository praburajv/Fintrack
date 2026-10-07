using Microsoft.Maui.Controls;

namespace FinTracker.Utilities;

public static class ChartColorPalette
{
    private static readonly Color[] ShopColors =
    [
        Color.FromArgb("#E15759"), Color.FromArgb("#4E79A7"), Color.FromArgb("#59A14F"),
        Color.FromArgb("#F28E2B"), Color.FromArgb("#B07AA1"), Color.FromArgb("#76B7B2"),
        Color.FromArgb("#EDC948"), Color.FromArgb("#FF9DA7")
    ];

    private static readonly Color[] MonthColors =
    [
        Color.FromArgb("#4E79A7"), Color.FromArgb("#F28E2B"), Color.FromArgb("#E15759"),
        Color.FromArgb("#76B7B2"), Color.FromArgb("#59A14F"), Color.FromArgb("#EDC948"),
        Color.FromArgb("#B07AA1"), Color.FromArgb("#FF9DA7"), Color.FromArgb("#9C755F"),
        Color.FromArgb("#BAB0AC"), Color.FromArgb("#2F5597"), Color.FromArgb("#70AD47")
    ];

    private static readonly Dictionary<string, int> ShopColorIndices = new(StringComparer.OrdinalIgnoreCase);
    private static int _nextShopColorIndex;

    public static Brush ForShop(string shopName)
    {
        var key = shopName.Trim();
        if (!ShopColorIndices.TryGetValue(key, out var index))
        {
            index = _nextShopColorIndex++ % ShopColors.Length;
            ShopColorIndices[key] = index;
        }

        return new SolidColorBrush(ShopColors[index]);
    }

    public static Brush ForMonth(int monthNumber) =>
        new SolidColorBrush(MonthColors[Math.Clamp(monthNumber - 1, 0, MonthColors.Length - 1)]);
}
