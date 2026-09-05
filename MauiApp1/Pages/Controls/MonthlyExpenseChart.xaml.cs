using MauiApp1.Models;

namespace MauiApp1.Pages.Controls;

public partial class MonthlyExpenseChart : Border
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<MonthlyExpenseChartData>), typeof(MonthlyExpenseChart));
    public IEnumerable<MonthlyExpenseChartData>? ItemsSource { get => (IEnumerable<MonthlyExpenseChartData>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public MonthlyExpenseChart() { InitializeComponent(); }
}
