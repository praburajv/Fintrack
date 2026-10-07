using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;
using FinTracker.Utilities;

namespace FinTracker.PageModels;

public partial class MainPageModel : ObservableObject
{
    private readonly BillTransactionRepository _billRepository;
    private readonly ModalErrorHandler _errorHandler;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    [ObservableProperty] private int _currentYear = DateTime.Now.Year;
    [ObservableProperty] private int _currentMonth = DateTime.Now.Month;
    [ObservableProperty] private decimal _annualTotal;
    [ObservableProperty] private List<CategoryExpenseChartData> _categoryExpenseData = [];
    [ObservableProperty] private List<CategoryExpenseChartData> _currentMonthCategoryExpenseData = [];
    [ObservableProperty] private List<MonthlyExpenseChartData> _monthlyExpenseData = [];
    [ObservableProperty] private List<WeeklyExpenseChartData> _weeklyExpenseData = [];
    [ObservableProperty] private decimal _currentMonthTotal;
    [ObservableProperty] private decimal _currentWeekTotal;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private bool _isOpeningAnalysis;
    [ObservableProperty] private bool _isOpeningCategoryBills;
    [ObservableProperty] private string _processingMessage = "Loading analytics dashboard...";
    public bool IsOverlayBusy => IsBusy || IsOpeningAnalysis || IsOpeningCategoryBills;
    public bool HasCategoryExpenses => CategoryExpenseData.Count > 0;
    public bool HasCurrentMonthCategoryExpenses => CurrentMonthCategoryExpenseData.Count > 0;
    public string CurrentMonthName => new DateTime(CurrentYear, CurrentMonth, 1).ToString("MMMM", System.Globalization.CultureInfo.CurrentCulture);
    public string CurrentMonthTitle => $"Expenses by Category — {CurrentMonthName}";
    public string WeeklyTitle => $"Expenses — Current Week ({WeeklyExpenseData.FirstOrDefault()?.Date:dd/MM} – {WeeklyExpenseData.LastOrDefault()?.Date:dd/MM})";
    public string CurrentMonthTotalText => EuroFormatter.Format(CurrentMonthTotal);
    public string CurrentWeekTotalText => EuroFormatter.Format(CurrentWeekTotal);
    public string AnnualTotalText => EuroFormatter.Format(AnnualTotal);

    public MainPageModel(BillTransactionRepository billRepository, ModalErrorHandler errorHandler) { _billRepository = billRepository; _errorHandler = errorHandler; }
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsOpeningAnalysisChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsOpeningCategoryBillsChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    [RelayCommand] private async Task Appearing() => await Refresh();
    [RelayCommand]
    private async Task Refresh()
    {
        if (!await _loadGate.WaitAsync(0)) return;
        try
        {
            ProcessingMessage = "Loading analytics dashboard...";
            IsRefreshing = true; IsBusy = true;
            await Task.Yield();
            var today = DateTime.Today; CurrentYear = today.Year; CurrentMonth = today.Month;
            var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            AnnualTotal = await _billRepository.GetAnnualTotalAsync(CurrentYear);
            CategoryExpenseData = await _billRepository.GetExpensesByCategoryAsync(CurrentYear);
            CurrentMonthCategoryExpenseData = await _billRepository.GetExpensesByCategoryForMonthAsync(CurrentYear, CurrentMonth);
            MonthlyExpenseData = (await _billRepository.GetExpensesByMonthAsync(CurrentYear)).Select(item => new MonthlyExpenseChartData { MonthNumber = item.MonthNumber, MonthName = item.MonthName, TotalValue = item.TotalValue, Color = ChartColorPalette.ForMonth(item.MonthNumber) }).ToList();
            WeeklyExpenseData = await _billRepository.GetExpensesByWeekAsync(weekStart);
            // Use the exact collections rendered by the two charts so their
            // displayed totals always reconcile with the chart segments/bars.
            CurrentMonthTotal = CurrentMonthCategoryExpenseData.Sum(item => item.TotalValue);
            CurrentWeekTotal = WeeklyExpenseData.Sum(item => item.TotalValue);
            OnPropertyChanged(nameof(AnnualTotalText)); OnPropertyChanged(nameof(HasCategoryExpenses)); OnPropertyChanged(nameof(HasCurrentMonthCategoryExpenses)); OnPropertyChanged(nameof(CurrentMonthName)); OnPropertyChanged(nameof(CurrentMonthTitle)); OnPropertyChanged(nameof(CurrentMonthTotalText)); OnPropertyChanged(nameof(WeeklyTitle)); OnPropertyChanged(nameof(CurrentWeekTotalText));
        }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
        finally { IsBusy = false; IsRefreshing = false; _loadGate.Release(); }
    }

}
