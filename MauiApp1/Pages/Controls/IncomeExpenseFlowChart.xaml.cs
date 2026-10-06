using MauiApp1.Models;

namespace MauiApp1.Pages.Controls;

public partial class IncomeExpenseFlowChart : Border
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<FinancialFlowMonth>), typeof(IncomeExpenseFlowChart));
    public IEnumerable<FinancialFlowMonth>? ItemsSource { get => (IEnumerable<FinancialFlowMonth>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public IncomeExpenseFlowChart() => InitializeComponent();
}
