using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;
using FinTracker.Services;
using FinTracker.Utilities;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FinTracker.PageModels;

public partial class MonthlyExpenseCalendarPageModel : ObservableObject, IQueryAttributable
{
    private readonly BillTransactionRepository _bills;
    private readonly IncomeTransactionRepository _income;
    private readonly ModalErrorHandler _errors;
    private readonly FinancialAccountContext _accounts;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    [ObservableProperty] private int _year = DateTime.Today.Year;
    [ObservableProperty] private int _month = DateTime.Today.Month;
    [ObservableProperty] private decimal _monthTotal;
    [ObservableProperty] private decimal _expenseMonthTotal;
    [ObservableProperty] private decimal _incomeMonthTotal;
    [ObservableProperty] private ObservableCollection<DailyExpenseCalendarItem> _days = [];
    [ObservableProperty] private ObservableCollection<DailyExpenseCalendarItem> _chartDays = [];
    [ObservableProperty] private ObservableCollection<CategoryExpenseChartData> _categoryChartData = [];
    [ObservableProperty] private ObservableCollection<CategoryExpenseChartData> _incomeCategoryChartData = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private FinancialTransactionType _transactionType = FinancialTransactionType.Expense;

    public string MonthTitle => $"{(TransactionType == FinancialTransactionType.Income ? "Income" : "Expense")} Calendar — {new DateTime(Year, Month, 1):MMM yyyy}";
    public string CalendarPeriodText => new DateTime(Year, Month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    public string MonthTotalText => $"Total: {EuroFormatter.Format(MonthTotal)}";
    public string ExpenseMonthTotalText => EuroFormatter.Format(ExpenseMonthTotal);
    public string IncomeMonthTotalText => EuroFormatter.Format(IncomeMonthTotal);
    public bool HasExpenses => MonthTotal > 0;
    public bool HasCategoryExpenses => CategoryChartData.Count > 0;
    public bool HasCategoryIncome => IncomeCategoryChartData.Count > 0;
    public bool IsIncomeMode => TransactionType == FinancialTransactionType.Income;
    public string DailyTrendTitle => IsIncomeMode ? "Daily Income Trend" : "Daily Expense Trend";
    public string HighestDayCaption => IsIncomeMode ? "Red border = Highest total income day" : "Red border = Highest total expense day";

    public MonthlyExpenseCalendarPageModel(BillTransactionRepository bills, IncomeTransactionRepository income, ModalErrorHandler errors, FinancialAccountContext accounts) { _bills = bills; _income = income; _errors = errors; _accounts = accounts; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        TransactionType = FinancialTransactionType.Expense;
        if (query.TryGetValue("year", out var year) && int.TryParse(year?.ToString(), out var parsedYear)) Year = parsedYear;
        if (query.TryGetValue("month", out var month) && int.TryParse(month?.ToString(), out var parsedMonth) && parsedMonth is >= 1 and <= 12) Month = parsedMonth;
        if (query.TryGetValue("type", out var type) && Enum.TryParse<FinancialTransactionType>(type?.ToString(), true, out var parsedType)) TransactionType = parsedType;
        OnPropertyChanged(nameof(MonthTitle)); OnPropertyChanged(nameof(CalendarPeriodText)); OnPropertyChanged(nameof(MonthTotalText));
        OnPropertyChanged(nameof(IsIncomeMode)); OnPropertyChanged(nameof(DailyTrendTitle)); OnPropertyChanged(nameof(HighestDayCaption));
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (Year < 1 || Month is < 1 or > 12) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (!await _loadGate.WaitAsync(0)) return;

        try
        {
            IsBusy = true;
            await Task.Yield();
            var accountId = _accounts.RequireId();
            var year = Year;
            var month = Month;
            var incomeMode = TransactionType == FinancialTransactionType.Income;
            var monthStart = new DateTime(year, month, 1);
            var nextMonth = monthStart.AddMonths(1);
            var expenseTotals = await _bills.GetDailyExpenseTotalsAsync(year, month);
            var incomeRows = await _income.ListAsync(monthStart, nextMonth);
            var expenseCategories = await _bills.GetExpensesByCategoryForMonthAsync(year, month);
            var incomeCategories = await _income.GetCategoryChartAsync(monthStart, nextMonth);
            if (_accounts.RequireId() != accountId || Year != year || Month != month || IsIncomeMode != incomeMode) return;

            var incomeTotals = incomeRows.GroupBy(x => x.TransactionDate.Day).ToDictionary(x => x.Key, x => x.Sum(item => item.Amount));
            var totals = incomeMode ? incomeTotals : expenseTotals;
            var highest = totals.Count == 0 ? 0m : totals.Values.Max();
            var firstColumn = ((int)monthStart.DayOfWeek + 6) % 7;
            var items = new List<DailyExpenseCalendarItem>();
            for (var blank = 0; blank < firstColumn; blank++) items.Add(new DailyExpenseCalendarItem { IsPlaceholder = true, GridRow = 1, GridColumn = blank });
            for (var day = 1; day <= DateTime.DaysInMonth(year, month); day++)
            {
                var index = firstColumn + day - 1;
                var value = totals.GetValueOrDefault(day);
                items.Add(new DailyExpenseCalendarItem { Date = new DateTime(year, month, day), DayNumber = day, TotalValue = value, IsIncome = incomeMode, GridRow = index / 7 + 1, GridColumn = index % 7, IsHighestExpenseDay = highest > 0 && value == highest });
            }
            Days = new ObservableCollection<DailyExpenseCalendarItem>(items);
            ChartDays = new ObservableCollection<DailyExpenseCalendarItem>(items.Where(item => !item.IsPlaceholder));
            CategoryChartData = new ObservableCollection<CategoryExpenseChartData>(expenseCategories);
            IncomeCategoryChartData = new ObservableCollection<CategoryExpenseChartData>(incomeCategories);
            ExpenseMonthTotal = expenseTotals.Values.Sum();
            IncomeMonthTotal = incomeRows.Sum(item => item.Amount);
            MonthTotal = totals.Values.Sum();
            OnPropertyChanged(nameof(MonthTitle)); OnPropertyChanged(nameof(CalendarPeriodText)); OnPropertyChanged(nameof(MonthTotalText));
            OnPropertyChanged(nameof(ExpenseMonthTotalText)); OnPropertyChanged(nameof(IncomeMonthTotalText));
            OnPropertyChanged(nameof(HasExpenses)); OnPropertyChanged(nameof(HasCategoryExpenses)); OnPropertyChanged(nameof(HasCategoryIncome));
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; _loadGate.Release(); }
    }

    [RelayCommand]
    private Task SelectDay(DailyExpenseCalendarItem item) => item.IsPlaceholder ? Task.CompletedTask : Shell.Current.GoToAsync($"dailybills?date={item.Date:yyyy-MM-dd}&type={TransactionType}");
    [RelayCommand] private Task PreviousPeriod() { if (IsBusy) return Task.CompletedTask; var date = new DateTime(Year, Month, 1).AddMonths(-1); Year = date.Year; Month = date.Month; return LoadAsync(); }
    [RelayCommand] private Task NextPeriod() { if (IsBusy) return Task.CompletedTask; var date = new DateTime(Year, Month, 1).AddMonths(1); Year = date.Year; Month = date.Month; return LoadAsync(); }
}
