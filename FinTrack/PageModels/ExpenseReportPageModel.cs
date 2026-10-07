using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Utilities;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FinTracker.PageModels;

public partial class ExpenseReportPageModel(ExpenseAnalysisService analysis, BillTransactionRepository bills, ModalErrorHandler errors, IExpenseStatementBuilder statements) : ObservableObject, IQueryAttributable
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _statementGate = new(1, 1);
    [ObservableProperty] private ExpensePeriodType _periodType = ExpensePeriodType.Daily;
    [ObservableProperty] private DateTime _referenceDate = DateTime.Today;
    [ObservableProperty] private DateTime _startDate;
    [ObservableProperty] private DateTime _endExclusive;
    [ObservableProperty] private string _periodText = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private decimal _overallTotal;
    [ObservableProperty] private ObservableCollection<CategoryExpenseGroup> _groups = [];
    [ObservableProperty] private decimal _overallIncome;
    [ObservableProperty] private ObservableCollection<CategoryExpenseGroup> _incomeGroups = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isReportLoading;
    [ObservableProperty] private bool _isPreparingStatement;
    [ObservableProperty] private bool _isShopVisitBusy;
    [ObservableProperty] private bool _shopVisitSortAscending;
    [ObservableProperty] private string _processingMessage = "Loading expense report...";
    [ObservableProperty] private ObservableCollection<PaymentModeExpenseChartData> _paymentModes = [];
    [ObservableProperty] private ObservableCollection<ShopExpenseChartData> _shopVisits = [];
    public bool HasResults => Groups.Count > 0;
    public bool HasIncomeResults => IncomeGroups.Count > 0;
    public string OverallTotalText => EuroFormatter.Format(OverallTotal);
    public string OverallIncomeText => EuroFormatter.Format(OverallIncome);
    public bool HasPaymentModes => PaymentModes.Count > 0;
    public bool HasShopVisits => ShopVisits.Count > 0;
    public bool IsOverlayBusy => IsBusy || IsReportLoading || IsPreparingStatement || IsShopVisitBusy;
    partial void OnIsShopVisitBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsReportLoadingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsPreparingStatementChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("reference", out var value) && DateTime.TryParseExact(value?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) ReferenceDate = date;
        if (query.TryGetValue("period", out var period) && Enum.TryParse<ExpensePeriodType>(period?.ToString(), true, out var type) && type != ExpensePeriodType.Yearly) PeriodType = type;
        LoadAsync().FireAndForgetSafeAsync(errors);
    }

    [RelayCommand] private async Task Appearing() { if (!IsOverlayBusy && Groups.Count == 0) await LoadAsync(); }
    private async Task LoadAsync()
    {
        if (!await _loadGate.WaitAsync(0)) return;
        try
        {
            var referenceDate = ReferenceDate;
            var periodType = PeriodType;
            ProcessingMessage = periodType switch
            {
                ExpensePeriodType.Daily => $"Loading {referenceDate:dd MMMM yyyy} expense report...",
                ExpensePeriodType.Weekly => "Loading weekly expense report...",
                ExpensePeriodType.Monthly => $"Loading {referenceDate:MMMM yyyy} expense report...",
                _ => "Loading expense report..."
            };
            IsReportLoading = true;
            await Task.Yield();
            var range = ExpensePeriodCalculator.Calculate(referenceDate, periodType);
            var result = await analysis.GetExpenseAnalysisAsync(range.StartDate, range.EndExclusive);
            var paymentModes = await bills.GetPaymentModeExpenseTotalsAsync(range.StartDate, range.EndExclusive);
            var shopVisits = await bills.GetExpensesByShopAsync(range.StartDate, range.EndExclusive);

            StartDate = range.StartDate; EndExclusive = range.EndExclusive; PeriodText = range.DisplayText; Description = ExpenseReportDescription.For(periodType);
            foreach (var group in result.Groups)
            {
                var ordered = ReportBillOrdering.Order(group.Bills).ToArray();
                group.Bills.Clear();
                foreach (var bill in ordered) group.Bills.Add(bill);
            }
            foreach (var group in result.IncomeGroups)
            {
                var ordered = ReportBillOrdering.Order(group.Bills).ToArray();
                group.Bills.Clear();
                foreach (var bill in ordered) group.Bills.Add(bill);
            }
            Groups = new ObservableCollection<CategoryExpenseGroup>(result.Groups); OverallTotal = result.OverallTotal;
            IncomeGroups = new ObservableCollection<CategoryExpenseGroup>(result.IncomeGroups);
            OverallIncome = result.OverallIncome;
            PaymentModes = new ObservableCollection<PaymentModeExpenseChartData>(paymentModes);
            ShopVisits = new ObservableCollection<ShopExpenseChartData>(shopVisits);
            OnPropertyChanged(nameof(HasPaymentModes)); OnPropertyChanged(nameof(HasShopVisits));
            OnPropertyChanged(nameof(HasResults)); OnPropertyChanged(nameof(OverallTotalText));
            OnPropertyChanged(nameof(HasIncomeResults)); OnPropertyChanged(nameof(OverallIncomeText));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsReportLoading = false; _loadGate.Release(); }
    }

    [RelayCommand] private static Task Toggle(CategoryExpenseGroup group) { if (group is not null) group.IsExpanded = !group.IsExpanded; return Task.CompletedTask; }
    [RelayCommand]
    private async Task Previous()
    {
        if (IsOverlayBusy) return;
        ReferenceDate = Shift(ReferenceDate, -1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task Next()
    {
        if (IsOverlayBusy) return;
        ReferenceDate = Shift(ReferenceDate, 1);
        await LoadAsync();
    }

    [RelayCommand]
    private async Task OpenStatement()
    {
        if (IsOverlayBusy || !HasResults || !await _statementGate.WaitAsync(0)) return;
        try
        {
            IsPreparingStatement = true;
            ProcessingMessage = "Preparing expense statement...";
            await Task.Yield();
            var statement = statements.Build(PeriodType, PeriodText, StartDate, EndExclusive, Groups,
                shopVisits: ShopVisitOrdering.Order(ShopVisits, ShopVisitSortAscending));
            await Shell.Current.GoToAsync("expensestatement", new ShellNavigationQueryParameters { ["statement"] = statement, ["shopVisitSortAscending"] = ShopVisitSortAscending });
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally
        {
            IsPreparingStatement = false;
            ProcessingMessage = "Loading expense report...";
            _statementGate.Release();
        }
    }

    private DateTime Shift(DateTime date, int direction) => PeriodType switch
    {
        ExpensePeriodType.Daily => date.AddDays(direction),
        ExpensePeriodType.Weekly => date.AddDays(direction * 7),
        ExpensePeriodType.Monthly => date.AddMonths(direction),
        _ => date
    };
}
