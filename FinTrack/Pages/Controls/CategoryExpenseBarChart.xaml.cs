using FinTracker.Models;
using FinTracker.Utilities;
using Syncfusion.Maui.Toolkit.Charts;

namespace FinTracker.Pages.Controls;

public partial class CategoryExpenseBarChart : Border
{
    public event EventHandler<CategoryExpenseChartData>? CategorySelected;

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IEnumerable<CategoryExpenseChartData>),
        typeof(CategoryExpenseBarChart),
        propertyChanged: OnItemsSourceChanged);

    public static readonly BindableProperty IsIncomeChartProperty = BindableProperty.Create(
        nameof(IsIncomeChart), typeof(bool), typeof(CategoryExpenseBarChart), false,
        propertyChanged: (bindable, _, _) => ((CategoryExpenseBarChart)bindable).UpdateChartLabels());

    public bool IsIncomeChart
    {
        get => (bool)GetValue(IsIncomeChartProperty);
        set => SetValue(IsIncomeChartProperty, value);
    }

    public IEnumerable<CategoryExpenseChartData>? ItemsSource
    {
        get => (IEnumerable<CategoryExpenseChartData>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public IList<Brush> PaletteBrushes { get; private set; } = [];

    public CategoryExpenseBarChart() => InitializeComponent();

    private void UpdateChartLabels()
    {
        SemanticProperties.SetDescription(CategoryChart, IsIncomeChart ? "Income by category bar chart" : "Expenses by category bar chart");
        if (IsIncomeChart)
            ValueAxisTitle.Text = $"Income ({Application.Current?.Resources["DisplayCurrencySymbol"] ?? "€"})";
        else
            ValueAxisTitle.SetDynamicResource(ChartAxisTitle.TextProperty, "CurrencyExpenseAxisTitle");
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var chart = (CategoryExpenseBarChart)bindable;
        chart.PaletteBrushes = newValue is IEnumerable<CategoryExpenseChartData> items
            ? items.Select(item => item.Color).ToList()
            : [];
        chart.OnPropertyChanged(nameof(PaletteBrushes));
        if (chart.IsIncomeChart) chart.UpdateChartLabels();
    }

    private void OnYAxisLabelCreated(object? sender, ChartAxisLabelEventArgs e)
        => e.Label = EuroFormatter.FormatCompact(e.Position);

    private void OnSelectionChanged(object? sender, ChartSelectionChangedEventArgs e)
    {
        if (e.NewIndexes is null || !e.NewIndexes.Any()) return;
        var index = e.NewIndexes.First();
        var item = ItemsSource?.ElementAtOrDefault(index);
        if (item is not null)
            CategorySelected?.Invoke(this, item);
        if (sender is ChartSelectionBehavior selection)
            selection.ClearSelection();
    }
}
