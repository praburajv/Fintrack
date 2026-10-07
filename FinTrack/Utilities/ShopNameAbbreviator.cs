namespace FinTracker.Utilities;

public static class ShopNameAbbreviator
{
    private const int MaximumLength = 14;

    public static string Abbreviate(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (name.Length <= MaximumLength) return name;

        if (words.Length > 1)
        {
            var initials = string.Concat(words.Select(word => char.ToUpperInvariant(word[0])));
            if (initials.Length <= MaximumLength) return initials;
        }

        return name[..(MaximumLength - 1)] + "…";
    }
}
