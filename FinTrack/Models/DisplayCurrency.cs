using System.Globalization;

namespace FinTracker.Models;

public sealed class DisplayCurrency
{
    public DisplayCurrency(string code, string name, string symbol, string cultureName,
        bool symbolBeforeAmount, bool includeSymbolSpace, int decimalDigits = 2)
    {
        Code = code;
        Name = name;
        Symbol = symbol;
        SymbolBeforeAmount = symbolBeforeAmount;
        IncludeSymbolSpace = includeSymbolSpace;
        DecimalDigits = decimalDigits;
        NumberCulture = (CultureInfo)CultureInfo.GetCultureInfo(cultureName).Clone();
        NumberCulture.NumberFormat.NumberDecimalDigits = decimalDigits;
    }

    public string Code { get; }
    public string Name { get; }
    public string Symbol { get; }
    public bool SymbolBeforeAmount { get; }
    public bool IncludeSymbolSpace { get; }
    public int DecimalDigits { get; }
    public CultureInfo NumberCulture { get; }
    public string DisplayName => $"{Code} — {Name} ({Symbol})";

    public override string ToString() => DisplayName;
}
