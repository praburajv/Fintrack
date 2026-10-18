using FinTracker.Models;
using FinTracker.Utilities;
using Syncfusion.Maui.Toolkit.Charts;

namespace FinTracker.Pages.Controls;

public partial class ShopVisitsChart : Border
{
    public event EventHandler<ShopExpenseChartData>? ShopSelected;
    public event EventHandler? SortChanged;
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable<ShopExpenseChartData>), typeof(ShopVisitsChart), propertyChanged: OnItemsSourceChanged);
    public IEnumerable<ShopExpenseChartData>? ItemsSource { get => (IEnumerable<ShopExpenseChartData>?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public static readonly BindableProperty ShowSortControlProperty = BindableProperty.Create(nameof(ShowSortControl), typeof(bool), typeof(ShopVisitsChart), false, propertyChanged: OnItemsSourceChanged);
    public bool ShowSortControl { get => (bool)GetValue(ShowSortControlProperty); set => SetValue(ShowSortControlProperty, value); }
    public static readonly BindableProperty SortAscendingProperty = BindableProperty.Create(nameof(SortAscending), typeof(bool), typeof(ShopVisitsChart), false, BindingMode.TwoWay, propertyChanged: OnItemsSourceChanged);
    public bool SortAscending { get => (bool)GetValue(SortAscendingProperty); set => SetValue(SortAscendingProperty, value); }
    public static readonly BindableProperty IsProcessingProperty = BindableProperty.Create(nameof(IsProcessing), typeof(bool), typeof(ShopVisitsChart), false, BindingMode.TwoWay);
    public bool IsProcessing { get => (bool)GetValue(IsProcessingProperty); set => SetValue(IsProcessingProperty, value); }
    public static readonly BindableProperty ProcessingMessageProperty = BindableProperty.Create(nameof(ProcessingMessage), typeof(string), typeof(ShopVisitsChart), "Processing shop visits...", BindingMode.TwoWay);
    public string ProcessingMessage { get => (string)GetValue(ProcessingMessageProperty); set => SetValue(ProcessingMessageProperty, value); }
    public static readonly BindableProperty StartDateProperty = BindableProperty.Create(nameof(StartDate), typeof(DateTime), typeof(ShopVisitsChart), default(DateTime));
    public DateTime StartDate { get => (DateTime)GetValue(StartDateProperty); set => SetValue(StartDateProperty, value); }
    public static readonly BindableProperty EndExclusiveProperty = BindableProperty.Create(nameof(EndExclusive), typeof(DateTime), typeof(ShopVisitsChart), default(DateTime));
    public DateTime EndExclusive { get => (DateTime)GetValue(EndExclusiveProperty); set => SetValue(EndExclusiveProperty, value); }
    public static readonly BindableProperty OpenBillsOnSelectionProperty = BindableProperty.Create(nameof(OpenBillsOnSelection), typeof(bool), typeof(ShopVisitsChart), false);
    public bool OpenBillsOnSelection { get => (bool)GetValue(OpenBillsOnSelectionProperty); set => SetValue(OpenBillsOnSelectionProperty, value); }
    public IReadOnlyList<ShopExpenseChartData> DisplayedItems { get; private set; } = [];
    public double ChartHeightRequest => Math.Max(320, DisplayedItems.Count * 48 + 96);
    public string SortText => SortAscending ? "Visits ↑ (ASC)" : "Visits ↓ (DESC)";
    public IList<Brush> PaletteBrushes { get; private set; } = [];
    public ShopVisitsChart() => InitializeComponent();
    private static void OnItemsSourceChanged(BindableObject bindable, object? oldValue, object? newValue)
    {
        var chart = (ShopVisitsChart)bindable;
        chart.DisplayedItems = chart.ShowSortControl
            ? ShopVisitOrdering.Order(chart.ItemsSource ?? [], chart.SortAscending)
            : (chart.ItemsSource?.ToArray() ?? []);
        chart.PaletteBrushes = chart.DisplayedItems.Select(item => item.Color).ToList();
        chart.OnPropertyChanged(nameof(DisplayedItems));
        chart.OnPropertyChanged(nameof(ChartHeightRequest));
        chart.OnPropertyChanged(nameof(PaletteBrushes));
        chart.OnPropertyChanged(nameof(SortText));
    }
    private async void OnSortClicked(object? sender, EventArgs e)
    {
        if (IsProcessing) return;
        IsProcessing = true;
        ProcessingMessage = "Sorting shop visits...";
        try
        {
            await Task.Yield(); // Let the page's screen lock and spinner render first.
            SortAscending = !SortAscending;
            SortChanged?.Invoke(this, EventArgs.Empty);
        }
        finally { IsProcessing = false; }
    }
    private void OnYAxisLabelCreated(object? sender, ChartAxisLabelEventArgs e) => e.Label = e.Position.ToString("0", System.Globalization.CultureInfo.InvariantCulture);
    private async void OnSelectionChanged(object? sender, ChartSelectionChangedEventArgs e)
    {
        if (e.NewIndexes is null || e.NewIndexes.Count == 0 || IsProcessing) return;
        var item = DisplayedItems.ElementAtOrDefault(e.NewIndexes[0]);
        if (sender is ChartSelectionBehavior selection) selection.ClearSelection();
        if (item is null) return;
        if (!OpenBillsOnSelection) { ShopSelected?.Invoke(this, item); return; }
        IsProcessing = true;
        ProcessingMessage = $"Opening {item.ShopName} bills...";
        try
        {
            await Task.Yield();
            await Shell.Current.GoToAsync(ShopBillNavigation.Route(item.ShopId, StartDate, EndExclusive));
        }
        catch (Exception)
        {
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Shop bills", "The shop bill list could not be opened. Please try again.", "OK");
        }
        finally { IsProcessing = false; }
    }
}
