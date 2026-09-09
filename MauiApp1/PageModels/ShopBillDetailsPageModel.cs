using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Utilities;
using System.Collections.ObjectModel;

namespace MauiApp1.PageModels;

public partial class ShopBillDetailsPageModel : ObservableObject, IQueryAttributable
{
    private readonly BillTransactionRepository _billRepository;
    private readonly ModalErrorHandler _errorHandler;
    [ObservableProperty] private int _shopId;
    [ObservableProperty] private int _year = DateTime.Now.Year;
    [ObservableProperty] private int? _month;
    [ObservableProperty] private string _shopName = string.Empty;
    [ObservableProperty] private decimal _shopTotal;
    [ObservableProperty] private ObservableCollection<BillTransaction> _bills = [];
    [ObservableProperty] private ObservableCollection<ExpenseMonthGroup> _monthGroups = [];
    [ObservableProperty] private bool _isBusy;

    public bool HasBills => Bills.Count > 0;
    public bool IsAnnual => Month is null;
    public bool IsMonthly => Month is not null;
    public bool ShowMonthlyEmptyState => IsMonthly && !HasBills;
    public string HeaderText => Month is null ? $"{ShopName} — Expenses {Year}" : $"{ShopName} Expenses";
    public string BillsPeriodText => Month is int month ? $"Bills for {new DateTime(Year, month, 1):MMMM yyyy}" : string.Empty;
    public string ShopTotalText => $"Total: {EuroFormatter.Format(ShopTotal)}";
    public string EmptyStateText => $"No bills found for {ShopName} in {Year}.";

    public ShopBillDetailsPageModel(BillTransactionRepository billRepository, ModalErrorHandler errorHandler)
    {
        _billRepository = billRepository;
        _errorHandler = errorHandler;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("shopId", out var shopValue) && int.TryParse(shopValue?.ToString(), out var shopId))
            ShopId = shopId;
        if (query.TryGetValue("year", out var yearValue) && int.TryParse(yearValue?.ToString(), out var year))
            Year = year;
        if (query.TryGetValue("month", out var monthValue) && int.TryParse(monthValue?.ToString(), out var month) && month is >= 1 and <= 12)
            Month = month;
        if (query.TryGetValue("shopName", out var nameValue) && nameValue is not null)
            ShopName = Uri.UnescapeDataString(nameValue.ToString() ?? string.Empty);
        OnPropertyChanged(nameof(IsAnnual));
        OnPropertyChanged(nameof(IsMonthly));
        OnPropertyChanged(nameof(ShowMonthlyEmptyState));
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(BillsPeriodText));
        OnPropertyChanged(nameof(ShopTotalText));
        OnPropertyChanged(nameof(EmptyStateText));
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (ShopId <= 0 || Year < 1) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            var bills = Month is int month ? await _billRepository.GetBillsByShopAndMonthAsync(ShopId, Year, month) : await _billRepository.GetBillsByShopAndYearAsync(ShopId, Year);
            var shop = Month is int currentMonth ? (await _billRepository.GetExpensesByShopForMonthAsync(Year, currentMonth)).FirstOrDefault(item => item.ShopId == ShopId) : (await _billRepository.GetExpensesByShopAsync(Year)).FirstOrDefault(item => item.ShopId == ShopId);
            ShopName = shop?.ShopName ?? bills.FirstOrDefault()?.ShopName ?? ShopName;
            ShopTotal = bills.Sum(bill => bill.BillValue);
            Bills = Month is int ? new ObservableCollection<BillTransaction>(bills) : [];
            MonthGroups = Month is null ? BuildMonthGroups(bills) : [];
            OnPropertyChanged(nameof(HasBills));
            OnPropertyChanged(nameof(ShowMonthlyEmptyState));
            OnPropertyChanged(nameof(HeaderText));
            OnPropertyChanged(nameof(BillsPeriodText));
            OnPropertyChanged(nameof(ShopTotalText));
            OnPropertyChanged(nameof(EmptyStateText));
        }
        catch (Exception ex)
        {
            _errorHandler.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private static void ToggleMonth(ExpenseMonthGroup group)
    {
        if (group is not null)
            group.IsExpanded = !group.IsExpanded;
    }

    private ObservableCollection<ExpenseMonthGroup> BuildMonthGroups(IEnumerable<BillTransaction> bills)
    {
        var byMonth = bills.GroupBy(bill => bill.BillDate.Month)
            .ToDictionary(group => group.Key, group => group.OrderByDescending(bill => bill.BillDate).ThenByDescending(bill => bill.Id));
        var groups = new ObservableCollection<ExpenseMonthGroup>();
        for (var month = 1; month <= 12; month++)
        {
            var item = new ExpenseMonthGroup { Year = Year, MonthNumber = month };
            if (byMonth.TryGetValue(month, out var monthBills))
                foreach (var bill in monthBills) item.Bills.Add(bill);
            groups.Add(item);
        }
        return groups;
    }

}
