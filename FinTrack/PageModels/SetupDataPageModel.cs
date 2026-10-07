using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Services;
using FinTracker.Utilities;

namespace FinTracker.PageModels;

public partial class SetupDataPageModel(
    SetUpKeyValueRepository settings,
    ICurrencyService currencies,
    ModalErrorHandler errors) : ObservableObject
{
    private readonly SemaphoreSlim _settingsGate = new(1, 1);
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    [ObservableProperty] private string _selectedTab = "Payment Modes";
    [ObservableProperty] private decimal _monthlyIncome;
    [ObservableProperty] private decimal _monthlyBudget;
    [ObservableProperty] private string _monthlyIncomeEntry = string.Empty;
    [ObservableProperty] private string _monthlyBudgetEntry = string.Empty;
    [ObservableProperty] private bool _isEditingIncome;
    [ObservableProperty] private bool _isEditingBudget;
    [ObservableProperty] private bool _isSettingsBusy;
    [ObservableProperty] private bool _isNavigationBusy;
    [ObservableProperty] private string _processingMessage = "Loading budget and income settings...";
    public bool IsOverlayBusy => IsSettingsBusy || IsNavigationBusy;

    partial void OnIsSettingsBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsNavigationBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));

    public bool IsPaymentModesTab => SelectedTab == "Payment Modes";
    public bool IsCategoriesTab => SelectedTab == "Categories";
    public bool IsShopsTab => SelectedTab == "Shops";
    public string MonthlyIncomeDisplay => EuroFormatter.Format(MonthlyIncome);
    public string MonthlyBudgetDisplay => EuroFormatter.Format(MonthlyBudget);

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsPaymentModesTab));
        OnPropertyChanged(nameof(IsCategoriesTab));
        OnPropertyChanged(nameof(IsShopsTab));
    }

    partial void OnMonthlyIncomeChanged(decimal value) => OnPropertyChanged(nameof(MonthlyIncomeDisplay));
    partial void OnMonthlyBudgetChanged(decimal value) => OnPropertyChanged(nameof(MonthlyBudgetDisplay));

    [RelayCommand] private async Task Appearing() => await LoadSettingsAsync();

    [RelayCommand]
    private void EditIncome()
    {
        if (IsSettingsBusy) return;
        IsEditingBudget = false;
        MonthlyIncomeEntry = MonthlyIncome.ToString("N2", currencies.CurrentCurrency.NumberCulture);
        IsEditingIncome = true;
    }

    [RelayCommand]
    private void EditBudget()
    {
        if (IsSettingsBusy) return;
        IsEditingIncome = false;
        MonthlyBudgetEntry = MonthlyBudget.ToString("N2", currencies.CurrentCurrency.NumberCulture);
        IsEditingBudget = true;
    }

    [RelayCommand] private void CancelIncomeEdit() => IsEditingIncome = false;
    [RelayCommand] private void CancelBudgetEdit() => IsEditingBudget = false;

    [RelayCommand]
    private async Task SaveIncome()
    {
        if (!TryParseAmount(MonthlyIncomeEntry, out var value))
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Monthly Income must be a valid non-negative amount.", "OK");
            return;
        }

        await SaveSettingAsync("Saving Monthly Income...", () => settings.UpdateMonthlyIncomeAsync(value), () =>
        {
            MonthlyIncome = value;
            IsEditingIncome = false;
        });
    }

    [RelayCommand]
    private async Task SaveBudget()
    {
        if (!TryParseAmount(MonthlyBudgetEntry, out var value))
        {
            await Shell.Current.DisplayAlertAsync("Validation", "Monthly Budget must be a valid non-negative amount.", "OK");
            return;
        }

        await SaveSettingAsync("Saving Monthly Budget...", () => settings.UpdateMonthlyBudgetAsync(value), () =>
        {
            MonthlyBudget = value;
            IsEditingBudget = false;
        });
    }

    private async Task LoadSettingsAsync()
    {
        if (!await _settingsGate.WaitAsync(0)) return;
        try
        {
            ProcessingMessage = "Loading budget and income settings...";
            IsSettingsBusy = true;
            await Task.Yield();
            var values = await settings.GetBudgetSettingsAsync();
            MonthlyIncome = values.MonthlyIncome;
            MonthlyBudget = values.MonthlyBudget;
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsSettingsBusy = false; _settingsGate.Release(); }
    }

    private async Task SaveSettingAsync(string message, Func<Task> save, Action apply)
    {
        if (!await _settingsGate.WaitAsync(0)) return;
        try
        {
            ProcessingMessage = message;
            IsSettingsBusy = true;
            await Task.Yield();
            await save();
            apply();
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsSettingsBusy = false; _settingsGate.Release(); }
    }

    private bool TryParseAmount(string? text, out decimal value)
    {
        var input = text?.Trim();
        var valid = decimal.TryParse(input, NumberStyles.Number, currencies.CurrentCurrency.NumberCulture, out value)
            || decimal.TryParse(input, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-PT"), out value)
            || decimal.TryParse(input, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
        return valid && value >= 0;
    }

    [RelayCommand] private Task OpenPaymentModes() => Shell.Current.GoToAsync("paymentmodes");
    [RelayCommand] private Task OpenCategories() => Shell.Current.GoToAsync("categories");
    [RelayCommand] private Task OpenShops() => Shell.Current.GoToAsync("shops");
    [RelayCommand] private Task OpenRecurringBills() => Shell.Current.GoToAsync("recurringbills");
    [RelayCommand] private Task OpenEtlData() => Shell.Current.GoToAsync("etldata");
    [RelayCommand] private Task OpenEtlDataManage() => NavigateToDataPageAsync("etldatamanage", "Opening ETL Data Manage...");
    [RelayCommand] private Task OpenEtlDataMapping() => NavigateToDataPageAsync("etldatamapping", "Opening ETL Data Mapping...");
    [RelayCommand] private Task OpenExpenseDataRemoval() => NavigateToDataPageAsync("monthlyexpensedata", "Opening Monthly Expense Data...");

    private async Task NavigateToDataPageAsync(string route, string message)
    {
        if (!await _navigationGate.WaitAsync(0)) return;
        try
        {
            ProcessingMessage = message;
            IsNavigationBusy = true;
            await Task.Yield();
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally
        {
            IsNavigationBusy = false;
            ProcessingMessage = string.Empty;
            _navigationGate.Release();
        }
    }
}
