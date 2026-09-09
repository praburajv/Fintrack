using MauiApp1.Models;
using Syncfusion.Maui.Toolkit.Charts;

namespace MauiApp1.Pages.Controls;

public partial class PaymentModeExpenseChart : Border
{
    public event EventHandler<PaymentModeExpenseChartData>? PaymentModeSelected;
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<PaymentModeExpenseChartData>), typeof(PaymentModeExpenseChart), propertyChanged: OnItemsSourceChanged);
    public static readonly BindableProperty StartDateProperty = BindableProperty.Create(nameof(StartDate), typeof(DateTime), typeof(PaymentModeExpenseChart), default(DateTime));
    public static readonly BindableProperty EndDateProperty = BindableProperty.Create(nameof(EndDate), typeof(DateTime), typeof(PaymentModeExpenseChart), default(DateTime));
    public IEnumerable<PaymentModeExpenseChartData>? ItemsSource { get => (IEnumerable<PaymentModeExpenseChartData>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public IList<Brush> PaletteBrushes { get; private set; } = [];
    public DateTime StartDate { get => (DateTime)GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }
    public DateTime EndDate { get => (DateTime)GetValue(EndDateProperty); set => SetValue(EndDateProperty, value); }
    public PaymentModeExpenseChart() => InitializeComponent();
    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue) { var chart = (PaymentModeExpenseChart)bindable; chart.PaletteBrushes = newValue is IEnumerable<PaymentModeExpenseChartData> items ? items.Select(x => x.Color).ToList() : []; chart.OnPropertyChanged(nameof(PaletteBrushes)); }
    private void OnSelectionChanged(object? sender, ChartSelectionChangedEventArgs e) { var item = ItemsSource?.ElementAtOrDefault(e.NewIndexes?.FirstOrDefault() ?? -1); if (item is not null) PaymentModeSelected?.Invoke(this, item); }
}
