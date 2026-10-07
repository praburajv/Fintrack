using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Utilities;
using System.Collections.ObjectModel;

namespace FinTracker.PageModels;

public partial class PaymentModeBillListPageModel(BillTransactionRepository bills, ModalErrorHandler errors) : ObservableObject, IQueryAttributable
{
    [ObservableProperty] private int _paymentModeId;
    [ObservableProperty] private string _paymentModeName = string.Empty;
    [ObservableProperty] private DateTime _startDate, _endDate;
    [ObservableProperty] private ObservableCollection<BillTransaction> _items = [];
    [ObservableProperty] private ObservableCollection<CategoryExpenseGroup> _groups = [];
    [ObservableProperty] private decimal _total;
    [ObservableProperty] private bool _isBusy;
    public string PeriodText => $"{StartDate:dd/MM/yyyy} – {EndDate.AddDays(-1):dd/MM/yyyy}";
    public string TitleText => $"{PaymentModeName} — {PeriodText}";
    public string TotalText => $"Total: {EuroFormatter.Format(Total)}";
    public bool HasBills => Items.Count > 0;
    public bool HasGroups => Groups.Count > 0;
    public string EmptyText => $"No bills found for {PaymentModeName} in this period.";
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("paymentModeId", out var id) && int.TryParse(id?.ToString(), out var parsedId)) PaymentModeId = parsedId;
        if (query.TryGetValue("paymentModeName", out var name)) PaymentModeName = Uri.UnescapeDataString(name?.ToString() ?? string.Empty);
        if (query.TryGetValue("start", out var start) && DateTime.TryParseExact(start?.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedStart)) StartDate = parsedStart;
        if (query.TryGetValue("end", out var end) && DateTime.TryParseExact(end?.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedEnd)) EndDate = parsedEnd;
        OnPropertyChanged(nameof(PeriodText)); OnPropertyChanged(nameof(TitleText)); OnPropertyChanged(nameof(TotalText)); OnPropertyChanged(nameof(EmptyText));
    }
    [RelayCommand]
    private async Task Appearing()
    {
        if (IsBusy || PaymentModeId <= 0 || StartDate == default || EndDate <= StartDate) return;
        IsBusy = true;
        try
        {
            Items = new(await bills.GetBillsByPaymentModeAndDateRangeAsync(PaymentModeId, StartDate, EndDate));
            Groups = CategoryExpenseGrouping.Group(Items);
            Total = Items.Sum(x => x.BillValue);
            OnPropertyChanged(nameof(HasBills)); OnPropertyChanged(nameof(HasGroups)); OnPropertyChanged(nameof(TotalText)); OnPropertyChanged(nameof(EmptyText));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private static Task ToggleCategory(CategoryExpenseGroup group)
    {
        if (group is not null) group.IsExpanded = !group.IsExpanded;
        return Task.CompletedTask;
    }
}
