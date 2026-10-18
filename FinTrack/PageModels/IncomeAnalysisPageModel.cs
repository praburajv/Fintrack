using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Utilities;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FinTracker.PageModels;

public partial class IncomeAnalysisPageModel(IncomeTransactionRepository income, ModalErrorHandler errors) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private ExpensePeriodType _periodType = ExpensePeriodType.Monthly;
    [ObservableProperty] private DateTime _referenceDate = DateTime.Today;
    [ObservableProperty] private string _periodText = string.Empty;
    [ObservableProperty] private decimal _overallTotal;
    [ObservableProperty] private ObservableCollection<CategoryExpenseGroup> _groups = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _processingMessage = "Loading income analytics...";
    [ObservableProperty] private ObservableCollection<CategoryExpenseChartData> _categoryChartData = [];
    [ObservableProperty] private ObservableCollection<MonthlyExpenseChartData> _monthlyData = [];
    [ObservableProperty] private ObservableCollection<WeeklyExpenseChartData> _weeklyData = [];
    [ObservableProperty] private ObservableCollection<DailyExpenseCalendarItem> _dailyTrend = [];
    public bool HasCategoryData => CategoryChartData.Count > 0;
    public IReadOnlyList<ExpensePeriodType> PeriodTypes { get; } = [ExpensePeriodType.Monthly, ExpensePeriodType.Yearly];
    public bool HasResults => Groups.Count > 0;
    public string OverallTotalText => EuroFormatter.Format(OverallTotal);

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("reference", out var value) && DateTime.TryParseExact(value?.ToString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) ReferenceDate = date;
        LoadAsync().FireAndForgetSafeAsync(errors);
    }

    [RelayCommand] private async Task Appearing() { if (!IsBusy) await LoadAsync(); }
    partial void OnPeriodTypeChanged(ExpensePeriodType value) => LoadAsync().FireAndForgetSafeAsync(errors);
    partial void OnReferenceDateChanged(DateTime value) => LoadAsync().FireAndForgetSafeAsync(errors);

    private async Task LoadAsync()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            var range = ExpensePeriodCalculator.Calculate(ReferenceDate, PeriodType);
            var transactions = await income.ListAsync(range.StartDate, range.EndExclusive);
            var groups = transactions.GroupBy(x => new { x.CategoryId, x.CategoryName, x.CategoryIconKey }).Select(group =>
            {
                var result = new CategoryExpenseGroup { CategoryId = group.Key.CategoryId, CategoryName = group.Key.CategoryName, IconFileName = CategoryIconCatalog.FileNameFor(group.Key.CategoryIconKey) };
                foreach (var item in group) result.Bills.Add(new BillTransaction { Id = item.Id, BillReferenceNo = item.BillReferenceNo, BillDate = item.TransactionDate, ShopId = item.ShopId, PaymentModeId = item.PaymentModeId, BillValue = item.Amount, Note = item.Note, ShopName = item.ShopName, PaymentModeName = item.PaymentModeName, TransactionType = FinancialTransactionType.Income });
                return result;
            }).OrderByDescending(x => x.TotalExpense).ThenBy(x => x.CategoryName).ToList();
            Groups = new ObservableCollection<CategoryExpenseGroup>(groups);
            OverallTotal = groups.Sum(x => x.TotalExpense);
            PeriodText = range.DisplayText;
            CategoryChartData = new ObservableCollection<CategoryExpenseChartData>(await income.GetCategoryChartAsync(range.StartDate, range.EndExclusive));
            MonthlyData = new ObservableCollection<MonthlyExpenseChartData>(await income.GetMonthlyChartAsync(ReferenceDate.Year));
            var weekStart = ReferenceDate.Date.AddDays(-(((int)ReferenceDate.DayOfWeek + 6) % 7));
            WeeklyData = new ObservableCollection<WeeklyExpenseChartData>(await income.GetWeeklyChartAsync(weekStart));
            DailyTrend = new ObservableCollection<DailyExpenseCalendarItem>(await income.GetDailyChartAsync(ReferenceDate.Year, ReferenceDate.Month));
            OnPropertyChanged(nameof(HasResults)); OnPropertyChanged(nameof(OverallTotalText)); OnPropertyChanged(nameof(HasCategoryData));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand] private Task Previous() { ReferenceDate = PeriodType == ExpensePeriodType.Yearly ? ReferenceDate.AddYears(-1) : ReferenceDate.AddMonths(-1); return Task.CompletedTask; }
    [RelayCommand] private Task Next() { ReferenceDate = PeriodType == ExpensePeriodType.Yearly ? ReferenceDate.AddYears(1) : ReferenceDate.AddMonths(1); return Task.CompletedTask; }
    [RelayCommand] private static Task Toggle(CategoryExpenseGroup group) { if (group is not null) group.IsExpanded = !group.IsExpanded; return Task.CompletedTask; }
}
