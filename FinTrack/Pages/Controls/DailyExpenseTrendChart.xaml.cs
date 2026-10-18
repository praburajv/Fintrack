using FinTracker.Models;
using FinTracker.Utilities;
using Syncfusion.Maui.Toolkit.Charts;

namespace FinTracker.Pages.Controls;

public partial class DailyExpenseTrendChart : Border
{
    public event EventHandler<DailyExpenseCalendarItem>? DaySelected;

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IEnumerable<DailyExpenseCalendarItem>),
        typeof(DailyExpenseTrendChart),
        propertyChanged: (bindable, _, _) =>
        {
            var chart = (DailyExpenseTrendChart)bindable;
            if (chart.IsIncomeChart) chart.UpdateChartLabels();
        });

    public static readonly BindableProperty IsIncomeChartProperty = BindableProperty.Create(
        nameof(IsIncomeChart), typeof(bool), typeof(DailyExpenseTrendChart), false,
        propertyChanged: (bindable, _, _) => ((DailyExpenseTrendChart)bindable).UpdateChartLabels());

    public bool IsIncomeChart
    {
        get => (bool)GetValue(IsIncomeChartProperty);
        set => SetValue(IsIncomeChartProperty, value);
    }

    public IEnumerable<DailyExpenseCalendarItem>? ItemsSource
    {
        get => (IEnumerable<DailyExpenseCalendarItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DailyExpenseTrendChart() => InitializeComponent();

    private void UpdateChartLabels()
    {
        SemanticProperties.SetDescription(TrendChart, IsIncomeChart ? "Daily income trend for the selected month" : "Daily expenses trend for the selected month");
        if (IsIncomeChart)
            ValueAxisTitle.Text = $"Income ({Application.Current?.Resources["DisplayCurrencySymbol"] ?? "€"})";
        else
            ValueAxisTitle.SetDynamicResource(ChartAxisTitle.TextProperty, "CurrencyExpenseAxisTitle");
    }

    private void OnYAxisLabelCreated(object? sender, ChartAxisLabelEventArgs e)
        => e.Label = EuroFormatter.FormatCompact(e.Position);

    private void OnSelectionChanged(object? sender, ChartSelectionChangedEventArgs e)
    {
        if (e.NewIndexes is null || !e.NewIndexes.Any()) return;
        var index = e.NewIndexes.First();
        var item = ItemsSource?.ElementAtOrDefault(index);
        if (item is not null)
            DaySelected?.Invoke(this, item);
        if (sender is ChartSelectionBehavior selection)
            selection.ClearSelection();
    }
}
