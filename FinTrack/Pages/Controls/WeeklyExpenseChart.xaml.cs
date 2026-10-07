using FinTracker.Models;
using FinTracker.Utilities;
using Syncfusion.Maui.Toolkit.Charts;

namespace FinTracker.Pages.Controls;

public partial class WeeklyExpenseChart : Border
{
    public event EventHandler<WeeklyExpenseChartData>? DaySelected;
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<WeeklyExpenseChartData>), typeof(WeeklyExpenseChart));
    public IEnumerable<WeeklyExpenseChartData>? ItemsSource { get => (IEnumerable<WeeklyExpenseChartData>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }

    public WeeklyExpenseChart() => InitializeComponent();

    private void OnYAxisLabelCreated(object? sender, ChartAxisLabelEventArgs e) => e.Label = EuroFormatter.FormatCompact(e.Position);

    private void OnSelectionChanged(object? sender, ChartSelectionChangedEventArgs e)
    {
        var index = e.NewIndexes?.FirstOrDefault() ?? -1;
        var item = ItemsSource?.ElementAtOrDefault(index);
        if (item is not null)
            DaySelected?.Invoke(this, item);
    }
}
