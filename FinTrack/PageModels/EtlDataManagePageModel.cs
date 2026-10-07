using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;

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
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    [ObservableProperty] private CategoryExpenseGroup? _group;
    [ObservableProperty] private bool _isBusy = true;
    [ObservableProperty] private string _statusText = string.Empty;

    public bool HasBills => Group?.Bills.Count > 0;
    public bool HasGroup => Group is not null;
    public string EmptyStateText => Group is null ? StatusText : "No ETL bills found.";

    public EtlDataManagePageModel(BillTransactionRepository bills, CategoryRepository categories, ShopRepository shops, EtlTextMapRepository textMaps, EtlTextMappingService mappingService, ModalErrorHandler errors, IAuthenticationService authentication)
    {
        _bills = bills;
        _categories = categories;
        _shops = shops;
        _textMaps = textMaps;
        _mappingService = mappingService;
        _errors = errors;
        _authentication = authentication;
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
            var category = (await _categories.ListAsync()).FirstOrDefault(x => x.Name.Equals(EtlCategoryName, StringComparison.OrdinalIgnoreCase));
            if (category is null)
            {
                Group = null;
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
    private async Task DeleteBill(BillTransaction bill)
    {
        if (bill is null || IsBusy || !await RequireAuthenticationAsync()) return;
        if (!await Shell.Current.DisplayAlertAsync("Delete this bill?", "This action cannot be undone.", "Delete", "Cancel")) return;

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
            await Shell.Current.DisplayAlertAsync("ETL bills", "No ETL bills to delete.", "OK");
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Delete All ETL Bills?",
            "This will permanently delete all bills belonging to the ETL_DT category.\n\nThe ETL_DT category and its shops will not be deleted.\n\nThis action cannot be undone.",
            "Delete All",
            "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var deleted = await _bills.DeleteBillsByCategoryAsync(Group.CategoryId);
            await LoadAsync(true);
            await Shell.Current.DisplayAlertAsync("ETL bills", deleted == 0 ? "No ETL bills to delete." : $"{deleted} ETL bill(s) deleted successfully.", "OK");
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
        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Re-Map ETL Bills?",
            "Current ETL text mappings will be applied to bills still assigned to ETL_DATA_VAULT. Matching bills will be reassigned to their configured Shops.",
            "Re-Map",
            "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var shops = await _shops.ListAsync();
            var holdingShop = shops.FirstOrDefault(x => x.Name.Equals("ETL_DATA_VAULT", StringComparison.OrdinalIgnoreCase));
            if (holdingShop is null)
            {
                await Shell.Current.DisplayAlertAsync("ETL Re-Map", "The ETL_DATA_VAULT Shop was not found.", "OK");
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
            await Shell.Current.DisplayAlertAsync("ETL Re-Map Completed", $"Bills Checked: {bills.Count}\nBills Re-Mapped: {remapped}\nBills Remaining Unmapped: {bills.Count - remapped}", "OK");
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    private async Task<bool> RequireAuthenticationAsync()
    {
        if (AppShell.IsClosing) return false;
        if (_authentication.IsAuthenticated) return true;
        await Shell.Current.DisplayAlertAsync("Authentication Required", "Please login to delete expenses.", "OK");
        return false;
    }
}
