using System.Globalization;
using System.Resources;
using FinTracker.Utilities;
using Microsoft.Maui.Storage;

namespace FinTracker.Pages;

public partial class WelcomePage : ContentPage, IQueryAttributable
{
    private const string WelcomeCompletedKey = "fintracker_welcome_completed";
    private const string LanguageKey = "fintracker_language";
    private static readonly ResourceManager Strings = new("FinTracker.Resources.Localization.Welcome", typeof(WelcomePage).Assembly);

    private string _language;
    private bool _isReplay;
    private bool _compactLayout;
    private bool _isDragging;
    private bool _isAnimating;
    private double _dragStart;
    private double _progress;
    private int _navigationStarted;

    public static bool HasCompletedWelcome => Preferences.Get(WelcomeCompletedKey, false);

    public WelcomePage()
    {
        InitializeComponent();
        _language = WelcomeFlow.ResolveLanguage(Preferences.Get(LanguageKey, string.Empty), CultureInfo.CurrentUICulture.Name);
        SliderContent.SizeChanged += (_, _) =>
        {
            if (_navigationStarted == 0 && !_isDragging && !_isAnimating)
                SetProgress(Math.Min(_progress, GetTravelDistance()));
        };
        ApplyLanguage();
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _isReplay = query.TryGetValue("entry", out var entry)
            && string.Equals(entry?.ToString(), "replay", StringComparison.OrdinalIgnoreCase);
        ApplyLanguage();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_navigationStarted == 0)
        {
            _language = WelcomeFlow.ResolveLanguage(Preferences.Get(LanguageKey, string.Empty), CultureInfo.CurrentUICulture.Name);
            ApplyLanguage();
            SetProgress(0);
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (WelcomeArtwork is null || width <= 0 || height <= 0) return;

        var compact = height / width < 2.0;
        if (compact == _compactLayout) return;
        _compactLayout = compact;
        // On shorter screens, show the entire illustration and leave room for its introduction text.
        WelcomeArtwork.Aspect = compact ? Aspect.AspectFit : Aspect.AspectFill;
        WelcomeControls.Padding = new Thickness(20, 12, 20, compact ? 0 : 20);
        SliderActions.Spacing = compact ? 4 : 8;
        SliderTrack.HeightRequest = compact ? 64 : 72;
        SliderContent.HeightRequest = compact ? 60 : 68;
        SliderThumb.WidthRequest = compact ? 56 : 64;
        SliderThumb.HeightRequest = compact ? 56 : 64;
        SliderText.FontSize = compact ? 16 : 19;
        if (!_isDragging && !_isAnimating && _navigationStarted == 0)
            SetProgress(0);
    }

    private string Localized(string key) => Strings.GetString(key, CultureInfo.GetCultureInfo(_language)) ?? key;

    private void ApplyLanguage()
    {
        WelcomeArtwork.Source = _language == "pt"
            ? "fintracker_pt__w_s_no_slider_n1.png"
            : "fintracker_en__w_s_no_slider_n1.png";
        SliderText.Text = Localized(_isReplay ? "SwipeToReturn" : "SwipeToStart");
        AccessibleContinueButton.Text = Localized(_isReplay ? "TapToReturn" : "TapToContinue");
        SemanticProperties.SetDescription(SliderTrack, Localized("SwipeDescription"));
        SemanticProperties.SetDescription(AccessibleContinueButton, AccessibleContinueButton.Text);
        LanguageButton.Text = _language == "pt" ? "EN" : "PT";
        SemanticProperties.SetDescription(LanguageButton, Localized("LanguageDescription"));
    }

    private void OnLanguageClicked(object? sender, EventArgs e)
    {
        if (_navigationStarted != 0 || _isAnimating || _isDragging) return;
        _language = _language == "pt" ? "en" : "pt";
        Preferences.Set(LanguageKey, _language);
        ApplyLanguage();
    }

    private double GetTravelDistance() => Math.Max(0, SliderContent.Width - SliderThumb.WidthRequest - 8);

    private void SetProgress(double position)
    {
        _progress = Math.Clamp(position, 0, GetTravelDistance());
        SliderThumb.TranslationX = _progress;
        SliderFill.WidthRequest = SliderThumb.WidthRequest + 4 + _progress;
    }

    private async void OnSliderPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_navigationStarted != 0 || _isAnimating || GetTravelDistance() <= 0) return;

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _dragStart = _progress;
                _isDragging = true;
                break;
            case GestureStatus.Running:
                if (!_isDragging)
                {
                    _dragStart = _progress;
                    _isDragging = true;
                }
                SetProgress(_dragStart + e.TotalX);
                break;
            case GestureStatus.Completed:
                if (!_isDragging) return;
                _isDragging = false;
                if (WelcomeFlow.HasCompletedSwipe(_progress, GetTravelDistance()))
                    await CompleteAsync();
                else
                    await AnimateProgressAsync(0);
                break;
            case GestureStatus.Canceled:
                _isDragging = false;
                await AnimateProgressAsync(0);
                break;
        }
    }

    private async void OnAccessibleContinueClicked(object? sender, EventArgs e) => await CompleteAsync();

    private async Task AnimateProgressAsync(double destination)
    {
        _isAnimating = true;
        try
        {
            var start = _progress;
            if (Math.Abs(start - destination) < 0.5)
            {
                SetProgress(destination);
                return;
            }

            var finished = new TaskCompletionSource();
            var animation = new Animation(SetProgress, start, destination, Easing.CubicOut);
            animation.Commit(this, "WelcomeSliderProgress", 16, 190, Easing.CubicOut,
                (_, _) => finished.TrySetResult());
            await finished.Task;
        }
        finally
        {
            _isAnimating = false;
        }
    }

    private async Task CompleteAsync()
    {
        if (Interlocked.Exchange(ref _navigationStarted, 1) != 0) return;

        SliderTrack.InputTransparent = true;
        AccessibleContinueButton.IsEnabled = false;
        LanguageButton.IsEnabled = false;
        var wasCompleted = HasCompletedWelcome;
        try
        {
            await AnimateProgressAsync(GetTravelDistance());
            var authenticated = (Shell.Current as AppShell)?.IsAuthenticated == true;
            var route = WelcomeFlow.CompletionRoute(_isReplay, authenticated);
            if (!_isReplay) Preferences.Set(WelcomeCompletedKey, true);
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Welcome navigation failed: {ex}");
            if (!_isReplay && !wasCompleted) Preferences.Set(WelcomeCompletedKey, false);
            await AnimateProgressAsync(0);
            SliderTrack.InputTransparent = false;
            AccessibleContinueButton.IsEnabled = true;
            LanguageButton.IsEnabled = true;
            Interlocked.Exchange(ref _navigationStarted, 0);
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("FinTracker", Localized("NavigationError"), "OK");
        }
    }
}
