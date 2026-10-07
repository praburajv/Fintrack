using System.Globalization;

namespace FinTracker.Utilities;

public static class ShopBillNavigation
{
    public static string Route(int shopId, DateTime from, DateTime endExclusive)
    {
        if (shopId <= 0 || from == default || endExclusive <= from)
            throw new ArgumentException("A shop and valid report period are required.");
        return string.Create(CultureInfo.InvariantCulture,
            $"dailybills?shopId={shopId}&from={from:yyyy-MM-dd}&to={endExclusive:yyyy-MM-dd}");
    }
}
