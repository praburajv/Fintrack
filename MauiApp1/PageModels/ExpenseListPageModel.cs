using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using System.Collections.ObjectModel;

namespace MauiApp1.PageModels;

public partial class ExpenseListPageModel : ObservableObject
{
    private readonly BillTransactionRepository _bills;
    private readonly ModalErrorHandler _errors;
    private readonly IAuthenticationService _authentication;
    [ObservableProperty] private ObservableCollection<ExpenseMonthGroup> _months = [];
    [ObservableProperty] private int _year = DateTime.Today.Year;
    [ObservableProperty] private bool _isBusy;
    public string YearTitle => Year.ToString();
    public bool HasNoExpenses => Months.Count == 0;
    [ObservableProperty] private bool _canMaintainExpenses;
    public ExpenseListPageModel(BillTransactionRepository bills, ModalErrorHandler errors, IAuthenticationService authentication)
    {
        _bills=bills; _errors=errors; _authentication=authentication;
        CanMaintainExpenses = authentication.IsAuthenticated;
        _authentication.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    private void OnAuthenticationStateChanged(object? sender, EventArgs e)
    {
        RefreshAuthorization();
    }

    public void RefreshAuthorization() => CanMaintainExpenses = _authentication.IsAuthenticated;

    [RelayCommand] private async Task Appearing() { RefreshAuthorization(); await LoadAsync(); }
    [RelayCommand] private async Task Load() => await LoadAsync();
    [RelayCommand] private async Task PreviousYear() { Year--; await LoadAsync(); }
    [RelayCommand] private async Task NextYear() { Year++; await LoadAsync(); }
    private async Task LoadAsync()
    {
        try { IsBusy=true; var bills=await _bills.ListAsync(Year); var groups=bills.GroupBy(b=>new { b.BillDate.Year,b.BillDate.Month }).OrderBy(g=>g.Key.Year).ThenBy(g=>g.Key.Month).Select(g=>{var x=new ExpenseMonthGroup{Year=g.Key.Year,MonthNumber=g.Key.Month}; var number=1; foreach(var b in g.OrderBy(b=>b.BillDate).ThenBy(b=>b.Id)){b.DisplayNumber=number++;x.Bills.Add(b);} return x;}); Months=new(groups); OnPropertyChanged(nameof(YearTitle)); OnPropertyChanged(nameof(HasNoExpenses)); }
        catch(Exception ex){_errors.HandleError(ex);}
        finally { IsBusy=false; }
    }
    [RelayCommand] private void ToggleMonth(ExpenseMonthGroup group) { if(group is not null) group.IsExpanded=!group.IsExpanded; }
    [RelayCommand]
    private async Task AddBill()
    {
        if (!await RequireAuthenticationAsync()) return;
        await Shell.Current.GoToAsync("bill");
    }

    [RelayCommand]
    private async Task EditBill(BillTransaction bill)
    {
        if (bill is null || !await RequireAuthenticationAsync()) return;
        await Shell.Current.GoToAsync($"bill?id={bill.Id}");
    }

    private async Task<bool> RequireAuthenticationAsync()
    {
        if (AppShell.IsClosing) return false;
        if (_authentication.IsAuthenticated) return true;
        await Shell.Current.DisplayAlertAsync("Authentication Required", "Please login to add or modify expenses.", "OK");
        return false;
    }
    [RelayCommand] private Task ManageShops()=>Shell.Current.GoToAsync("shops");
    [RelayCommand] private Task ManagePaymentModes()=>Shell.Current.GoToAsync("paymentmodes");
}
