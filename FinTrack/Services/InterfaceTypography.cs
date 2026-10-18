using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.Maui.Handlers;

namespace FinTracker.Services;

/// <summary>Typography for app-owned text only. Existing global styles and charts are untouched.</summary>
public static class InterfaceTypography
{
    public const string Regular = "InterRegular";
    public const string Medium = "InterMedium";
    public const string Semibold = "InterSemibold";
    private static readonly ConditionalWeakTable<View, TextState> States = new();
    private static int _configured;
    private static readonly string[] HeadingStyles = ["Title1", "Title2", "Title3", "LargeTitle", "Display", "Headline", "SubHeadline", "Caption1Strong", "Body1Strong", "Body2Strong"];

    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) != 0) return;
        LabelHandler.Mapper.AppendToMapping(nameof(InterfaceTypography), (_, view) => Attach(view as Label));
        ButtonHandler.Mapper.AppendToMapping(nameof(InterfaceTypography), (_, view) => Attach(view as Button));
        EntryHandler.Mapper.AppendToMapping(nameof(InterfaceTypography), (_, view) => Attach(view as Entry));
        EditorHandler.Mapper.AppendToMapping(nameof(InterfaceTypography), (_, view) => Attach(view as Editor));
        PickerHandler.Mapper.AppendToMapping(nameof(InterfaceTypography), (_, view) => Attach(view as Picker));
        DatePickerHandler.Mapper.AppendToMapping(nameof(InterfaceTypography), (_, view) => Attach(view as DatePicker));
        TimePickerHandler.Mapper.AppendToMapping(nameof(InterfaceTypography), (_, view) => Attach(view as TimePicker));
        SearchBarHandler.Mapper.AppendToMapping(nameof(InterfaceTypography), (_, view) => Attach(view as SearchBar));
        RadioButtonHandler.Mapper.AppendToMapping(nameof(InterfaceTypography), (_, view) => Attach(view as RadioButton));
        // Reapply the exact weight after MAUI maps a changed font/size, without changing
        // FontAttributes itself (which may be controlled by a binding or visual state).
        LabelHandler.Mapper.AppendToMapping("Font", (_, view) => Refresh(view as View));
        ButtonHandler.Mapper.AppendToMapping("Font", (_, view) => Refresh(view as View));
        EntryHandler.Mapper.AppendToMapping("Font", (_, view) => Refresh(view as View));
        EditorHandler.Mapper.AppendToMapping("Font", (_, view) => Refresh(view as View));
        PickerHandler.Mapper.AppendToMapping("Font", (_, view) => Refresh(view as View));
        DatePickerHandler.Mapper.AppendToMapping("Font", (_, view) => Refresh(view as View));
        TimePickerHandler.Mapper.AppendToMapping("Font", (_, view) => Refresh(view as View));
        SearchBarHandler.Mapper.AppendToMapping("Font", (_, view) => Refresh(view as View));
        RadioButtonHandler.Mapper.AppendToMapping("Font", (_, view) => Refresh(view as View));
    }

    private static void Refresh(View? view)
    {
        if (view is not null && view.IsLoaded) Apply(view);
    }

    public static void Attach(View? view)
    {
        if (view is null || !TryProperties(view, out _, out _, out _)) return;
        if (States.TryGetValue(view, out _)) return;
        States.Add(view, new TextState());
        // Wait until the logical parent chain is complete before applying the chart guard.
        view.Loaded += OnLoaded;
        view.PropertyChanged += OnTextPropertyChanged;
        if (view.IsLoaded) Apply(view);
    }

    private static void OnLoaded(object? sender, EventArgs args)
    {
        if (sender is View view) Apply(view);
    }

    private static void OnTextPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not View view || !view.IsLoaded || !States.TryGetValue(view, out var state) || state.Applying) return;
        if (args.PropertyName is not ("FontFamily" or "FontAttributes" or "Style" or "Parent")) return;
        if (TryProperties(view, out var family, out var attributes, out _))
        {
            if (args.PropertyName == "FontFamily") state.SourceFamily = view.GetValue(family) as string;
            if (args.PropertyName == "FontAttributes") state.SourceAttributes = (FontAttributes)view.GetValue(attributes);
        }
        Apply(view);
    }

    private static bool IsExcluded(View view, string? family)
    {
        for (Element? current = view; current is not null; current = current.Parent)
        {
            var type = current.GetType();
            if (type.Name.Contains("Chart", StringComparison.OrdinalIgnoreCase)
                || type.Namespace?.Contains(".Charts", StringComparison.Ordinal) == true
                || type.Name == "GlobalDisplaySettingsPage") return true;
        }
        // Font glyphs and deliberate specialist fonts must keep their original typeface.
        if (view is Label label && label.Text?.Any(c => c is >= '\uE000' and <= '\uF8FF') == true) return true;
        return !string.IsNullOrEmpty(family) && family is not
            ("OpenSansRegular" or "OpenSansSemibold" or "SegoeSemibold" or ".SFUI-SemiBold" or Regular or Medium or Semibold);
    }

    private static bool IsHeading(View view, TextState state)
    {
        if ((state.SourceAttributes & FontAttributes.Bold) != 0
            || state.SourceFamily is "OpenSansSemibold" or "SegoeSemibold" or ".SFUI-SemiBold" or Semibold) return true;
        for (var style = view.Style; style is not null; style = style.BasedOn)
            foreach (var key in HeadingStyles)
                if (Application.Current?.Resources.TryGetValue(key, out var heading) == true && ReferenceEquals(style, heading)) return true;
        return false;
    }

    private static void Apply(View view)
    {
        if (!States.TryGetValue(view, out var state) || state.Applying || !TryProperties(view, out var family, out var attributes, out var scaling)) return;
        state.Applying = true;
        try
        {
            if (!state.Captured)
            {
                state.SourceFamily = view.GetValue(family) as string;
                state.SourceAttributes = (FontAttributes)view.GetValue(attributes);
                state.SourceScaling = (bool)view.GetValue(scaling);
                if (view is Label original) { state.MaxLines = original.MaxLines; state.LineBreak = original.LineBreakMode; }
                state.Captured = true;
            }
            else
            {
                var currentFamily = view.GetValue(family) as string;
                if (currentFamily != state.AppliedFamily) state.SourceFamily = currentFamily;
                state.SourceAttributes = (FontAttributes)view.GetValue(attributes);
            }
            if (IsExcluded(view, state.SourceFamily))
            {
                if (state.Applied)
                {
                    view.SetValue(family, state.SourceFamily);
                    view.SetValue(scaling, state.SourceScaling);
                    if (view is Label original) { original.MaxLines = state.MaxLines; original.LineBreakMode = state.LineBreak; }
                    state.Applied = false;
                }
                return;
            }
            var alias = view is Button or RadioButton ? Medium : IsHeading(view, state) ? Semibold : state.SourceFamily == Medium ? Medium : Regular;
            state.AppliedFamily = alias;
            if (!Equals(view.GetValue(family), alias)) view.SetValue(family, alias);
            view.SetValue(scaling, true);
            ApplyNativeWeight(view, alias, (state.SourceAttributes & FontAttributes.Italic) != 0);
            if (view is Label label && label.LineBreakMode is LineBreakMode.TailTruncation or LineBreakMode.HeadTruncation or LineBreakMode.MiddleTruncation)
            {
                label.MaxLines = -1;
                label.LineBreakMode = LineBreakMode.WordWrap;
            }
            state.Applied = true;
        }
        finally { state.Applying = false; }
    }

    internal static int WeightForAlias(string alias) => alias == Semibold ? 600 : alias == Medium ? 500 : 400;

    private static void ApplyNativeWeight(View view, string alias, bool italic)
    {
#if ANDROID
        if (view.Handler?.PlatformView is global::Android.Widget.TextView text)
        {
            global::Android.Graphics.Typeface? typeface;
            if (OperatingSystem.IsAndroidVersionAtLeast(28))
                typeface = global::Android.Graphics.Typeface.Create(text.Typeface, WeightForAlias(alias), italic);
            else
            {
                var file = alias == Semibold ? "Inter-SemiBold.ttf" : alias == Medium ? "Inter-Medium.ttf" : "Inter-Regular.ttf";
                typeface = LegacyTypefaces.GetOrAdd(file, name => global::Android.Graphics.Typeface.CreateFromAsset(global::Android.App.Application.Context.Assets, name)!);
                if (italic) typeface = global::Android.Graphics.Typeface.Create(typeface, global::Android.Graphics.TypefaceStyle.Italic);
            }
            if (typeface is not null && text.Typeface?.Handle != typeface.Handle) text.Typeface = typeface;
        }
#elif WINDOWS
        var weight = new global::Windows.UI.Text.FontWeight { Weight = (ushort)WeightForAlias(alias) };
        if (view.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.TextBlock label && label.FontWeight.Weight != weight.Weight) label.FontWeight = weight;
        else if (view.Handler?.PlatformView is Microsoft.UI.Xaml.Controls.Control control && control.FontWeight.Weight != weight.Weight) control.FontWeight = weight;
#endif
    }

#if ANDROID
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, global::Android.Graphics.Typeface> LegacyTypefaces = new();
#endif

    private static bool TryProperties(View view, out BindableProperty family, out BindableProperty attributes, out BindableProperty scaling)
    {
        (family, attributes, scaling) = view switch
        {
            Label => (Label.FontFamilyProperty, Label.FontAttributesProperty, Label.FontAutoScalingEnabledProperty),
            Button => (Button.FontFamilyProperty, Button.FontAttributesProperty, Button.FontAutoScalingEnabledProperty),
            Entry => (Entry.FontFamilyProperty, Entry.FontAttributesProperty, Entry.FontAutoScalingEnabledProperty),
            Editor => (Editor.FontFamilyProperty, Editor.FontAttributesProperty, Editor.FontAutoScalingEnabledProperty),
            Picker => (Picker.FontFamilyProperty, Picker.FontAttributesProperty, Picker.FontAutoScalingEnabledProperty),
            DatePicker => (DatePicker.FontFamilyProperty, DatePicker.FontAttributesProperty, DatePicker.FontAutoScalingEnabledProperty),
            TimePicker => (TimePicker.FontFamilyProperty, TimePicker.FontAttributesProperty, TimePicker.FontAutoScalingEnabledProperty),
            SearchBar => (SearchBar.FontFamilyProperty, SearchBar.FontAttributesProperty, SearchBar.FontAutoScalingEnabledProperty),
            RadioButton => (RadioButton.FontFamilyProperty, RadioButton.FontAttributesProperty, RadioButton.FontAutoScalingEnabledProperty),
            _ => (null!, null!, null!)
        };
        return family is not null;
    }

    private sealed class TextState
    {
        public bool Applying, Captured, Applied;
        public string? SourceFamily;
        public string? AppliedFamily;
        public FontAttributes SourceAttributes;
        public bool SourceScaling;
        public int MaxLines;
        public LineBreakMode LineBreak;
    }
}
