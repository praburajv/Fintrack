using MauiApp1.Models;
using System.Collections.ObjectModel;

namespace MauiApp1.Pages.Controls;

public partial class ShopExpenseChart : Border
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<ShopExpenseChartData>), typeof(ShopExpenseChart));
    public IEnumerable<ShopExpenseChartData>? ItemsSource { get => (IEnumerable<ShopExpenseChartData>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public ShopExpenseChart() { InitializeComponent(); }
}
