using System.ComponentModel;

namespace FinTracker.Pages.Controls;

public partial class BottomTaskBar : ContentView
{
    private AppShell? _shell;
    private readonly Dictionary<string, Label> _labels = [];
    private readonly Dictionary<string, View> _icons = [];
    public BottomTaskBar()
    {
        InitializeComponent();
        var items = new[] { "Home", "Reports", "Add", "Calendar", "More" };
        for (var i = 0; i < items.Length; i++)
        {
            var item = items[i];
            var cell = new Grid { RowDefinitions = { new(new GridLength(40)), new(GridLength.Auto) }, MinimumHeightRequest = 64 };
            var icon = NavigationIcon.Create(item.ToLowerInvariant(), item == "Add" ? "TextOnPrimary" : "TextSecondary");
            if (item == "Add")
            {
                var circle = new Border { StrokeThickness = 0, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 }, WidthRequest = 38, HeightRequest = 38, Content = icon, InputTransparent = true };
                circle.SetDynamicResource(BackgroundColorProperty, "Primary"); cell.Add(circle);
            }
            else cell.Add(icon);
            var label = new Label { Text = item, FontSize = 11, HorizontalTextAlignment = TextAlignment.Center, InputTransparent = true, LineBreakMode = LineBreakMode.WordWrap };
            label.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
            label.SetDynamicResource(Label.FontFamilyProperty, "SharedFontFamily");
            label.SetDynamicResource(Label.FontSizeProperty, "SharedNavigationFontSize"); cell.Add(label, 0, 1);
            var hit = NavigationIcon.HitTarget(item == "Add" ? "Add income or expense" : item, async (_, _) => { if (Shell.Current is AppShell shell) await shell.NavigateSharedAsync(item); });
            Grid.SetRowSpan(hit, 2); cell.Add(hit);
            NavigationItems.Add(cell, i); _labels[item] = label; _icons[item] = icon;
        }
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisible = false;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        Detach();
        _shell = Shell.Current as AppShell;
        if (_shell is not null) { _shell.Navigated += OnNavigated; _shell.PropertyChanged += OnShellChanged; }
        Refresh();
    }
    private void OnUnloaded(object? sender, EventArgs e) => Detach();
    private void Detach()
    {
        if (_shell is not null) { _shell.Navigated -= OnNavigated; _shell.PropertyChanged -= OnShellChanged; }
        _shell = null;
    }
    private void OnNavigated(object? sender, ShellNavigatedEventArgs e) => Refresh();
    private void OnShellChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(AppShell.IsAuthenticated)) Refresh(); }
    private void Refresh()
    {
        IsVisible = _shell?.CanShowSharedNavigation == true;
        if (_shell is null) return;
        var selected = _shell.SelectedNavigationItem;
        foreach (var (name, label) in _labels)
        {
            label.SetDynamicResource(Label.TextColorProperty, name == selected ? "Primary" : "TextSecondary");
            label.FontAttributes = name == selected ? FontAttributes.Bold : FontAttributes.None;
            if (name != "Add") _icons[name].SetDynamicResource(Microsoft.Maui.Controls.Shapes.Shape.StrokeProperty, name == selected ? "Primary" : "TextSecondary");
        }
    }
}
