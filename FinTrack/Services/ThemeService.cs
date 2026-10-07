using Microsoft.Maui.Storage;

namespace FinTracker.Services;

public sealed class ThemeService : IThemeService
{
    private const string ColorThemeKey = "SelectedColorTheme";
    private const string AppearanceKey = "SelectedAppearance";
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _themes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Red"] = CreateTheme("#B42318", "#8E1B12", "#F04438", "#D92D20"),
        ["Blue"] = CreateTheme("#175CD3", "#1246A4", "#84CAFF", "#2E90FA"),
        ["Green"] = CreateTheme("#087443", "#055B35", "#6CE9A6", "#12B76A"),
        ["Purple"] = CreateTheme("#512BD4", "#2B0B98", "#AC99EA", "#7F56D9")
    };

    public string CurrentColorTheme { get; private set; } = "Red";
    public AppTheme CurrentAppearance { get; private set; } = AppTheme.Unspecified;
    public event EventHandler? ThemeChanged;

    public void LoadSavedPreferences()
    {
        var savedTheme = Preferences.Get(ColorThemeKey, "Red");
        ApplyColorTheme(_themes.ContainsKey(savedTheme) ? savedTheme : "Red", save: false);

        var savedAppearance = Preferences.Get(AppearanceKey, string.Empty);
        var appearance = Enum.TryParse<AppTheme>(savedAppearance, true, out var parsed)
            ? parsed
            : AppTheme.Unspecified;
        ApplyAppearance(appearance, save: false);
    }

    public void ApplyColorTheme(string themeName) => ApplyColorTheme(themeName, save: true);

    public void ApplyAppearance(AppTheme appearance) => ApplyAppearance(appearance, save: true);

    private void ApplyColorTheme(string themeName, bool save)
    {
        if (!_themes.TryGetValue(themeName, out var values))
            themeName = "Red";
        values = _themes[themeName];
        CurrentColorTheme = themeName;
        if (Application.Current is not null)
        {
            foreach (var pair in values)
                Application.Current.Resources[pair.Key] = Color.FromArgb(pair.Value);
        }
        if (save) Preferences.Set(ColorThemeKey, themeName);
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private void ApplyAppearance(AppTheme appearance, bool save)
    {
        CurrentAppearance = appearance;
        if (Application.Current is not null)
        {
            Application.Current.UserAppTheme = appearance;
            var isDark = appearance == AppTheme.Dark;
            Application.Current.Resources["Background"] = Color.FromArgb(isDark ? "#101828" : "#F8FAFC");
            Application.Current.Resources["Surface"] = Color.FromArgb(isDark ? "#1D2939" : "#FFFFFF");
            Application.Current.Resources["SurfaceSecondary"] = Color.FromArgb(isDark ? "#344054" : "#F2F4F7");
            Application.Current.Resources["TextPrimary"] = Color.FromArgb(isDark ? "#F2F4F7" : "#101828");
            Application.Current.Resources["TextSecondary"] = Color.FromArgb(isDark ? "#D0D5DD" : "#475467");
        }
        if (save) Preferences.Set(AppearanceKey, appearance.ToString());
        ThemeChanged?.Invoke(this, EventArgs.Empty);
    }

    private static IReadOnlyDictionary<string, string> CreateTheme(string primary, string primaryDark, string primaryLight, string accent) => new Dictionary<string, string>
    {
        ["Primary"] = primary,
        ["PrimaryDark"] = primaryDark,
        ["PrimaryLight"] = primaryLight,
        ["Accent"] = accent,
        ["Secondary"] = primaryLight,
        ["Tertiary"] = primaryDark,
        ["Highlight"] = "#F79009",
        ["Danger"] = "#B42318",
        ["Success"] = "#027A48",
        ["Warning"] = "#B54708",
        ["TextOnPrimary"] = "#FFFFFF",
        ["TextOnDanger"] = "#FFFFFF",
        ["Border"] = primaryLight,
        ["Surface"] = "#FFFFFF",
        ["SurfaceSecondary"] = "#F2F4F7",
        ["Background"] = "#F8FAFC",
        ["TextPrimary"] = "#101828",
        ["TextSecondary"] = "#475467",
        ["BottomNavigationBackground"] = "#FFFFFF",
        ["BottomNavigationInactive"] = "#667085",
        ["BottomNavigationActive"] = primary,
        ["LightBackground"] = "#F8FAFC",
        ["DarkBackground"] = "#101828",
        ["LightSecondaryBackground"] = "#F2F4F7",
        ["DarkSecondaryBackground"] = "#1D2939",
        ["DarkOnLightBackground"] = "#101828",
        ["LightOnDarkBackground"] = "#F2F4F7"
    };
}
