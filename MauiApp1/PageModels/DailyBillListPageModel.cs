using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Utilities;
using System.Collections.ObjectModel;

namespace MauiApp1.PageModels;

public partial class DailyBillListPageModel : ObservableObject, IQueryAttributable
{
    private readonly BillTransactionRepository _bills;
    private readonly ModalErrorHandler _errors;
    [ObservableProperty] private DateTime _selectedDate;
    [ObservableProperty] private ObservableCollection<BillTransaction> _items = [];
    [ObservableProperty] private decimal _dailyTotal;
    [ObservableProperty] private bool _isBusy;

    public string DateTitle => $"Bills — {SelectedDate:dd/MM/yyyy}";
    public string TotalText => $"Total: {EuroFormatter.Format(DailyTotal)}";
    public bool HasBills => Items.Count > 0;
    public string EmptyText => $"No bills found for {SelectedDate:dd/MM/yyyy}.";

    public DailyBillListPageModel(BillTransactionRepository bills, ModalErrorHandler errors) { _bills = bills; _errors = errors; }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("date", out var value) && DateTime.TryParseExact(value?.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
            SelectedDate = date;
        OnPropertyChanged(nameof(DateTitle)); OnPropertyChanged(nameof(TotalText)); OnPropertyChanged(nameof(EmptyText));
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (SelectedDate == default) return;
        try
        {
            IsBusy = true; Items = new ObservableCollection<BillTransaction>(await _bills.GetBillsForDateAsync(SelectedDate)); DailyTotal = Items.Sum(item => item.BillValue);
            OnPropertyChanged(nameof(HasBills)); OnPropertyChanged(nameof(TotalText)); OnPropertyChanged(nameof(EmptyText));
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

}
