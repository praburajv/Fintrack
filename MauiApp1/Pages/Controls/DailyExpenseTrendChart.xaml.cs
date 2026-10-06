using MauiApp1.Models;
using MauiApp1.Utilities;
using Syncfusion.Maui.Toolkit.Charts;

namespace MauiApp1.Pages.Controls;

public partial class DailyExpenseTrendChart : Border
{
    public event EventHandler<DailyExpenseCalendarItem>? DaySelected;

    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IEnumerable<DailyExpenseCalendarItem>),
        typeof(DailyExpenseTrendChart));

    public IEnumerable<DailyExpenseCalendarItem>? ItemsSource
    {
        get => (IEnumerable<DailyExpenseCalendarItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DailyExpenseTrendChart() => InitializeComponent();

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
