using MauiApp1.Models;
using System.Collections.Generic;
using Microsoft.Maui.Controls;

namespace MauiApp1.Pages.Controls;

public partial class MonthlyExpenseChart : Border
{
    public event EventHandler<MonthlyExpenseChartData>? MonthSelected;
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<MonthlyExpenseChartData>), typeof(MonthlyExpenseChart), propertyChanged: OnItemsSourceChanged);
    public IEnumerable<MonthlyExpenseChartData>? ItemsSource { get => (IEnumerable<MonthlyExpenseChartData>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public IList<Brush> PaletteBrushes { get; private set; } = [];
    public MonthlyExpenseChart() { InitializeComponent(); }

    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var chart = (MonthlyExpenseChart)bindable;
        chart.PaletteBrushes = newValue is IEnumerable<MonthlyExpenseChartData> items
            ? items.Select(item => item.Color).ToList()
            : [];
        chart.OnPropertyChanged(nameof(PaletteBrushes));
    }

    private void OnYAxisLabelCreated(object? sender, Syncfusion.Maui.Toolkit.Charts.ChartAxisLabelEventArgs e)
        => e.Label = EuroFormatter.FormatCompact(e.Position);

    private void OnSelectionChanged(object? sender, Syncfusion.Maui.Toolkit.Charts.ChartSelectionChangedEventArgs e)
    {
        var index = e.NewIndexes?.FirstOrDefault() ?? -1;
        var item = ItemsSource?.ElementAtOrDefault(index);
        if (item is not null) MonthSelected?.Invoke(this, item);
    }
}
