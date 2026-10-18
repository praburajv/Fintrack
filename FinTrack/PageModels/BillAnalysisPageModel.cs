using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;
using FinTracker.Services;
using FinTracker.Utilities;

namespace FinTracker.PageModels;

public partial class BillAnalysisPageModel(ShopBillAnalysisService analysis, FinancialAccountContext accounts, ModalErrorHandler errors) : ObservableObject, IQueryAttributable
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private bool _loaded;
    private bool _reloadRequested;

    [ObservableProperty] private ObservableCollection<BillTransaction> _expenseBills = [];
    [ObservableProperty] private ObservableCollection<BillTransaction> _incomeBills = [];
    [ObservableProperty] private string? _referenceFilter;
    public bool HasReferenceFilter => !string.IsNullOrEmpty(ReferenceFilter);
    public string ReferenceFilterText => $"Reference No.: {ReferenceFilter} (lists filtered; charts show the full year)";
    public bool HasExpenseBills => ExpenseBills.Count > 0;
    public bool HasIncomeBills => IncomeBills.Count > 0;
    partial void OnReferenceFilterChanged(string? value) { OnPropertyChanged(nameof(HasReferenceFilter)); OnPropertyChanged(nameof(ReferenceFilterText)); }

    [RelayCommand]
    private async Task ClearReferenceFilter() { ReferenceFilter = null; await LoadAsync(); }

    [ObservableProperty] private int _shopId;
    [ObservableProperty] private string _shopName = string.Empty;
    [ObservableProperty] private int _selectedYear = DateTime.Today.Year;
    [ObservableProperty] private ObservableCollection<int> _yearOptions = [];
    [ObservableProperty] private ObservableCollection<MonthlyVisitData> _monthlyVisits = [];
    [ObservableProperty] private ObservableCollection<MonthlyVisitData> _incomeMonthlyVisits = [];
    [ObservableProperty] private int _incomeCount;
    [ObservableProperty] private decimal _totalIncome;
    [ObservableProperty] private decimal _averageIncome;
    [ObservableProperty] private int _visitCount;
    [ObservableProperty] private decimal _totalSpent;
    [ObservableProperty] private decimal _averageSpend;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isOpeningMonth;
    [ObservableProperty] private string _processingMessage = "Loading bill analysis...";

    public bool IsOverlayBusy => IsLoading || IsOpeningMonth;
    public bool HasVisits => VisitCount > 0;
    public string VisitsText => VisitCount.ToString(CultureInfo.InvariantCulture);
    public string TotalSpentText => EuroFormatter.Format(TotalSpent);
    public string AverageSpendText => EuroFormatter.Format(AverageSpend);
    public string EmptyText => $"No bills found for {ShopName} in {SelectedYear}.";
    public bool HasIncome => IncomeCount > 0;
    public string IncomeCountText => IncomeCount.ToString(CultureInfo.InvariantCulture);
    public string TotalIncomeText => EuroFormatter.Format(TotalIncome);
    public string AverageIncomeText => EuroFormatter.Format(AverageIncome);
    public string IncomeEmptyText => $"No income found for {ShopName} in {SelectedYear}.";

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsOpeningMonthChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnSelectedYearChanged(int value)
    {
        OnPropertyChanged(nameof(EmptyText));
        if (_loaded) LoadAsync().FireAndForgetSafeAsync(errors);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _loaded = false;
        ReferenceFilter = query.TryGetValue("reference", out var reference) ? TransactionInput.NormalizeReference(reference?.ToString()) : null;
        SelectedYear = query.TryGetValue("year", out var selectedYear) && int.TryParse(selectedYear?.ToString(), out var year) && year is >= 1 and <= 9998 ? year : DateTime.Today.Year;
        if (query.TryGetValue("shopId", out var value) && int.TryParse(value?.ToString(), out var shopId) && shopId > 0)
            ShopId = shopId;
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (ShopId <= 0) return;
        _loaded = true;
        await LoadAsync(loadYears: true);
    }

    private async Task LoadAsync(bool loadYears = false)
    {
        if (!await _loadGate.WaitAsync(0))
        {
            _reloadRequested = true;
            return;
        }

        try
        {
            IsLoading = true;
            await Task.Yield();
            do
            {
                _reloadRequested = false;
                var requestedYear = SelectedYear;
                var requestedShop = ShopId;
                var requestedAccount = accounts.Current?.Id;
                var requestedReference = ReferenceFilter;
                ExpenseBills = []; IncomeBills = [];
                OnPropertyChanged(nameof(HasExpenseBills)); OnPropertyChanged(nameof(HasIncomeBills));
                ProcessingMessage = $"Loading bill analysis for {requestedYear}...";
                VisitCount = 0;
                TotalSpent = 0;
                AverageSpend = 0;
                MonthlyVisits = [];
                IncomeMonthlyVisits = [];
                IncomeCount = 0;
                TotalIncome = 0;
                AverageIncome = 0;
                NotifySummary();

                if (loadYears || YearOptions.Count == 0)
                {
                    var years = await analysis.GetAvailableYearsAsync(ShopId);
                    YearOptions = new ObservableCollection<int>(years);
                    loadYears = false;
                }

                var expenseTask = analysis.GetYearAnalysisAsync(requestedShop, requestedYear);
                var incomeTask = analysis.GetIncomeYearAnalysisAsync(requestedShop, requestedYear);
                var billsTask = analysis.GetBillsAsync(requestedShop, requestedYear, requestedReference);
                await Task.WhenAll(expenseTask, incomeTask, billsTask);
                var result = await expenseTask;
                var incomeResult = await incomeTask;
                if (requestedAccount != accounts.Current?.Id) return;
                if (requestedYear != SelectedYear || requestedShop != ShopId || requestedReference != ReferenceFilter) { _reloadRequested = true; continue; }
                var detail = await billsTask;
                ExpenseBills = new ObservableCollection<BillTransaction>(detail.Expenses);
                IncomeBills = new ObservableCollection<BillTransaction>(detail.Income);
                OnPropertyChanged(nameof(HasExpenseBills)); OnPropertyChanged(nameof(HasIncomeBills));
                ShopName = result.ShopName;
                VisitCount = result.VisitCount;
                TotalSpent = result.TotalSpent;
                AverageSpend = result.AverageSpendPerVisit;
                MonthlyVisits = new ObservableCollection<MonthlyVisitData>(result.MonthlyVisits);
                IncomeCount = incomeResult.VisitCount;
                TotalIncome = incomeResult.TotalSpent;
                AverageIncome = incomeResult.AverageSpendPerVisit;
                IncomeMonthlyVisits = new ObservableCollection<MonthlyVisitData>(incomeResult.MonthlyVisits);
                NotifySummary();
            }
            while (_reloadRequested);
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsLoading = false; _loadGate.Release(); }
    }

    [RelayCommand]
    private async Task OpenMonth(MonthlyVisitData month)
    {
        if (month is null || month.VisitCount == 0 || IsOverlayBusy || !await _navigationGate.WaitAsync(0)) return;
        try
        {
            IsOpeningMonth = true;
            ProcessingMessage = $"Loading {month.MonthName} {SelectedYear} transaction dates...";
            await Task.Yield();
            await Shell.Current.GoToAsync($"shoptransactiondates?shopId={ShopId}&year={SelectedYear}&month={month.MonthNumber}");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsOpeningMonth = false; _navigationGate.Release(); }
    }

    private void NotifySummary()
    {
        OnPropertyChanged(nameof(HasVisits));
        OnPropertyChanged(nameof(VisitsText));
        OnPropertyChanged(nameof(TotalSpentText));
        OnPropertyChanged(nameof(AverageSpendText));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(HasIncome));
        OnPropertyChanged(nameof(IncomeCountText));
        OnPropertyChanged(nameof(TotalIncomeText));
        OnPropertyChanged(nameof(AverageIncomeText));
        OnPropertyChanged(nameof(IncomeEmptyText));
    }

    public async Task OpenIncomeMonthAsync(MonthlyVisitData month)
    {
        if (month is null || month.VisitCount == 0 || IsOverlayBusy || !await _navigationGate.WaitAsync(0)) return;
        try
        {
            IsOpeningMonth = true;
            ProcessingMessage = $"Loading income dates for {month.MonthName} {SelectedYear}...";
            await Task.Yield();
            await Shell.Current.GoToAsync($"shoptransactiondates?shopId={ShopId}&year={SelectedYear}&month={month.MonthNumber}&type=Income");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsOpeningMonth = false; _navigationGate.Release(); }
    }
}
