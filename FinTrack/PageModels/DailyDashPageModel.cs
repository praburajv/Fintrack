using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Utilities;

namespace FinTracker.PageModels;

public partial class DailyDashPageModel(BillTransactionRepository bills, ModalErrorHandler errors) : ObservableObject
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private DateTime _reportDate = DateTime.Today;
    [ObservableProperty] private List<PaymentModeExpenseChartData> _dailyData = [];
    [ObservableProperty] private List<PaymentModeExpenseChartData> _weeklyData = [];
    [ObservableProperty] private List<PaymentModeExpenseChartData> _monthlyData = [];
    [ObservableProperty] private List<ShopExpenseChartData> _dailyShopVisits = [];
    [ObservableProperty] private List<ShopExpenseChartData> _weeklyShopVisits = [];
    [ObservableProperty] private List<ShopExpenseChartData> _monthlyShopVisits = [];
    [ObservableProperty] private bool _isShopVisitBusy;
    [ObservableProperty] private decimal _dailyTotal, _weeklyTotal, _monthlyTotal;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isOpeningBudget;
    [ObservableProperty] private bool _isOpeningReport;
    [ObservableProperty] private string _processingMessage = "Loading DailyDash...";
    public bool IsOverlayBusy => IsBusy || IsOpeningBudget || IsOpeningReport || IsShopVisitBusy;
    partial void OnIsShopVisitBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    public bool HasDailyShopVisits => DailyShopVisits.Count > 0;
    public bool HasWeeklyShopVisits => WeeklyShopVisits.Count > 0;
    public bool HasMonthlyShopVisits => MonthlyShopVisits.Count > 0;
    public DateTime Today => _reportDate;
    public DateTime SelectedDate => Today;
    public DateTime WeekStart => Today.AddDays(-(((int)Today.DayOfWeek + 6) % 7));
    public DateTime DayEnd => Today.AddDays(1);
    public DateTime WeekEnd => WeekStart.AddDays(7);
    public DateTime MonthStart => new(Today.Year, Today.Month, 1);
    public DateTime MonthEnd => MonthStart.AddMonths(1);
    public string DailyDateText => Today.ToString("dd/MM/yyyy");
    public string WeeklyDateText => $"{WeekStart:dd/MM/yyyy} – {WeekStart.AddDays(6):dd/MM/yyyy}";
    public string MonthlyDateText => $"{MonthStart:dd/MM/yyyy} – {MonthStart.AddMonths(1).AddDays(-1):dd/MM/yyyy}";
    public string DailyTotalText => EuroFormatter.Format(DailyTotal);
    public string WeeklyTotalText => EuroFormatter.Format(WeeklyTotal);
    public string MonthlyTotalText => EuroFormatter.Format(MonthlyTotal);
    public bool HasDailyData => DailyData.Count > 0;
    public bool HasWeeklyData => WeeklyData.Count > 0;
    public bool HasMonthlyData => MonthlyData.Count > 0;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsOpeningBudgetChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsOpeningReportChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));

    [RelayCommand]
    private async Task Appearing()
    {
        if (!await _loadGate.WaitAsync(0)) return;
        try
        {
            ProcessingMessage = "Loading DailyDash...";
            IsBusy = true;
            _reportDate = DateTime.Today;
            foreach (var property in new[] { nameof(Today), nameof(SelectedDate), nameof(DayEnd), nameof(WeekStart), nameof(WeekEnd), nameof(MonthStart), nameof(MonthEnd) })
                OnPropertyChanged(property);
            await Task.Yield();
            var dayEnd = Today.AddDays(1); var weekEnd = WeekStart.AddDays(7); var monthEnd = MonthStart.AddMonths(1);
            // Use the repository's existing connection architecture sequentially;
            // each query opens and disposes its own SQLite connection, while Init
            // is shared by the singleton repository.
            DailyData = await bills.GetPaymentModeExpenseTotalsAsync(Today, dayEnd);
            WeeklyData = await bills.GetPaymentModeExpenseTotalsAsync(WeekStart, weekEnd);
            MonthlyData = await bills.GetPaymentModeExpenseTotalsAsync(MonthStart, monthEnd);
            DailyShopVisits = await bills.GetExpensesByShopAsync(Today, dayEnd);
            WeeklyShopVisits = await bills.GetExpensesByShopAsync(WeekStart, weekEnd);
            MonthlyShopVisits = await bills.GetExpensesByShopAsync(MonthStart, monthEnd);
            OnPropertyChanged(nameof(HasDailyShopVisits)); OnPropertyChanged(nameof(HasWeeklyShopVisits)); OnPropertyChanged(nameof(HasMonthlyShopVisits));
            DailyTotal = DailyData.Sum(x => x.TotalValue); WeeklyTotal = WeeklyData.Sum(x => x.TotalValue); MonthlyTotal = MonthlyData.Sum(x => x.TotalValue);
            OnPropertyChanged(nameof(DailyDateText)); OnPropertyChanged(nameof(WeeklyDateText)); OnPropertyChanged(nameof(MonthlyDateText)); OnPropertyChanged(nameof(DailyTotalText)); OnPropertyChanged(nameof(WeeklyTotalText)); OnPropertyChanged(nameof(MonthlyTotalText)); OnPropertyChanged(nameof(HasDailyData)); OnPropertyChanged(nameof(HasWeeklyData)); OnPropertyChanged(nameof(HasMonthlyData));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; _loadGate.Release(); }
    }
}
