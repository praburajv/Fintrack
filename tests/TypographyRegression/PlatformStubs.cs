global using Microsoft.Maui.Controls;
using System.ComponentModel;

namespace Microsoft.Maui.Controls
{
    [Flags] public enum FontAttributes { None = 0, Bold = 1, Italic = 2 }
    public enum LineBreakMode { NoWrap, WordWrap, CharacterWrap, HeadTruncation, TailTruncation, MiddleTruncation }
    public sealed record BindableProperty(string PropertyName, object? DefaultValue);
    public class Element : INotifyPropertyChanged
    {
        private Element? _parent;
        public Element? Parent { get => _parent; set { _parent = value; Changed(nameof(Parent)); } }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void Changed(string name) => PropertyChanged?.Invoke(this, new(name));
    }
    public class Style { public Style? BasedOn { get; set; } }
    public class View : Element
    {
        private readonly Dictionary<BindableProperty, object?> _values = [];
        private Style? _style;
        public Style? Style { get => _style; set { _style = value; Changed(nameof(Style)); } }
        public bool IsLoaded { get; private set; }
        public event EventHandler? Loaded;
        public object? GetValue(BindableProperty property) => _values.TryGetValue(property, out var value) ? value : property.DefaultValue;
        public void SetValue(BindableProperty property, object? value) { if (Equals(GetValue(property), value)) return; _values[property] = value; Changed(property.PropertyName); }
        public void Load() { IsLoaded = true; Loaded?.Invoke(this, EventArgs.Empty); }
    }
    public class TextView : View
    {
        public static readonly BindableProperty FontFamilyProperty = new(nameof(FontFamily), null);
        public static readonly BindableProperty FontAttributesProperty = new(nameof(FontAttributes), FontAttributes.None);
        public static readonly BindableProperty FontAutoScalingEnabledProperty = new(nameof(FontAutoScalingEnabled), true);
        public string? FontFamily { get => (string?)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
        public FontAttributes FontAttributes { get => (FontAttributes)GetValue(FontAttributesProperty)!; set => SetValue(FontAttributesProperty, value); }
        public bool FontAutoScalingEnabled { get => (bool)GetValue(FontAutoScalingEnabledProperty)!; set => SetValue(FontAutoScalingEnabledProperty, value); }
        public double FontSize { get; set; } = 17;
    }
    public class Label : TextView { public string? Text { get; set; } public int MaxLines { get; set; } = -1; public LineBreakMode LineBreakMode { get; set; } = LineBreakMode.WordWrap; }
    public class Button : TextView { }
    public class Entry : TextView { }
    public class Editor : TextView { }
    public class Picker : TextView { }
    public class DatePicker : TextView { }
    public class TimePicker : TextView { }
    public class SearchBar : TextView { }
    public class RadioButton : TextView { }
    public sealed class Application { public static Application? Current { get; set; } public Dictionary<string, object> Resources { get; } = []; }
}
namespace Microsoft.Maui.Handlers
{
    public sealed class TestMapper
    {
        public List<Action<object, object>> Actions { get; } = [];
        public void AppendToMapping(string key, Action<object, object> action) => Actions.Add(action);
        public void Create(object control) { foreach (var action in Actions) action(this, control); }
    }
    public static class LabelHandler { public static TestMapper Mapper { get; } = new(); }
    public static class ButtonHandler { public static TestMapper Mapper { get; } = new(); }
    public static class EntryHandler { public static TestMapper Mapper { get; } = new(); }
    public static class EditorHandler { public static TestMapper Mapper { get; } = new(); }
    public static class PickerHandler { public static TestMapper Mapper { get; } = new(); }
    public static class DatePickerHandler { public static TestMapper Mapper { get; } = new(); }
    public static class TimePickerHandler { public static TestMapper Mapper { get; } = new(); }
    public static class SearchBarHandler { public static TestMapper Mapper { get; } = new(); }
    public static class RadioButtonHandler { public static TestMapper Mapper { get; } = new(); }
}
namespace Syncfusion.Maui.Toolkit.Charts { public sealed class PlotSurface : View { } }
public sealed class CategoryExpenseChart : View { }
public sealed class GlobalDisplaySettingsPage : View { }
