using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Utilities;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MauiApp1.PageModels;

public partial class MonthlyExpenseCalendarPageModel : ObservableObject, IQueryAttributable
{
    private readonly BillTransactionRepository _bills;
    private readonly ModalErrorHandler _errors;
    [ObservableProperty] private int _year = DateTime.Today.Year;
    [ObservableProperty] private int _month = DateTime.Today.Month;
    [ObservableProperty] private decimal _monthTotal;
    [ObservableProperty] private ObservableCollection<DailyExpenseCalendarItem> _days = [];
    [ObservableProperty] private bool _isBusy;

    public string MonthTitle => new DateTime(Year, Month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture);
    public string MonthTotalText => $"Total: {EuroFormatter.Format(MonthTotal)}";
    public bool HasExpenses => MonthTotal > 0;

    public MonthlyExpenseCalendarPageModel(BillTransactionRepository bills, ModalErrorHandler errors) { _bills = bills; _errors = errors; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("year", out var year) && int.TryParse(year?.ToString(), out var parsedYear)) Year = parsedYear;
        if (query.TryGetValue("month", out var month) && int.TryParse(month?.ToString(), out var parsedMonth) && parsedMonth is >= 1 and <= 12) Month = parsedMonth;
        OnPropertyChanged(nameof(MonthTitle)); OnPropertyChanged(nameof(MonthTotalText));
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (Year < 1 || Month is < 1 or > 12) return;
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            IsBusy = true;
            var totals = await _bills.GetDailyExpenseTotalsAsync(Year, Month);
            var monthStart = new DateTime(Year, Month, 1);
            var highest = totals.Count == 0 ? 0m : totals.Values.Max();
            var firstColumn = ((int)monthStart.DayOfWeek + 6) % 7;
            var items = new List<DailyExpenseCalendarItem>();
            for (var blank = 0; blank < firstColumn; blank++) items.Add(new DailyExpenseCalendarItem { IsPlaceholder = true, GridRow = 1, GridColumn = blank });
            for (var day = 1; day <= DateTime.DaysInMonth(Year, Month); day++)
            {
                var index = firstColumn + day - 1;
                var value = totals.GetValueOrDefault(day);
                items.Add(new DailyExpenseCalendarItem { Date = new DateTime(Year, Month, day), DayNumber = day, TotalValue = value, GridRow = index / 7 + 1, GridColumn = index % 7, IsHighestExpenseDay = highest > 0 && value == highest });
            }
            Days = new ObservableCollection<DailyExpenseCalendarItem>(items);
            MonthTotal = totals.Values.Sum();
            OnPropertyChanged(nameof(MonthTitle)); OnPropertyChanged(nameof(MonthTotalText)); OnPropertyChanged(nameof(HasExpenses));
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task SelectDay(DailyExpenseCalendarItem item) => item.IsPlaceholder ? Task.CompletedTask : Shell.Current.GoToAsync($"dailybills?date={item.Date:yyyy-MM-dd}");
}
