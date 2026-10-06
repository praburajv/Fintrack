using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Data;
using MauiApp1.Models;
using MauiApp1.Utilities;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MauiApp1.PageModels;

public partial class ExpenseAnalysisPageModel : ObservableObject, IQueryAttributable
{
    private readonly ExpenseAnalysisService _analysis;
    private readonly ModalErrorHandler _errors;
    private readonly IExpenseStatementBuilder _statements;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _statementGate = new(1, 1);
    private bool _loaded;
    private bool _reloadRequested;
    [ObservableProperty] private ExpensePeriodType _periodType = ExpensePeriodType.Monthly;
    [ObservableProperty] private DateTime _referenceDate = DateTime.Today;
    [ObservableProperty] private DateTime _startDate;
    [ObservableProperty] private DateTime _endExclusive;
    [ObservableProperty] private int? _categoryId;
    [ObservableProperty] private string _periodText = string.Empty;
    [ObservableProperty] private decimal _overallTotal;
    [ObservableProperty] private ObservableCollection<CategoryExpenseGroup> _groups = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isAnalysisLoading;
    [ObservableProperty] private bool _isOpeningCategoryBills;
    [ObservableProperty] private bool _isPreparingStatement;
    [ObservableProperty] private string _processingMessage = "Loading expense analysis...";
    [ObservableProperty] private ObservableCollection<PaymentModeExpenseChartData> _paymentModes = [];
    [ObservableProperty] private ObservableCollection<DailyExpenseCalendarItem> _dailyTrend = [];
    [ObservableProperty] private ObservableCollection<CategoryExpenseChartData> _categoryChartData = [];
    [ObservableProperty] private ObservableCollection<ShopExpenseChartData> _shopVisits = [];
    public bool HasShopVisits => ShopVisits.Count > 0;
    public IReadOnlyList<ExpensePeriodType> PeriodTypes { get; } = Enum.GetValues<ExpensePeriodType>();
    public bool HasResults => Groups.Count > 0;
    public string OverallTotalText => EuroFormatter.Format(OverallTotal);
    public bool HasPaymentModes => PaymentModes.Count > 0;
    public bool IsMonthlyAnalysis => PeriodType == ExpensePeriodType.Monthly;
    public bool IsOverlayBusy => IsBusy || IsAnalysisLoading || IsOpeningCategoryBills || IsPreparingStatement;

    public ExpenseAnalysisPageModel(ExpenseAnalysisService analysis, ModalErrorHandler errors, IExpenseStatementBuilder statements) { _analysis = analysis; _errors = errors; _statements = statements; }
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsAnalysisLoadingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsOpeningCategoryBillsChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsPreparingStatementChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("reference", out var value) && DateTime.TryParseExact(value?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) ReferenceDate = date;
        if (query.TryGetValue("period", out var period) && Enum.TryParse<ExpensePeriodType>(period?.ToString(), true, out var type)) PeriodType = type;
        if (query.TryGetValue("categoryId", out var category) && int.TryParse(category?.ToString(), out var categoryId)) CategoryId = categoryId;
        LoadAsync().FireAndForgetSafeAsync(_errors);
    }

    partial void OnPeriodTypeChanged(ExpensePeriodType value)
    {
        OnPropertyChanged(nameof(IsMonthlyAnalysis));
        if (_loaded) LoadAsync().FireAndForgetSafeAsync(_errors);
    }
    partial void OnReferenceDateChanged(DateTime value) { if (_loaded) LoadAsync().FireAndForgetSafeAsync(_errors); }

    [RelayCommand] private async Task Appearing() { if (!_loaded) { _loaded = true; await LoadAsync(); } }
    private async Task LoadAsync()
    {
        if (!await _loadGate.WaitAsync(0))
        {
            _reloadRequested = true;
            return;
        }

        try
        {
            IsAnalysisLoading = true;
            await Task.Yield();
            do
            {
                _reloadRequested = false;
                var referenceDate = ReferenceDate;
                var periodType = PeriodType;
                var categoryId = CategoryId;
                ProcessingMessage = CreateProcessingMessage(referenceDate, periodType);
                var range = ExpensePeriodCalculator.Calculate(referenceDate, periodType);
                var result = await _analysis.GetExpenseAnalysisAsync(range.StartDate, range.EndExclusive);
                var groups = categoryId.HasValue ? result.Groups.Where(group => group.CategoryId == categoryId.Value).ToList() : result.Groups.ToList();
                var paymentModes = await _analysis.GetPaymentModeExpenseTotalsAsync(range.StartDate, range.EndExclusive);
                var shopVisits = await _analysis.GetShopVisitsAsync(range.StartDate, range.EndExclusive);

                StartDate = range.StartDate; EndExclusive = range.EndExclusive; PeriodText = range.DisplayText;
                Groups = new ObservableCollection<CategoryExpenseGroup>(groups);
                OverallTotal = groups.Sum(group => group.TotalExpense);
                PaymentModes = new ObservableCollection<PaymentModeExpenseChartData>(paymentModes);
                ShopVisits = new ObservableCollection<ShopExpenseChartData>(shopVisits);
                BuildMonthlyCharts(groups, periodType, range.StartDate);
                OnPropertyChanged(nameof(HasPaymentModes)); OnPropertyChanged(nameof(HasShopVisits));
                OnPropertyChanged(nameof(HasResults)); OnPropertyChanged(nameof(OverallTotalText));
            }
            while (_reloadRequested);
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsAnalysisLoading = false; _loadGate.Release(); }
    }

    private void BuildMonthlyCharts(IReadOnlyCollection<CategoryExpenseGroup> groups, ExpensePeriodType periodType, DateTime startDate)
    {
        if (periodType != ExpensePeriodType.Monthly)
        {
            DailyTrend = [];
            CategoryChartData = [];
            return;
        }

        var bills = groups.SelectMany(group => group.Bills).ToList();
        var totalsByDay = bills
            .GroupBy(bill => bill.BillDate.Day)
            .ToDictionary(group => group.Key, group => group.Sum(bill => bill.BillValue));
        var daysInMonth = DateTime.DaysInMonth(startDate.Year, startDate.Month);
        DailyTrend = new ObservableCollection<DailyExpenseCalendarItem>(
            Enumerable.Range(1, daysInMonth).Select(day => new DailyExpenseCalendarItem
            {
                Date = new DateTime(startDate.Year, startDate.Month, day),
                DayNumber = day,
                TotalValue = totalsByDay.GetValueOrDefault(day)
            }));

        CategoryChartData = new ObservableCollection<CategoryExpenseChartData>(groups
            .Where(group => group.HasExpense)
            .Select(group => new CategoryExpenseChartData
            {
                CategoryId = group.CategoryId,
                CategoryName = group.CategoryName,
                TotalValue = group.TotalExpense,
                Color = ChartColorPalette.ForShop(group.CategoryName)
            }));
    }

    private static string CreateProcessingMessage(DateTime referenceDate, ExpensePeriodType periodType) => periodType switch
    {
        ExpensePeriodType.Monthly => $"Loading {referenceDate:MMMM yyyy} expense analysis...",
        ExpensePeriodType.Yearly => $"Loading {referenceDate:yyyy} expense analysis...",
        ExpensePeriodType.Weekly => "Loading weekly expense analysis...",
        _ => "Loading daily expense analysis..."
    };

    [RelayCommand] private async Task Previous() { if (IsOverlayBusy) return; ReferenceDate = Shift(ReferenceDate, -1); if (!_loaded) await LoadAsync(); }
    [RelayCommand] private async Task Next() { if (IsOverlayBusy) return; ReferenceDate = Shift(ReferenceDate, 1); if (!_loaded) await LoadAsync(); }
    [RelayCommand]
    private async Task OpenStatement()
    {
        if (IsOverlayBusy || !HasResults || !await _statementGate.WaitAsync(0)) return;
        try
        {
            IsPreparingStatement = true;
            ProcessingMessage = "Preparing expense statement...";
            await Task.Yield();
            var statement = _statements.Build(PeriodType, PeriodText, StartDate, EndExclusive, Groups, shopVisits: ShopVisits);
            await Shell.Current.GoToAsync("expensestatement", new ShellNavigationQueryParameters { ["statement"] = statement });
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally
        {
            IsPreparingStatement = false;
            ProcessingMessage = "Loading expense analysis...";
            _statementGate.Release();
        }
    }
    [RelayCommand] private static Task Toggle(CategoryExpenseGroup group) { if (group is not null) group.IsExpanded = !group.IsExpanded; return Task.CompletedTask; }
    private DateTime Shift(DateTime date, int direction) => PeriodType switch { ExpensePeriodType.Daily => date.AddDays(direction), ExpensePeriodType.Weekly => date.AddDays(direction * 7), ExpensePeriodType.Monthly => date.AddMonths(direction), ExpensePeriodType.Yearly => date.AddYears(direction), _ => date };
}
