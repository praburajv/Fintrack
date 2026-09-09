using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Data;
using MauiApp1.Models;
using MauiApp1.Utilities;

namespace MauiApp1.PageModels;

public partial class DailyDashPageModel(BillTransactionRepository bills, ModalErrorHandler errors) : ObservableObject
{
    [ObservableProperty] private List<PaymentModeExpenseChartData> _dailyData = [];
    [ObservableProperty] private List<PaymentModeExpenseChartData> _weeklyData = [];
    [ObservableProperty] private List<PaymentModeExpenseChartData> _monthlyData = [];
    [ObservableProperty] private decimal _dailyTotal, _weeklyTotal, _monthlyTotal;
    [ObservableProperty] private bool _isBusy;
    public DateTime Today => DateTime.Today;
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

    [RelayCommand]
    private async Task Appearing()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var dayEnd = Today.AddDays(1); var weekEnd = WeekStart.AddDays(7); var monthEnd = MonthStart.AddMonths(1);
            // Use the repository's existing connection architecture sequentially;
            // each query opens and disposes its own SQLite connection, while Init
            // is shared by the singleton repository.
            DailyData = await bills.GetPaymentModeExpenseTotalsAsync(Today, dayEnd);
            WeeklyData = await bills.GetPaymentModeExpenseTotalsAsync(WeekStart, weekEnd);
            MonthlyData = await bills.GetPaymentModeExpenseTotalsAsync(MonthStart, monthEnd);
            DailyTotal = DailyData.Sum(x => x.TotalValue); WeeklyTotal = WeeklyData.Sum(x => x.TotalValue); MonthlyTotal = MonthlyData.Sum(x => x.TotalValue);
            OnPropertyChanged(nameof(DailyDateText)); OnPropertyChanged(nameof(WeeklyDateText)); OnPropertyChanged(nameof(MonthlyDateText)); OnPropertyChanged(nameof(DailyTotalText)); OnPropertyChanged(nameof(WeeklyTotalText)); OnPropertyChanged(nameof(MonthlyTotalText)); OnPropertyChanged(nameof(HasDailyData)); OnPropertyChanged(nameof(HasWeeklyData)); OnPropertyChanged(nameof(HasMonthlyData));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }
}
