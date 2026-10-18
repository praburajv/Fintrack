using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace FinTracker.PageModels;

public partial class RecurringBillEntryPageModel : ObservableObject, IQueryAttributable
{
    private readonly RecurringBillRepository _repository;
    private readonly ShopRepository _shops;
    private readonly PaymentModeRepository _paymentModes;
    private readonly ModalErrorHandler _errors;
    private RecurringBill _item = new();
    [ObservableProperty] private string _amountText = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private ObservableCollection<Shop> _shopOptions = [];
    [ObservableProperty] private Shop? _selectedShop;
    [ObservableProperty] private ObservableCollection<PaymentMode> _paymentModeOptions = [];
    [ObservableProperty] private PaymentMode? _selectedPaymentMode;
    [ObservableProperty] private ObservableCollection<DateTime> _monthOptions = [];
    [ObservableProperty] private DateTime _selectedStartMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime? _selectedEndMonth;
    [ObservableProperty] private bool _hasEndDate, _isActive = true, _isBusy;
    public bool IsEdit => _item.Id > 0;
    public string SelectedStartMonthText => SelectedStartMonth.ToString("MMMM yyyy");
    public string SelectedEndMonthText => SelectedEndMonth?.ToString("MMMM yyyy") ?? "No end date";
    public RecurringBillEntryPageModel(RecurringBillRepository repository, ShopRepository shops, PaymentModeRepository paymentModes, ModalErrorHandler errors) { _repository = repository; _shops = shops; _paymentModes = paymentModes; _errors = errors; }
    public void ApplyQueryAttributes(IDictionary<string, object> query) { InitializeAsync(query).FireAndForgetSafeAsync(_errors); }
    private async Task InitializeAsync(IDictionary<string, object> query) { await LoadMastersAsync(); if (query.TryGetValue("id", out var value) && int.TryParse(value?.ToString(), out var id)) await LoadAsync(id); }
    private async Task LoadMastersAsync() { ShopOptions = new ObservableCollection<Shop>(await _shops.ListAsync()); PaymentModeOptions = new ObservableCollection<PaymentMode>(await _paymentModes.ListAsync()); MonthOptions = new ObservableCollection<DateTime>(Enumerable.Range(-12, 37).Select(x => new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(x))); }
    private async Task LoadAsync(int id) { _item = await _repository.GetAsync(id) ?? throw new InvalidOperationException("Recurring bill not found."); AmountText = _item.Amount.ToString("N2", CultureInfo.GetCultureInfo("pt-PT")); Description = _item.Description; SelectedStartMonth = _item.StartMonth; SelectedEndMonth = _item.EndMonth; HasEndDate = _item.EndMonth.HasValue; IsActive = _item.IsActive; SelectedShop = ShopOptions.FirstOrDefault(x => x.Id == _item.ShopId); SelectedPaymentMode = PaymentModeOptions.FirstOrDefault(x => x.Id == _item.PaymentModeId); OnPropertyChanged(nameof(IsEdit)); }
    partial void OnHasEndDateChanged(bool value) { if (!value) SelectedEndMonth = null; OnPropertyChanged(nameof(SelectedEndMonthText)); }
    [RelayCommand] private async Task Save() { if (IsBusy) return; if (SelectedShop is null || SelectedPaymentMode is null || string.IsNullOrWhiteSpace(Description) || !decimal.TryParse(AmountText?.Trim(), NumberStyles.Number, CultureInfo.GetCultureInfo("pt-PT"), out var amount) || amount <= 0 || (HasEndDate && SelectedEndMonth < SelectedStartMonth)) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Validation", "Select a shop and payment mode, enter a positive amount and description, and use a valid month range.", "OK"); return; } IsBusy = true; try { _item.ShopId = SelectedShop.Id; _item.PaymentModeId = SelectedPaymentMode.Id; _item.Amount = decimal.Round(amount, 2); _item.Description = Description.Trim(); _item.StartMonth = new DateTime(SelectedStartMonth.Year, SelectedStartMonth.Month, 1); _item.EndMonth = HasEndDate ? new DateTime(SelectedEndMonth!.Value.Year, SelectedEndMonth.Value.Month, 1) : null; _item.IsActive = IsActive; await _repository.SaveAsync(_item); await Shell.Current.GoToAsync(".."); } catch (Exception ex) { _errors.HandleError(ex); } finally { IsBusy = false; } }
    [RelayCommand] private Task Cancel() => Shell.Current.GoToAsync("..");
}
