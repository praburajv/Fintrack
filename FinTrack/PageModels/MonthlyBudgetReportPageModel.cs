using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Utilities;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FinTracker.PageModels;

public partial class MonthlyBudgetReportPageModel(
    ExpenseAnalysisService analysis,
    ICurrencyService currencies,
    ModalErrorHandler errors) : ObservableObject, IQueryAttributable
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _reportGate = new(1, 1);
    [ObservableProperty] private DateTime _selectedMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private decimal _income, _monthlyBudget, _actualExpense;
    [ObservableProperty] private ObservableCollection<CategoryExpenseGroup> _groups = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isBudgetLoading, _isPreparingReport;
    [ObservableProperty] private string _processingMessage = "Loading monthly budget...";
    public bool IsOverlayBusy => IsBusy || IsBudgetLoading || IsPreparingReport;

    public string MonthText => SelectedMonth.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("en-US"));
    public string MonthlyBudgetText => EuroFormatter.Format(MonthlyBudget);
    public string ActualExpenseText => EuroFormatter.Format(ActualExpense);
    public decimal BudgetRemaining => MonthlyBudget - ActualExpense;
    public string BudgetBalanceLabel => "Remaining Budget";
    public string BudgetBalanceText => EuroFormatter.Format(BudgetRemaining);
    public Color BudgetBalanceColor => BudgetRemaining >= 0 ? Colors.Green : Colors.Red;
    public string BudgetUsedPercent => MonthlyBudget > 0 ? $"{ActualExpense / MonthlyBudget * 100:0.0}%" : "Not available";
    public double BudgetProgress => MonthlyBudget > 0 ? (double)Math.Min(ActualExpense / MonthlyBudget, 1) : 0;
    public string BudgetHealthText => MonthlyBudget <= 0 ? "No budget configured" : ActualExpense / MonthlyBudget * 100 > 100 ? "Over Budget" : ActualExpense / MonthlyBudget * 100 >= 80 ? "Approaching Budget" : "Within Budget";
    public Color BudgetHealthColor => BudgetHealthText == "Over Budget" ? Colors.Red : BudgetHealthText == "Approaching Budget" ? Colors.Orange : Colors.Green;
    public bool HasIncome => true;
    public string IncomeTextForDisplay => EuroFormatter.Format(Income);
    public string IncomeRemainingText => EuroFormatter.Format(Math.Abs(Income - ActualExpense));
    public string IncomeRemainingLabel => Income >= ActualExpense ? "Income Remaining" : "Expenses exceed income by";
    public bool HasResults => Groups.Count > 0;
    public bool HasConfiguredBudgetWithoutExpenses => ActualExpense == 0 && Groups.Any(x => x.HasBudget);
    public bool HasNoInformation => Groups.Count == 0;
    public string EmptyMonthText => Groups.Any(x => x.HasBudget) ? $"No expenses recorded for {MonthText}. Your configured category budgets are shown below." : $"No budget or expense information is available for {MonthText}.";
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsBudgetLoadingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsPreparingReportChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("reference", out var value) && DateTime.TryParseExact(value?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            SelectedMonth = new DateTime(date.Year, date.Month, 1);
        LoadAsync().FireAndForgetSafeAsync(errors);
    }

    [RelayCommand] private async Task Appearing() { if (!IsOverlayBusy) await LoadAsync(); }
    [RelayCommand] private async Task PreviousMonth() { if (!IsOverlayBusy) await LoadAsync(SelectedMonth.AddMonths(-1)); }
    [RelayCommand] private async Task NextMonth() { if (!IsOverlayBusy) await LoadAsync(SelectedMonth.AddMonths(1)); }
    [RelayCommand] private static Task Toggle(CategoryExpenseGroup group) { if (group is not null) group.IsExpanded = !group.IsExpanded; return Task.CompletedTask; }

    private async Task LoadAsync(DateTime? requestedMonth = null)
    {
        if (!await _loadGate.WaitAsync(0)) return;
        try
        {
            var start = requestedMonth is { } requested
                ? new DateTime(requested.Year, requested.Month, 1)
                : new DateTime(SelectedMonth.Year, SelectedMonth.Month, 1);
            var end = start.AddMonths(1);
            ProcessingMessage = $"Loading budget for {start:MMMM yyyy}...";
            IsBudgetLoading = true;
            await Task.Yield();
            var result = await analysis.GetMonthlyBudgetAnalysisAsync(start, end);

            SelectedMonth = start;
            Groups = new ObservableCollection<CategoryExpenseGroup>(result.Groups);
            MonthlyBudget = result.Summary.MonthlyBudget;
            ActualExpense = result.Summary.ActualExpense;
            Income = result.Summary.MonthlyIncome;
            NotifySummary(); OnPropertyChanged(nameof(HasResults)); OnPropertyChanged(nameof(HasConfiguredBudgetWithoutExpenses)); OnPropertyChanged(nameof(HasNoInformation)); OnPropertyChanged(nameof(EmptyMonthText));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBudgetLoading = false; _loadGate.Release(); }
    }

    [RelayCommand]
    private async Task OpenBudgetReport()
    {
        if (IsOverlayBusy || !await _reportGate.WaitAsync(0)) return;
        try
        {
            ProcessingMessage = "Preparing budget report...";
            IsPreparingReport = true;
            await Task.Yield();
            var report = new BudgetReportSnapshot
            {
                PeriodTitle = MonthText,
                Month = SelectedMonth,
                CurrencyCode = currencies.CurrentCurrency.Code,
                MonthlyIncomeText = IncomeTextForDisplay,
                MonthlyBudgetText = MonthlyBudgetText,
                ActualExpenseText = ActualExpenseText,
                RemainingBudgetText = BudgetBalanceText,
                BudgetUsedPercentText = BudgetUsedPercent,
                BudgetHealthText = BudgetHealthText,
                Categories = Groups.Select(group => new BudgetReportCategory
                {
                    CategoryName = group.CategoryName,
                    BudgetedText = group.HasBudget ? group.BudgetedText : "Not configured",
                    ActualExpenseText = group.TotalText,
                    RemainingText = group.HasBudget ? group.DifferenceText : "Not available",
                    StatusText = group.BudgetStatusText
                }).ToArray()
            };
            await Shell.Current.GoToAsync("budgetreport", new ShellNavigationQueryParameters { ["report"] = report });
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally
        {
            IsPreparingReport = false;
            ProcessingMessage = "Loading monthly budget...";
            _reportGate.Release();
        }
    }

    private void NotifySummary()
    {
        OnPropertyChanged(nameof(MonthText)); OnPropertyChanged(nameof(MonthlyBudgetText)); OnPropertyChanged(nameof(ActualExpenseText)); OnPropertyChanged(nameof(BudgetRemaining)); OnPropertyChanged(nameof(BudgetBalanceLabel)); OnPropertyChanged(nameof(BudgetBalanceText)); OnPropertyChanged(nameof(BudgetBalanceColor)); OnPropertyChanged(nameof(BudgetUsedPercent)); OnPropertyChanged(nameof(BudgetProgress)); OnPropertyChanged(nameof(BudgetHealthText)); OnPropertyChanged(nameof(BudgetHealthColor)); OnPropertyChanged(nameof(HasIncome)); OnPropertyChanged(nameof(IncomeTextForDisplay)); OnPropertyChanged(nameof(IncomeRemainingText)); OnPropertyChanged(nameof(IncomeRemainingLabel));
    }
}
