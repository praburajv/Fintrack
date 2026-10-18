using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FinTracker.Models;

namespace FinTracker.PageModels;

public partial class BillEntryPageModel : ObservableObject, IQueryAttributable
{
    private BillTransaction _bill = new();
    private readonly BillTransactionRepository _bills;
    private readonly IncomeTransactionRepository _income;
    private readonly ShopRepository _shops;
    private readonly PaymentModeRepository _modes;
    private readonly ModalErrorHandler _errors;
    private readonly IAuthenticationService _authentication;
    private readonly FinancialAccountContext _accounts;
    private int? _entryAccountId;

    [ObservableProperty] private DateTime _billDate = DateTime.Today;
    [ObservableProperty] private List<Shop> _shopOptions = [];
    [ObservableProperty] private Shop? _shop;
    [ObservableProperty] private List<PaymentMode> _paymentModeOptions = [];
    [ObservableProperty] private PaymentMode? _paymentMode;
    [ObservableProperty] private decimal _billValue;
    [ObservableProperty] private string _billReferenceNo = string.Empty;
    [ObservableProperty] private string _note = string.Empty;
    [ObservableProperty] private FinancialTransactionType _transactionType = FinancialTransactionType.Expense;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    private bool _canMaintainExpenses;

    public bool IsEdit => _bill.Id > 0;
    public bool CanDelete => IsEdit && CanMaintainExpenses;

    public IReadOnlyList<FinancialTransactionType> TransactionTypes { get; } = Enum.GetValues<FinancialTransactionType>();
    public bool IsTransactionTypeEditable => !IsEdit;

    public BillEntryPageModel(BillTransactionRepository bills, IncomeTransactionRepository income, ShopRepository shops, PaymentModeRepository modes, ModalErrorHandler errors, IAuthenticationService authentication, FinancialAccountContext accounts)
    {
        _bills = bills; _income = income; _shops = shops; _modes = modes; _errors = errors; _authentication = authentication; _accounts = accounts;
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
        if (AppShell.IsClosing) return;
        if (_authentication.IsAuthenticated)
        {
            if (_entryAccountId.HasValue && _entryAccountId != _accounts.Current?.Id)
            {
                Shop = null; ShopOptions = []; PaymentMode = null; PaymentModeOptions = [];
                await Shell.Current.GoToAsync(".."); return;
            }
            if (_entryAccountId.HasValue && !IsBusy)
            {
                try
                {
                    var shopId = Shop?.Id; var modeId = PaymentMode?.Id;
                    await LoadMastersAsync();
                    Shop = ShopOptions.FirstOrDefault(x => x.Id == shopId);
                    PaymentMode = PaymentModeOptions.FirstOrDefault(x => x.Id == modeId);
                }
                catch (Exception ex) { _errors.HandleError(ex); }
            }
            return;
        }
        await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Authentication Required", "Please login to add or modify expenses.", "OK");
        if (!AppShell.IsClosing) await Shell.Current.GoToAsync("..");
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && int.TryParse(value?.ToString(), out var id))
            LoadAsync(id, query.TryGetValue("type", out var type) && Enum.TryParse<FinancialTransactionType>(type?.ToString(), true, out var parsed) ? parsed : FinancialTransactionType.Expense).FireAndForgetSafeAsync(_errors);
        else
        {
            TransactionType = query.TryGetValue("type", out var newType) && Enum.TryParse<FinancialTransactionType>(newType?.ToString(), true, out var selectedType)
                ? selectedType : FinancialTransactionType.Expense;
            LoadMastersAsync().FireAndForgetSafeAsync(_errors);
        }
    }

    private async Task LoadMastersAsync()
    {
        var account = _accounts.RequireId();
        var shops = await _shops.ListAsync(); var modes = await _modes.ListAsync();
        if (_accounts.Current?.Id != account) throw new OperationCanceledException("Company changed while loading entry choices.");
        ShopOptions = shops; PaymentModeOptions = modes; _entryAccountId = account;
    }

    private async Task LoadAsync(int id, FinancialTransactionType type)
    {
        IsBusy = true;
        try
        {
            await LoadMastersAsync();
            if (type == FinancialTransactionType.Income)
            {
                var income = await _income.GetAsync(id) ?? throw new InvalidOperationException("Income transaction not found.");
                _bill = new BillTransaction { Id = income.Id, BillDate = income.TransactionDate, ShopId = income.ShopId, PaymentModeId = income.PaymentModeId, BillValue = income.Amount, Note = income.Note, BillReferenceNo = income.BillReferenceNo, ShopName = income.ShopName, PaymentModeName = income.PaymentModeName, ShopCategoryId = income.CategoryId, ShopCategoryName = income.CategoryName, ShopCategoryIconKey = income.CategoryIconKey, TransactionType = FinancialTransactionType.Income };
            }
            else
                _bill = await _bills.GetAsync(id) ?? throw new InvalidOperationException("Bill not found.");
            TransactionType = _bill.TransactionType;
            BillDate = _bill.BillDate; Shop = ShopOptions.FirstOrDefault(x => x.Id == _bill.ShopId);
            PaymentMode = PaymentModeOptions.FirstOrDefault(x => x.Id == _bill.PaymentModeId);
            BillValue = _bill.BillValue; Note = _bill.Note; BillReferenceNo = _bill.BillReferenceNo ?? string.Empty;
            OnPropertyChanged(nameof(IsEdit)); OnPropertyChanged(nameof(IsTransactionTypeEditable)); OnPropertyChanged(nameof(CanDelete)); DeleteCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    private string? Validate()
    {
        if (_entryAccountId != _accounts.Current?.Id) return "The selected company changed. Reopen this entry.";
        if (BillDate == default) return "Bill date is required.";
        if (Shop is null) return "Select a shop.";
        if (PaymentMode is null) return "Select a payment mode.";
        if (BillValue <= 0) return "Bill value must be greater than zero.";
        try { FinTracker.Utilities.TransactionInput.NormalizeReference(BillReferenceNo); }
        catch (ArgumentException ex) { return ex.Message; }
        var noteError = FinTracker.Utilities.TransactionInput.ValidateManualNote(Note, IsEdit ? _bill.Note : null);
        if (noteError is not null) return noteError;
        return null;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (IsBusy) return;
        var error = Validate();
        if (error is not null) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Validation", error, "OK"); return; }
        if (!await RequireAuthenticationAsync()) return;
        IsBusy = true;
        try
        {
            if (!await RequireAuthenticationAsync()) return;
            var previousDate = IsEdit ? _bill.BillDate : (DateTime?)null;
            _bill.BillDate = BillDate; _bill.ShopId = Shop!.Id; _bill.PaymentModeId = PaymentMode!.Id;
            _bill.BillValue = decimal.Round(BillValue, 2); _bill.Note = Note.Trim();
            _bill.BillReferenceNo = FinTracker.Utilities.TransactionInput.NormalizeReference(BillReferenceNo);
            if (TransactionType == FinancialTransactionType.Income)
                await _income.SaveAsync(new IncomeTransaction { Id = _bill.Id, TransactionDate = _bill.BillDate, ShopId = _bill.ShopId, PaymentModeId = _bill.PaymentModeId, Amount = _bill.BillValue, Note = _bill.Note, BillReferenceNo = _bill.BillReferenceNo });
            else
                await _bills.SaveItemAsync(_bill);
            WeakReferenceMessenger.Default.Send(new BillChangedMessage(new BillChanged(previousDate, _bill.BillDate, false)));
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
        if (!await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Delete this bill?", "This action cannot be undone.", "Delete", "Cancel")) return;
        IsBusy = true;
        try
        {
            if (!await RequireAuthenticationAsync()) return;
            var deletedDate = _bill.BillDate;
            if (TransactionType == FinancialTransactionType.Income)
                await _income.DeleteAsync(_bill.Id);
            else
                await _bills.DeleteItemAsync(_bill);
            WeakReferenceMessenger.Default.Send(new BillChangedMessage(new BillChanged(deletedDate, deletedDate, true)));
            await Shell.Current.GoToAsync(".."); await AppShell.DisplayToastAsync("Bill deleted");
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    private async Task<bool> RequireAuthenticationAsync()
    {
        if (AppShell.IsClosing) return false;
        if (_authentication.IsAuthenticated) return true;
        await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Authentication Required", "Please login to add or modify expenses.", "OK");
        return false;
    }
}
