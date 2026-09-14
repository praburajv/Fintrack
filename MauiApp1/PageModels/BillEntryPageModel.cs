using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;

namespace MauiApp1.PageModels;

public partial class BillEntryPageModel : ObservableObject, IQueryAttributable
{
    private BillTransaction _bill = new();
    private readonly BillTransactionRepository _bills;
    private readonly ShopRepository _shops;
    private readonly PaymentModeRepository _modes;
    private readonly ModalErrorHandler _errors;
    private readonly IAuthenticationService _authentication;

    [ObservableProperty] private DateTime _billDate = DateTime.Today;
    [ObservableProperty] private List<Shop> _shopOptions = [];
    [ObservableProperty] private Shop? _shop;
    [ObservableProperty] private List<PaymentMode> _paymentModeOptions = [];
    [ObservableProperty] private PaymentMode? _paymentMode;
    [ObservableProperty] private decimal _billValue;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private bool _canMaintainExpenses;

    public bool IsEdit => _bill.Id > 0;
    public bool CanDelete => IsEdit && CanMaintainExpenses;

    public BillEntryPageModel(BillTransactionRepository bills, ShopRepository shops, PaymentModeRepository modes, ModalErrorHandler errors, IAuthenticationService authentication)
    {
        _bills = bills; _shops = shops; _modes = modes; _errors = errors; _authentication = authentication;
        CanMaintainExpenses = authentication.IsAuthenticated;
        _authentication.AuthenticationStateChanged += OnAuthenticationStateChanged;
    }

    private void OnAuthenticationStateChanged(object? sender, EventArgs e)
    {
        CanMaintainExpenses = _authentication.IsAuthenticated;
        OnPropertyChanged(nameof(CanDelete));
        DeleteCommand.NotifyCanExecuteChanged();
    }

    public async Task EnsureAuthenticatedAsync()
    {
        if (_authentication.IsAuthenticated || AppShell.IsClosing) return;
        await Shell.Current.DisplayAlertAsync("Authentication Required", "Please login to add or modify expenses.", "OK");
        if (!AppShell.IsClosing) await Shell.Current.GoToAsync("..");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && int.TryParse(value?.ToString(), out var id))
            LoadAsync(id).FireAndForgetSafeAsync(_errors);
        else
            LoadMastersAsync().FireAndForgetSafeAsync(_errors);
    }

    private async Task LoadMastersAsync() { ShopOptions = await _shops.ListAsync(); PaymentModeOptions = await _modes.ListAsync(); }

    private async Task LoadAsync(int id)
    {
        IsBusy = true;
        try
        {
            await LoadMastersAsync();
            _bill = await _bills.GetAsync(id) ?? throw new InvalidOperationException("Bill not found.");
            BillDate = _bill.BillDate; Shop = ShopOptions.FirstOrDefault(x => x.Id == _bill.ShopId);
            PaymentMode = PaymentModeOptions.FirstOrDefault(x => x.Id == _bill.PaymentModeId);
            BillValue = _bill.BillValue; Note = _bill.Note;
            OnPropertyChanged(nameof(IsEdit)); OnPropertyChanged(nameof(CanDelete)); DeleteCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    private string? Validate()
    {
        if (BillDate == default) return "Bill date is required.";
        if (Shop is null) return "Select a shop.";
        if (PaymentMode is null) return "Select a payment mode.";
        if (BillValue <= 0) return "Bill value must be greater than zero.";
        if (Note.Length > 1000) return "Note cannot exceed 1000 characters.";
        return null;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (IsBusy) return;
        var error = Validate();
        if (error is not null) { await Shell.Current.DisplayAlertAsync("Validation", error, "OK"); return; }
        if (!await RequireAuthenticationAsync()) return;
        IsBusy = true;
        try
        {
            if (!await RequireAuthenticationAsync()) return;
            _bill.BillDate = BillDate; _bill.ShopId = Shop!.Id; _bill.PaymentModeId = PaymentMode!.Id;
            _bill.BillValue = decimal.Round(BillValue, 2); _bill.Note = Note.Trim();
            await _bills.SaveItemAsync(_bill);
            await Shell.Current.GoToAsync(".."); await AppShell.DisplayToastAsync("Bill saved");
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand] private async Task Cancel() => await Shell.Current.GoToAsync("..");
    [RelayCommand] private Task ManageShops() => Shell.Current.GoToAsync("shops");
    [RelayCommand] private Task ManagePaymentModes() => Shell.Current.GoToAsync("paymentmodes");

    [RelayCommand(CanExecute = nameof(CanDelete))]
    private async Task Delete()
    {
        if (IsBusy) return;
        if (!await RequireAuthenticationAsync()) return;
        if (!await Shell.Current.DisplayAlertAsync("Delete this bill?", "This action cannot be undone.", "Delete", "Cancel")) return;
        IsBusy = true;
        try
        {
            if (!await RequireAuthenticationAsync()) return;
            await _bills.DeleteItemAsync(_bill);
            await Shell.Current.GoToAsync(".."); await AppShell.DisplayToastAsync("Bill deleted");
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    private async Task<bool> RequireAuthenticationAsync()
    {
        if (AppShell.IsClosing) return false;
        if (_authentication.IsAuthenticated) return true;
        await Shell.Current.DisplayAlertAsync("Authentication Required", "Please login to add or modify expenses.", "OK");
        return false;
    }
}
