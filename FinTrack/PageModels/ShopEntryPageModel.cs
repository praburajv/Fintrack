using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;

namespace FinTracker.PageModels;

public partial class ShopEntryPageModel : ObservableObject, IQueryAttributable
{
    private Shop _item = new();
    private readonly ShopRepository _repo;
    private readonly CategoryRepository _categoryRepo;
    private readonly ModalErrorHandler _errors;
    private readonly FinancialAccountContext _accounts;
    private int? _entryAccount;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private List<Category> _categories = [];
    [ObservableProperty] private Category? _selectedCategory;
    public string CategoryIcon => CategoryIconCatalog.FileNameFor(SelectedCategory?.IconKey);
    public bool IsEdit => _item.Id > 0;
    public ShopEntryPageModel(ShopRepository repo, CategoryRepository categoryRepo, ModalErrorHandler errors, FinancialAccountContext accounts) { _repo = repo; _categoryRepo = categoryRepo; _errors = errors; _accounts = accounts; LoadCategoriesAsync().FireAndForgetSafeAsync(_errors); }
    public void ApplyQueryAttributes(IDictionary<string, object> query) { if (query.TryGetValue("id", out var v) && int.TryParse(v?.ToString(), out var id)) LoadAsync(id).FireAndForgetSafeAsync(_errors); else LoadCategoriesAsync().FireAndForgetSafeAsync(_errors); }
    private async Task LoadCategoriesAsync()
    {
        var account = _accounts.RequireId();
        var categories = await _categoryRepo.ListAsync();
        if (_accounts.Current?.Id != account) return;
        Categories = categories; _entryAccount = account;
        SelectedCategory = Categories.FirstOrDefault(c => c.Name.Equals("Default Shop", StringComparison.OrdinalIgnoreCase));
    }
    private async Task LoadAsync(int id) { try { await LoadCategoriesAsync(); _item = await _repo.GetAsync(id) ?? throw new InvalidOperationException("Shop not found."); Name = _item.Name; SelectedCategory = Categories.FirstOrDefault(c => c.Id == _item.CategoryId) ?? Categories.First(); OnPropertyChanged(nameof(IsEdit)); } catch (Exception ex) { _errors.HandleError(ex); } }
    partial void OnSelectedCategoryChanged(Category? value) => OnPropertyChanged(nameof(CategoryIcon));
    [RelayCommand] private async Task Save() { if (IsBusy) return; if (_entryAccount != _accounts.Current?.Id) { await FinTracker.Pages.Controls.ThemeDialog.AlertAsync("Shop", "The selected company changed. Reopen this entry."); return; } Name = Name.Trim(); if (string.IsNullOrWhiteSpace(Name) || SelectedCategory is null) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Validation", "Shop name and category are required.", "OK"); return; } if (Name.Length > 225) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Validation", "Shop name cannot exceed 225 characters.", "OK"); return; } IsBusy = true; try { if (await _repo.ExistsNameAsync(Name, _item.Id)) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Validation", "A shop with this name already exists.", "OK"); return; } _item.Name = Name; _item.CategoryId = SelectedCategory.Id; _item.CategoryIconKey = SelectedCategory.IconKey; _item.Category = ShopCategoryCatalog.FromIconKey(SelectedCategory.IconKey); await _repo.SaveItemAsync(_item); await Shell.Current.GoToAsync(".."); } catch (Exception ex) { _errors.HandleError(ex); } finally { IsBusy = false; } }
    [RelayCommand] private Task Cancel() => Shell.Current.GoToAsync("..");
}
