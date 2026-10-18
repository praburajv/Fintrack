using FinTracker.Models;
using Syncfusion.Maui.Toolkit.Charts;

namespace FinTracker.Pages.Controls;

public partial class CategoryExpenseChart : Border
{
    public event EventHandler<CategoryExpenseChartData>? CategorySelected;
    public event EventHandler<CategoryExpenseChartData>? CategoryLegendTapped;
    public bool IsCurrentMonth { get; set; }
    public static readonly BindableProperty ChartHeightProperty = BindableProperty.Create(nameof(ChartHeight), typeof(double), typeof(CategoryExpenseChart), 300d);
    public static readonly BindableProperty ShowLegendProperty = BindableProperty.Create(nameof(ShowLegend), typeof(bool), typeof(CategoryExpenseChart), true);
    public double ChartHeight { get => (double)GetValue(ChartHeightProperty); set => SetValue(ChartHeightProperty, value); }
    public bool ShowLegend { get => (bool)GetValue(ShowLegendProperty); set => SetValue(ShowLegendProperty, value); }
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<CategoryExpenseChartData>), typeof(CategoryExpenseChart), propertyChanged: OnItemsSourceChanged);
    public IEnumerable<CategoryExpenseChartData>? ItemsSource { get => (IEnumerable<CategoryExpenseChartData>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public IList<Brush> PaletteBrushes { get; private set; } = [];
    public CategoryExpenseChart() => InitializeComponent();
    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var chart = (CategoryExpenseChart)bindable;
        chart.PaletteBrushes = newValue is IEnumerable<CategoryExpenseChartData> items ? items.Select(item => item.Color).ToList() : [];
        chart.OnPropertyChanged(nameof(PaletteBrushes));
    }
    private void OnSelectionChanged(object? sender, ChartSelectionChangedEventArgs e)
    {
        if (e.NewIndexes is null || !e.NewIndexes.Any()) return;
        var item = ItemsSource?.ElementAtOrDefault(e.NewIndexes.First());
        if (item is not null)
            CategorySelected?.Invoke(this, item);
        if (sender is ChartSelectionBehavior selection)
            selection.ClearSelection();
    }
    private void OnLegendTapped(object? sender, TappedEventArgs e)
    {
        if (sender is Border { BindingContext: CategoryExpenseChartData item }) CategoryLegendTapped?.Invoke(this, item);
    }
}
