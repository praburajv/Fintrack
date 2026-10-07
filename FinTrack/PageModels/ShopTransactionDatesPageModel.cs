using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;
using FinTracker.Services;

namespace FinTracker.PageModels;

public partial class ShopTransactionDatesPageModel(ShopBillAnalysisService analysis, ModalErrorHandler errors) : ObservableObject, IQueryAttributable
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private bool _loaded;

    [ObservableProperty] private int _shopId;
    [ObservableProperty] private int _year;
    [ObservableProperty] private int _month;
    [ObservableProperty] private string _shopName = string.Empty;
    [ObservableProperty] private ObservableCollection<ShopTransactionDateSummary> _dates = [];
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isOpeningDate;
    [ObservableProperty] private string _processingMessage = "Loading transaction dates...";

    public string MonthTitle => Year > 0 && Month is >= 1 and <= 12 ? new DateTime(Year, Month, 1).ToString("MMMM yyyy", CultureInfo.CurrentCulture) : string.Empty;
    public bool HasDates => Dates.Count > 0;
    public bool IsOverlayBusy => IsLoading || IsOpeningDate;
    public string EmptyText => $"No transactions found for {ShopName} in {MonthTitle}.";

    partial void OnIsLoadingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsOpeningDateChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("shopId", out var shop) && int.TryParse(shop?.ToString(), out var shopId)) ShopId = shopId;
        if (query.TryGetValue("year", out var year) && int.TryParse(year?.ToString(), out var parsedYear)) Year = parsedYear;
        if (query.TryGetValue("month", out var month) && int.TryParse(month?.ToString(), out var parsedMonth)) Month = parsedMonth;
        OnPropertyChanged(nameof(MonthTitle));
        OnPropertyChanged(nameof(EmptyText));
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (_loaded || ShopId <= 0 || Year is < 1 or > 9998 || Month is < 1 or > 12) return;
        _loaded = true;
        if (!await _loadGate.WaitAsync(0)) return;
        try
        {
            ProcessingMessage = $"Loading {MonthTitle} transaction dates...";
            IsLoading = true;
            await Task.Yield();
            var result = await analysis.GetTransactionDatesAsync(ShopId, Year, Month);
            ShopName = result.Shop.Name;
            Dates = new ObservableCollection<ShopTransactionDateSummary>(result.Dates);
            OnPropertyChanged(nameof(HasDates));
            OnPropertyChanged(nameof(EmptyText));
        }
        catch (Exception ex) { _loaded = false; errors.HandleError(ex); }
        finally { IsLoading = false; _loadGate.Release(); }
    }

    [RelayCommand]
    private async Task OpenDate(ShopTransactionDateSummary date)
    {
        if (date is null || IsOverlayBusy || !await _navigationGate.WaitAsync(0)) return;
        try
        {
            ProcessingMessage = $"Opening bills for {date.Date:dd MMMM yyyy}...";
            IsOpeningDate = true;
            await Task.Yield();
            await Shell.Current.GoToAsync($"dailybills?shopId={ShopId}&date={date.Date:yyyy-MM-dd}");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsOpeningDate = false; _navigationGate.Release(); }
    }
}
