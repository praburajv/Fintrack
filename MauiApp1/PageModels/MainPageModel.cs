using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Utilities;

namespace MauiApp1.PageModels;

public partial class MainPageModel : ObservableObject
{
    private readonly BillTransactionRepository _billRepository;
    private readonly ModalErrorHandler _errorHandler;
    [ObservableProperty] private int _currentYear = DateTime.Now.Year;
    [ObservableProperty] private int _currentMonth = DateTime.Now.Month;
    [ObservableProperty] private decimal _annualTotal;
    [ObservableProperty] private List<ShopExpenseChartData> _shopExpenseData = [];
    [ObservableProperty] private List<ShopExpenseChartData> _currentMonthShopExpenseData = [];
    [ObservableProperty] private List<MonthlyExpenseChartData> _monthlyExpenseData = [];
    [ObservableProperty] private List<WeeklyExpenseChartData> _weeklyExpenseData = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    public bool HasShopExpenses => ShopExpenseData.Count > 0;
    public bool HasCurrentMonthShopExpenses => CurrentMonthShopExpenseData.Count > 0;
    public string CurrentMonthName => new DateTime(CurrentYear, CurrentMonth, 1).ToString("MMMM", System.Globalization.CultureInfo.CurrentCulture);
    public string CurrentMonthTitle => $"Expenses by Shop — {CurrentMonthName}";
    public string WeeklyTitle => $"Expenses — Current Week ({WeeklyExpenseData.FirstOrDefault()?.Date:dd/MM} – {WeeklyExpenseData.LastOrDefault()?.Date:dd/MM})";
    public string AnnualTotalText => EuroFormatter.Format(AnnualTotal);

    public MainPageModel(BillTransactionRepository billRepository, ModalErrorHandler errorHandler) { _billRepository = billRepository; _errorHandler = errorHandler; }
    [RelayCommand] private async Task Appearing() => await Refresh();
    [RelayCommand]
    private async Task Refresh()
    {
        try
        {
            IsRefreshing = true; IsBusy = true;
            var today = DateTime.Today; CurrentYear = today.Year; CurrentMonth = today.Month;
            var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            AnnualTotal = await _billRepository.GetAnnualTotalAsync(CurrentYear);
            ShopExpenseData = MapShops(await _billRepository.GetExpensesByShopAsync(CurrentYear));
            CurrentMonthShopExpenseData = MapShops(await _billRepository.GetExpensesByShopForMonthAsync(CurrentYear, CurrentMonth));
            MonthlyExpenseData = (await _billRepository.GetExpensesByMonthAsync(CurrentYear)).Select(item => new MonthlyExpenseChartData { MonthNumber = item.MonthNumber, MonthName = item.MonthName, TotalValue = item.TotalValue, Color = ChartColorPalette.ForMonth(item.MonthNumber) }).ToList();
            WeeklyExpenseData = await _billRepository.GetExpensesByWeekAsync(weekStart);
            OnPropertyChanged(nameof(AnnualTotalText)); OnPropertyChanged(nameof(HasShopExpenses)); OnPropertyChanged(nameof(HasCurrentMonthShopExpenses)); OnPropertyChanged(nameof(CurrentMonthName)); OnPropertyChanged(nameof(CurrentMonthTitle)); OnPropertyChanged(nameof(WeeklyTitle));
        }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
        finally { IsBusy = false; IsRefreshing = false; }
    }

    private static List<ShopExpenseChartData> MapShops(IEnumerable<ShopExpenseChartData> items) => items.Select(item => new ShopExpenseChartData { ShopId = item.ShopId, ShopName = item.ShopName, ShopDisplayName = ShopNameAbbreviator.Abbreviate(item.ShopName), TotalValue = item.TotalValue, Color = ChartColorPalette.ForShop(item.ShopName) }).ToList();

}
