using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;
using FinTracker.Services;
using FinTracker.Utilities;

namespace FinTracker.PageModels;

public partial class BillAnalysisPageModel(ShopBillAnalysisService analysis, ModalErrorHandler errors) : ObservableObject, IQueryAttributable
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private bool _loaded;
    private bool _reloadRequested;

    [ObservableProperty] private int _shopId;
    [ObservableProperty] private string _shopName = string.Empty;
    [ObservableProperty] private int _selectedYear = DateTime.Today.Year;
    [ObservableProperty] private ObservableCollection<int> _yearOptions = [];
    [ObservableProperty] private ObservableCollection<MonthlyVisitData> _monthlyVisits = [];
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

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsOpeningMonthChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnSelectedYearChanged(int value)
    {
        OnPropertyChanged(nameof(EmptyText));
        if (_loaded) LoadAsync().FireAndForgetSafeAsync(errors);
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("shopId", out var value) && int.TryParse(value?.ToString(), out var shopId) && shopId > 0)
            ShopId = shopId;
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (_loaded || ShopId <= 0) return;
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
                ProcessingMessage = $"Loading bill analysis for {requestedYear}...";
                VisitCount = 0;
                TotalSpent = 0;
                AverageSpend = 0;
                MonthlyVisits = [];
                NotifySummary();

                if (loadYears || YearOptions.Count == 0)
                {
                    var years = await analysis.GetAvailableYearsAsync(ShopId);
                    YearOptions = new ObservableCollection<int>(years);
                    loadYears = false;
                }

                var result = await analysis.GetYearAnalysisAsync(ShopId, requestedYear);
                if (requestedYear != SelectedYear) continue;
                ShopName = result.ShopName;
                VisitCount = result.VisitCount;
                TotalSpent = result.TotalSpent;
                AverageSpend = result.AverageSpendPerVisit;
                MonthlyVisits = new ObservableCollection<MonthlyVisitData>(result.MonthlyVisits);
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
    }
}
