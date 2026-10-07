using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;
using FinTracker.Utilities;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FinTracker.PageModels;

public partial class MonthlyExpenseCalendarPageModel : ObservableObject, IQueryAttributable
{
    private readonly BillTransactionRepository _bills;
    private readonly IncomeTransactionRepository _income;
    private readonly ModalErrorHandler _errors;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    [ObservableProperty] private int _year = DateTime.Today.Year;
    [ObservableProperty] private int _month = DateTime.Today.Month;
    [ObservableProperty] private decimal _monthTotal;
    [ObservableProperty] private ObservableCollection<DailyExpenseCalendarItem> _days = [];
    [ObservableProperty] private ObservableCollection<DailyExpenseCalendarItem> _chartDays = [];
    [ObservableProperty] private ObservableCollection<CategoryExpenseChartData> _categoryChartData = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private FinancialTransactionType _transactionType = FinancialTransactionType.Expense;

    public string MonthTitle => $"{(TransactionType == FinancialTransactionType.Income ? "Income" : "Expense")} Calendar — {new DateTime(Year, Month, 1):MMM yyyy}";
    public string CalendarPeriodText => new DateTime(Year, Month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    public string MonthTotalText => $"Total: {EuroFormatter.Format(MonthTotal)}";
    public bool HasExpenses => MonthTotal > 0;
    public bool HasCategoryExpenses => CategoryChartData.Count > 0;

    public MonthlyExpenseCalendarPageModel(BillTransactionRepository bills, IncomeTransactionRepository income, ModalErrorHandler errors) { _bills = bills; _income = income; _errors = errors; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("year", out var year) && int.TryParse(year?.ToString(), out var parsedYear)) Year = parsedYear;
        if (query.TryGetValue("month", out var month) && int.TryParse(month?.ToString(), out var parsedMonth) && parsedMonth is >= 1 and <= 12) Month = parsedMonth;
        if (query.TryGetValue("type", out var type) && Enum.TryParse<FinancialTransactionType>(type?.ToString(), true, out var parsedType)) TransactionType = parsedType;
        OnPropertyChanged(nameof(MonthTitle)); OnPropertyChanged(nameof(CalendarPeriodText)); OnPropertyChanged(nameof(MonthTotalText));
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
            var incomeMode = TransactionType == FinancialTransactionType.Income;
            var incomeRows = incomeMode ? await _income.ListAsync(new DateTime(Year, Month, 1), new DateTime(Year, Month, 1).AddMonths(1)) : [];
            var totals = incomeMode ? incomeRows.GroupBy(x => x.TransactionDate.Day).ToDictionary(x => x.Key, x => x.Sum(item => item.Amount)) : await _bills.GetDailyExpenseTotalsAsync(Year, Month);
            var categoryTotals = incomeMode ? await _income.GetCategoryChartAsync(new DateTime(Year, Month, 1), new DateTime(Year, Month, 1).AddMonths(1)) : await _bills.GetExpensesByCategoryForMonthAsync(Year, Month);
            var monthStart = new DateTime(Year, Month, 1);
            var highest = totals.Count == 0 ? 0m : totals.Values.Max();
            var firstColumn = ((int)monthStart.DayOfWeek + 6) % 7;
            var items = new List<DailyExpenseCalendarItem>();
            for (var blank = 0; blank < firstColumn; blank++) items.Add(new DailyExpenseCalendarItem { IsPlaceholder = true, GridRow = 1, GridColumn = blank });
            for (var day = 1; day <= DateTime.DaysInMonth(Year, Month); day++)
            {
                var index = firstColumn + day - 1;
                var value = totals.GetValueOrDefault(day);
                items.Add(new DailyExpenseCalendarItem { Date = new DateTime(Year, Month, day), DayNumber = day, TotalValue = value, IsIncome = incomeMode, GridRow = index / 7 + 1, GridColumn = index % 7, IsHighestExpenseDay = highest > 0 && value == highest });
            }
            Days = new ObservableCollection<DailyExpenseCalendarItem>(items);
            ChartDays = new ObservableCollection<DailyExpenseCalendarItem>(items.Where(item => !item.IsPlaceholder));
            CategoryChartData = new ObservableCollection<CategoryExpenseChartData>(categoryTotals);
            MonthTotal = totals.Values.Sum();
            OnPropertyChanged(nameof(MonthTitle)); OnPropertyChanged(nameof(CalendarPeriodText)); OnPropertyChanged(nameof(MonthTotalText)); OnPropertyChanged(nameof(HasExpenses)); OnPropertyChanged(nameof(HasCategoryExpenses));
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; _loadGate.Release(); }
    }

    [RelayCommand]
    private Task SelectDay(DailyExpenseCalendarItem item) => item.IsPlaceholder ? Task.CompletedTask : Shell.Current.GoToAsync($"dailybills?date={item.Date:yyyy-MM-dd}&type={TransactionType}");
    [RelayCommand] private Task PreviousPeriod() { var date = new DateTime(Year, Month, 1).AddMonths(-1); Year = date.Year; Month = date.Month; return LoadAsync(); }
    [RelayCommand] private Task NextPeriod() { var date = new DateTime(Year, Month, 1).AddMonths(1); Year = date.Year; Month = date.Month; return LoadAsync(); }
}
