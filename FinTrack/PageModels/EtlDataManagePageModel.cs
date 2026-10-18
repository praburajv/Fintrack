using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;
using System.Collections.ObjectModel;
using FinTracker.Pages.Controls;

namespace FinTracker.PageModels;

public partial class EtlDataManagePageModel : ObservableObject
{
    private const string EtlCategoryName = "ETL_DT";
    private readonly BillTransactionRepository _bills;
    private readonly CategoryRepository _categories;
    private readonly ShopRepository _shops;
    private readonly EtlTextMapRepository _textMaps;
    private readonly EtlTextMappingService _mappingService;
    private readonly ModalErrorHandler _errors;
    private readonly IAuthenticationService _authentication;
    private readonly FinancialAccountContext _accounts;
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    [ObservableProperty] private CategoryExpenseGroup? _group;
    [ObservableProperty] private bool _isBusy = true;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private ObservableCollection<EtlMonthGroup> _months = [];
    [ObservableProperty] private string _companyName = string.Empty;

    public bool HasBills => Months.Count > 0;
    public bool HasGroup => Group is not null;
    public string EmptyStateText => Group is null ? StatusText : "No ETL bills found.";

    public EtlDataManagePageModel(BillTransactionRepository bills, CategoryRepository categories, ShopRepository shops, EtlTextMapRepository textMaps, EtlTextMappingService mappingService, ModalErrorHandler errors, IAuthenticationService authentication, FinancialAccountContext accounts)
    {
        _bills = bills;
        _categories = categories;
        _shops = shops;
        _textMaps = textMaps;
        _mappingService = mappingService;
        _errors = errors;
        _authentication = authentication;
        _accounts = accounts;
    }

    [RelayCommand]
    private async Task Appearing() => await LoadAsync();

    private async Task LoadAsync(bool? expanded = null)
    {
        if (!await _loadGate.WaitAsync(0)) return;
        try
        {
            IsBusy = true;
            await Task.Yield();
            var accountId = _accounts.RequireId();
            CompanyName = _accounts.Current?.Name ?? string.Empty;
            var category = (await _categories.ListAsync()).FirstOrDefault(x => x.Name.Equals(EtlCategoryName, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                Group = null;
                Months = [];
                StatusText = "The ETL_DT category was not found. Configure it before managing ETL bills.";
                OnPropertyChanged(nameof(HasBills));
                OnPropertyChanged(nameof(HasGroup));
                OnPropertyChanged(nameof(EmptyStateText));
                return;
            }

            var group = new CategoryExpenseGroup
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                IconFileName = CategoryIconCatalog.FileNameFor(category.IconKey),
                IsExpanded = expanded ?? true
            };
            var bills = await _bills.GetBillsByCategoryAsync(category.Id);
            var number = 1;
            foreach (var bill in bills)
            {
                bill.DisplayNumber = number++;
                group.Bills.Add(bill);
            }

            if (_accounts.RequireId() != accountId) return;
            var oldExpanded = Months.ToDictionary(x => x.Month, x => x.IsExpanded);
            var months = new ObservableCollection<EtlMonthGroup>();
            foreach (var set in bills.GroupBy(x => new DateTime(x.BillDate.Year, x.BillDate.Month, 1)).OrderByDescending(x => x.Key))
            {
                var item = new EtlMonthGroup { Month = set.Key, FinancialAccountId = accountId, IsExpanded = oldExpanded.GetValueOrDefault(set.Key, false) };
                foreach (var bill in set) item.Bills.Add(bill);
                months.Add(item);
            }
            Months = months;

            Group = group;
            StatusText = string.Empty;
            OnPropertyChanged(nameof(HasBills));
            OnPropertyChanged(nameof(HasGroup));
            OnPropertyChanged(nameof(EmptyStateText));
        }
        catch (Exception ex)
        {
            StatusText = "ETL bills could not be loaded.";
            OnPropertyChanged(nameof(EmptyStateText));
            _errors.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
            _loadGate.Release();
        }
    }

    [RelayCommand]
    private void ToggleCategory()
    {
        if (Group is not null)
            Group.IsExpanded = !Group.IsExpanded;
    }

    [RelayCommand]
    private void ToggleMonth(EtlMonthGroup month)
    {
        if (month is not null) month.IsExpanded = !month.IsExpanded;
    }

    [RelayCommand]
    private async Task DeleteMonth(EtlMonthGroup month)
    {
        if (month is null || IsBusy || Group is null || month.FinancialAccountId != _accounts.RequireId() || !await RequireAuthenticationAsync()) return;
        var company = _accounts.Current?.Name ?? "this company";
        if (!await ThemeDialog.ConfirmAsync("Delete ETL month?",
            $"Delete {month.Count} ETL expense bills from {month.Title} for {company}? Other months and manually categorized bills will remain.", "Delete month")) return;
        IsBusy = true;
        try
        {
            if (month.FinancialAccountId != _accounts.RequireId()) return;
            await _bills.DeleteBillsByCategoryForMonthAsync(Group.CategoryId, month.Month.Year, month.Month.Month);
            await LoadAsync();
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ProcessMonth(EtlMonthGroup month)
    {
        if (month is null || IsBusy || month.FinancialAccountId != _accounts.RequireId() || !await RequireAuthenticationAsync()) return;
        if (!await ThemeDialog.ConfirmAsync("Process ETL month?", $"Apply current mappings to {month.Title} for {_accounts.Current?.Name}?", "Process")) return;
        IsBusy = true;
        try
        {
            var shops = await _shops.ListAsync();
            var holding = shops.FirstOrDefault(x => x.Name.Equals("ETL_DATA_VAULT", StringComparison.OrdinalIgnoreCase));
            if (holding is null) throw new InvalidOperationException("Create ETL_DATA_VAULT before processing this month.");
            var mapping = await _textMaps.ListAsync();
            var shopIds = shops.Select(x => x.Id).ToHashSet();
            var candidates = month.Bills.Where(x => x.ShopId == holding.Id).ToArray();
            var assignments = candidates.Select(x => (Bill: x, Target: _mappingService.FindMappedShopId(x.Note, mapping, shopIds)))
                .Where(x => x.Target.HasValue && x.Target.Value != holding.Id).ToDictionary(x => x.Bill.Id, x => x.Target!.Value);
            var changed = await _bills.ReassignShopIdsAsync(holding.Id, assignments);
            await LoadAsync();
            await ThemeDialog.AlertAsync("ETL month", $"{changed} bill(s) processed for {month.Title}.");
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteBill(BillTransaction bill)
    {
        if (bill is null || IsBusy || !await RequireAuthenticationAsync()) return;
        if (!await ThemeDialog.ConfirmAsync("Delete this bill?", "This action cannot be undone.", "Delete")) return;

        IsBusy = true;
        try
        {
            await _bills.DeleteItemAsync(bill);
            await LoadAsync(Group?.IsExpanded ?? true);
            await AppShell.DisplayToastAsync("Bill deleted");
        }
        catch (Exception ex)
        {
            _errors.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAll()
    {
        if (Group is null || IsBusy || !await RequireAuthenticationAsync()) return;
        if (!HasBills)
        {
            await ThemeDialog.AlertAsync("ETL bills", "No ETL bills to delete.");
            return;
        }

        var confirmed = await ThemeDialog.ConfirmAsync(
            "Delete All ETL Bills?",
            "This will permanently delete all bills belonging to the ETL_DT category.\n\nThe ETL_DT category and its shops will not be deleted.\n\nThis action cannot be undone.",
            "Delete All");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var deleted = await _bills.DeleteBillsByCategoryAsync(Group.CategoryId);
            await LoadAsync(true);
            await ThemeDialog.AlertAsync("ETL bills", deleted == 0 ? "No ETL bills to delete." : $"{deleted} ETL bill(s) deleted successfully.");
        }
        catch (Exception ex)
        {
            _errors.HandleError(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task Remap()
    {
        if (IsBusy || !await RequireAuthenticationAsync()) return;
        var confirmed = await ThemeDialog.ConfirmAsync(
            "Re-Map ETL Bills?",
            "Current ETL text mappings will be applied to bills still assigned to ETL_DATA_VAULT. Matching bills will be reassigned to their configured Shops.",
            "Re-Map");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var shops = await _shops.ListAsync();
            var holdingShop = shops.FirstOrDefault(x => x.Name.Equals("ETL_DATA_VAULT", StringComparison.OrdinalIgnoreCase));
            if (holdingShop is null)
            {
                await ThemeDialog.AlertAsync("ETL Re-Map", "The ETL_DATA_VAULT Shop was not found.");
                return;
            }

            var bills = await _bills.GetBillsByShopIdAsync(holdingShop.Id);
            var mappings = await _textMaps.ListAsync();
            var availableShopIds = shops.Select(x => x.Id).ToHashSet();
            var assignments = bills
                .Select(bill => new { Bill = bill, ShopId = _mappingService.FindMappedShopId(bill.Note, mappings, availableShopIds) })
                .Where(x => x.ShopId.HasValue && x.ShopId.Value != holdingShop.Id)
                .ToDictionary(x => x.Bill.Id, x => x.ShopId!.Value);

            var remapped = await _bills.ReassignShopIdsAsync(holdingShop.Id, assignments);
            await LoadAsync(Group?.IsExpanded ?? true);
            await ThemeDialog.AlertAsync("ETL Re-Map Completed", $"Bills Checked: {bills.Count}\nBills Re-Mapped: {remapped}\nBills Remaining Unmapped: {bills.Count - remapped}");
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    private async Task<bool> RequireAuthenticationAsync()
    {
        if (AppShell.IsClosing) return false;
        if (_authentication.IsAuthenticated) return true;
        await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Authentication Required", "Please login to delete expenses.", "OK");
        return false;
    }
}
