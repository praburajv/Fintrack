using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;

namespace MauiApp1.PageModels;

public partial class MainPageModel : ObservableObject
{
    private readonly BillTransactionRepository _billRepository;
    private readonly ModalErrorHandler _errorHandler;
    [ObservableProperty] private int _currentYear = DateTime.Now.Year;
    [ObservableProperty] private decimal _annualTotal;
    [ObservableProperty] private List<ShopExpenseChartData> _shopExpenseData = [];
    [ObservableProperty] private List<MonthlyExpenseChartData> _monthlyExpenseData = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isRefreshing;
    public bool HasShopExpenses => ShopExpenseData.Count > 0;
    public string AnnualTotalText => AnnualTotal.ToString("C2");

    public MainPageModel(BillTransactionRepository billRepository, ModalErrorHandler errorHandler) { _billRepository = billRepository; _errorHandler = errorHandler; }
    [RelayCommand] private async Task Appearing() => await Refresh();
    [RelayCommand]
    private async Task Refresh()
    {
        try { IsRefreshing = true; IsBusy = true; CurrentYear = DateTime.Now.Year; AnnualTotal = await _billRepository.GetAnnualTotalAsync(CurrentYear); ShopExpenseData = await _billRepository.GetExpensesByShopAsync(CurrentYear); MonthlyExpenseData = await _billRepository.GetExpensesByMonthAsync(CurrentYear); OnPropertyChanged(nameof(AnnualTotalText)); OnPropertyChanged(nameof(HasShopExpenses)); }
        catch (Exception ex) { _errorHandler.HandleError(ex); }
        finally { IsBusy = false; IsRefreshing = false; }
    }
}
