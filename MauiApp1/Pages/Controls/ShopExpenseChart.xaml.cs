using MauiApp1.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Syncfusion.Maui.Toolkit.Charts;

namespace MauiApp1.Pages.Controls;

public partial class ShopExpenseChart : Border
{
    public event EventHandler<ShopExpenseChartData>? ShopSelected;
    public event EventHandler<ShopExpenseChartData>? ShopLegendTapped;
    public bool IsCurrentMonth { get; set; }
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<ShopExpenseChartData>), typeof(ShopExpenseChart), propertyChanged: OnItemsSourceChanged);
    public IEnumerable<ShopExpenseChartData>? ItemsSource { get => (IEnumerable<ShopExpenseChartData>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public IList<Brush> PaletteBrushes { get; private set; } = [];
    public ShopExpenseChart() { InitializeComponent(); }

    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var chart = (ShopExpenseChart)bindable;
        chart.PaletteBrushes = newValue is IEnumerable<ShopExpenseChartData> items
            ? items.Select(item => item.Color).ToList()
            : [];
        chart.OnPropertyChanged(nameof(PaletteBrushes));
    }

    private void OnSelectionChanged(object? sender, ChartSelectionChangedEventArgs e)
    {
        var index = e.NewIndexes?.FirstOrDefault() ?? -1;
        var item = ItemsSource?.ElementAtOrDefault(index);
        if (item is not null) ShopSelected?.Invoke(this, item);
    }

    private void OnLegendTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { BindingContext: ShopExpenseChartData item })
            ShopLegendTapped?.Invoke(this, item);
    }
}
