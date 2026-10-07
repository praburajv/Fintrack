using System.Globalization;
using FinTracker.Models;
using Syncfusion.Maui.Toolkit.Charts;

namespace FinTracker.Pages.Controls;

public partial class MonthlyVisitsChart : Border
{
    public event EventHandler<MonthlyVisitData>? MonthSelected;

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource), typeof(IEnumerable<MonthlyVisitData>), typeof(MonthlyVisitsChart), propertyChanged: OnItemsSourceChanged);

    public IEnumerable<MonthlyVisitData>? ItemsSource
    {
        get => (IEnumerable<MonthlyVisitData>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IList<Brush> PaletteBrushes { get; private set; } = [];

    public MonthlyVisitsChart() => InitializeComponent();

    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var chart = (MonthlyVisitsChart)bindable;
        chart.PaletteBrushes = newValue is IEnumerable<MonthlyVisitData> items ? items.Select(item => item.Color).ToList() : [];
        chart.OnPropertyChanged(nameof(PaletteBrushes));
    }

    private void OnYAxisLabelCreated(object? sender, ChartAxisLabelEventArgs e)
        => e.Label = Math.Max(0, Math.Round(e.Position)).ToString(CultureInfo.InvariantCulture);

    private void OnSelectionChanged(object? sender, ChartSelectionChangedEventArgs e)
    {
        if (e.NewIndexes is null || !e.NewIndexes.Any()) return;
        var item = ItemsSource?.ElementAtOrDefault(e.NewIndexes.First());
        if (item is { VisitCount: > 0 }) MonthSelected?.Invoke(this, item);
        if (sender is ChartSelectionBehavior selection) selection.ClearSelection();
    }
}
