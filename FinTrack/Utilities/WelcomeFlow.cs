namespace FinTracker.Utilities;

/// <summary>Small, UI-independent rules for Welcome gesture and routing.</summary>
public static class WelcomeFlow
{
    public const double CompletionFraction = 0.8;

    public static bool HasCompletedSwipe(double position, double travel)
        => double.IsFinite(position) && double.IsFinite(travel) && travel > 0
            && position >= travel * CompletionFraction;

    public static string CompletionRoute(bool replay, bool authenticated)
        => replay && authenticated ? ".." : "//login";

    public static string ResolveLanguage(string? savedLanguage, string? deviceLanguage)
    {
        if (IsPortuguese(savedLanguage)) return "pt";
        if (IsEnglish(savedLanguage)) return "en";
        return IsPortuguese(deviceLanguage) ? "pt" : "en";
    }

    private static bool IsPortuguese(string? value)
        => value is not null && (value.Equals("pt", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("pt-", StringComparison.OrdinalIgnoreCase));

    private static bool IsEnglish(string? value)
        => value is not null && (value.Equals("en", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("en-", StringComparison.OrdinalIgnoreCase));
}
