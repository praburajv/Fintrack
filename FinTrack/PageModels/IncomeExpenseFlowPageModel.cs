using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Utilities;
using FinTracker.Services;
using System.Collections.ObjectModel;

namespace FinTracker.PageModels;

public partial class IncomeExpenseFlowPageModel(IncomeExpenseFlowService flow, BillTransactionRepository bills, IncomeTransactionRepository income, IExpenseStatementBuilder statements, ModalErrorHandler errors) : ObservableObject
{
    [ObservableProperty] private int _year = DateTime.Today.Year;
    [ObservableProperty] private int? _month;
    [ObservableProperty] private ObservableCollection<FinancialFlowMonth> _months = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isPrinting;
    [ObservableProperty] private string _processingMessage = "Loading income and expense flow...";
    private bool _suppressPeriodReload;
    public IReadOnlyList<int?> MonthOptions { get; } = new int?[] { null }.Concat(Enumerable.Range(1, 12).Cast<int?>()).ToArray();
    public string MonthText => Month is null ? "All" : new DateTime(2000, Month.Value, 1).ToString("MMM", System.Globalization.CultureInfo.InvariantCulture);
    public string PeriodDisplayText => Month is null ? Year.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{new DateTime(2000, Month.Value, 1):MMMM} {Year}";
    public bool HasMonth => Month.HasValue;
    public string TotalIncomeText => EuroFormatter.Format(Months.Sum(item => item.Income));
    public string TotalExpenseText => EuroFormatter.Format(Months.Sum(item => item.Expense));
    public bool HasData => Months.Count > 0;
    public bool IsOverlayBusy => IsBusy || IsPrinting;

    partial void OnYearChanged(int value) { OnPropertyChanged(nameof(PeriodDisplayText)); if (!_suppressPeriodReload) Reload().FireAndForgetSafeAsync(errors); }
    partial void OnMonthChanged(int? value) { OnPropertyChanged(nameof(MonthText)); OnPropertyChanged(nameof(PeriodDisplayText)); OnPropertyChanged(nameof(HasMonth)); if (!_suppressPeriodReload) Reload().FireAndForgetSafeAsync(errors); }
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsPrintingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    [RelayCommand] private async Task Appearing() { if (!IsBusy) await Reload(); }
    [RelayCommand] private Task PreviousPeriod()
    {
        if (Month is int month) SetPeriod(new DateTime(Year, month, 1).AddMonths(-1)); else SetYear(Year - 1);
        return Task.CompletedTask;
    }
    [RelayCommand] private Task NextPeriod()
    {
        if (Month is int month) SetPeriod(new DateTime(Year, month, 1).AddMonths(1)); else SetYear(Year + 1);
        return Task.CompletedTask;
    }
    [RelayCommand] private Task ClearMonth() { Month = null; return Task.CompletedTask; }
    [RelayCommand] private Task PrintIncome() => PrintAsync(true);
    [RelayCommand] private Task PrintExpense() => PrintAsync(false);

    private async Task PrintAsync(bool printIncome)
    {
        if (IsOverlayBusy) return;
        IsPrinting = true;
        try
        {
            var range = GetRange();
            ProcessingMessage = printIncome ? "Preparing income report..." : "Preparing expense report...";
            IEnumerable<CategoryExpenseGroup> groups;
            if (printIncome)
            {
                var rows = await income.ListAsync(range.StartDate, range.EndExclusive);
                groups = rows.GroupBy(item => new { item.CategoryId, item.CategoryName, item.CategoryIconKey }).Select(group =>
                {
                    var category = new CategoryExpenseGroup { CategoryId = group.Key.CategoryId, CategoryName = group.Key.CategoryName, IconFileName = CategoryIconCatalog.FileNameFor(group.Key.CategoryIconKey), IsIncome = true };
                    foreach (var item in group) category.Bills.Add(new BillTransaction { Id = item.Id, BillReferenceNo = item.BillReferenceNo, BillDate = item.TransactionDate, BillValue = item.Amount, ShopName = item.ShopName, PaymentModeName = item.PaymentModeName, TransactionType = FinancialTransactionType.Income });
                    return category;
                }).ToList();
            }
            else
                groups = CategoryExpenseGrouping.Group(await bills.GetBillsForDateRangeAsync(range.StartDate, range.EndExclusive));
            var title = printIncome ? "Income Report" : "Expense Report";
            var statement = statements.Build(Month is null ? ExpensePeriodType.Yearly : ExpensePeriodType.Monthly, Month is null ? Year.ToString() : MonthText + " " + Year, range.StartDate, range.EndExclusive, groups, title);
            await Shell.Current.GoToAsync("expensestatement", new ShellNavigationQueryParameters { ["statement"] = statement });
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsPrinting = false; }
    }

    private ExpensePeriodRange GetRange() => Month is int month ? new ExpensePeriodRange(new DateTime(Year, month, 1), new DateTime(Year, month, 1).AddMonths(1), MonthText + " " + Year) : new ExpensePeriodRange(new DateTime(Year, 1, 1), new DateTime(Year + 1, 1, 1), Year.ToString());

    private void SetYear(int year)
    {
        _suppressPeriodReload = true;
        try { Year = year; }
        finally { _suppressPeriodReload = false; }
        Reload().FireAndForgetSafeAsync(errors);
    }

    private void SetPeriod(DateTime period)
    {
        _suppressPeriodReload = true;
        try { Year = period.Year; Month = period.Month; }
        finally { _suppressPeriodReload = false; }
        OnPropertyChanged(nameof(MonthText)); OnPropertyChanged(nameof(PeriodDisplayText)); OnPropertyChanged(nameof(HasMonth));
        Reload().FireAndForgetSafeAsync(errors);
    }

    private async Task Reload()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            ProcessingMessage = Month is null ? $"Loading {Year} income and expenses..." : $"Loading {MonthText} {Year} income and expenses...";
            Months = new ObservableCollection<FinancialFlowMonth>(await flow.LoadAsync(Year, Month));
            OnPropertyChanged(nameof(MonthText)); OnPropertyChanged(nameof(TotalIncomeText)); OnPropertyChanged(nameof(TotalExpenseText)); OnPropertyChanged(nameof(HasData));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }
}
