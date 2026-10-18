using Android.Views;
using AndroidX.Core.View;
using FinTracker.Services;
using Google.Android.Material.AppBar;
using Microsoft.Maui.Platform;
using System.Runtime.CompilerServices;
using MauiApplication = Microsoft.Maui.Controls.Application;
using NativeView = Android.Views.View;

namespace FinTracker.Platforms.Android;

/// <summary>Colors Android's status-bar backing without changing MAUI's insets or navigation.</summary>
internal sealed class AndroidTopBarTheme : IDisposable
{
    private readonly MainActivity _activity;
    private readonly IThemeService _themes;
    private readonly MauiApplication _application;
    private readonly NativeView _decor;
    private readonly ConditionalWeakTable<AppBarLayout, AppliedColor> _appBarColors = new();
    private bool _disposed;
    private bool _refreshQueued;

    public AndroidTopBarTheme(MainActivity activity, IThemeService themes, MauiApplication application)
    {
        _activity = activity;
        _themes = themes;
        _application = application;
        _decor = activity.Window!.DecorView;
        _themes.ThemeChanged += OnThemeChanged;
        _application.RequestedThemeChanged += OnRequestedThemeChanged;
        // Shell can create another native app bar when navigating. Paint after it is laid out,
        // retaining the existing inset listener, padding, keyboard handling and content bounds.
        _decor.ViewTreeObserver!.GlobalLayout += OnGlobalLayout;
        Refresh();
    }

    private void OnThemeChanged(object? sender, EventArgs e) => Refresh();
    private void OnRequestedThemeChanged(object? sender, AppThemeChangedEventArgs e) => Refresh();
    private void OnGlobalLayout(object? sender, EventArgs e) => Apply();

    public void Refresh()
    {
        if (_disposed || _refreshQueued || _activity.IsFinishing || _activity.IsDestroyed)
            return;

        _refreshQueued = true;
        // Run after MAUI's native appearance updates, and coalesce color/appearance notifications.
        _decor.Post(() =>
        {
            _refreshQueued = false;
            if (_disposed) return;
            // Native appearance/lifecycle updates may have replaced a background drawable.
            _appBarColors.Clear();
            Apply();
        });
    }

    private void Apply()
    {
        if (_disposed || _activity.IsFinishing || _activity.IsDestroyed || AppShell.IsClosing)
            return;
        var window = _activity.Window;
        if (window is null || !_application.Resources.TryGetValue("PrimaryDark", out var value)
            || value is not Microsoft.Maui.Graphics.Color color)
            return;

        var nativeColor = color.ToPlatform();
        PaintAppBars(_decor, nativeColor);

        // On Android 15+ the transparent status bar shows the AppBarLayout behind it.
        // Older Android also needs the window color. Do not alter edge-to-edge/inset flags.
        if (!OperatingSystem.IsAndroidVersionAtLeast(35))
            window.SetStatusBarColor(nativeColor);

        // All current PrimaryDark palettes use the same white foreground as the shared App Bar.
        // AppearanceLightStatusBars means DARK icons, so keep it false even in Light appearance.
        if (WindowCompat.GetInsetsController(window, _decor) is { } controller)
            controller.AppearanceLightStatusBars = false;
    }

    private void PaintAppBars(NativeView view, global::Android.Graphics.Color color)
    {
        if (view is AppBarLayout appBar)
        {
            // Material can wrap a ColorDrawable in another drawable. Remember the applied
            // color per native app bar rather than repainting every subsequent layout pass.
            var applied = _appBarColors.GetValue(appBar, _ => new AppliedColor());
            if (applied.Color is not { } current || current != color)
            {
                appBar.SetBackgroundColor(color);
                applied.Color = color;
            }
            return;
        }

        if (view is ViewGroup group)
        {
            for (var i = 0; i < group.ChildCount; i++)
                if (group.GetChildAt(i) is { } child)
                    PaintAppBars(child, color);
        }
    }

    private sealed class AppliedColor
    {
        public global::Android.Graphics.Color? Color { get; set; }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _themes.ThemeChanged -= OnThemeChanged;
        _application.RequestedThemeChanged -= OnRequestedThemeChanged;
        if (_decor.ViewTreeObserver is { IsAlive: true } observer)
            observer.GlobalLayout -= OnGlobalLayout;
    }
}
