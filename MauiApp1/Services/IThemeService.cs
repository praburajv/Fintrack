namespace MauiApp1.Services;

public interface IThemeService
{
    string CurrentColorTheme { get; }
    AppTheme CurrentAppearance { get; }
    event EventHandler? ThemeChanged;
    void LoadSavedPreferences();
    void ApplyColorTheme(string themeName);
    void ApplyAppearance(AppTheme appearance);
}
