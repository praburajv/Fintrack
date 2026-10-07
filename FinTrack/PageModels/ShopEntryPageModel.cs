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
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private List<Category> _categories = [];
    [ObservableProperty] private Category? _selectedCategory;
    public string CategoryIcon => CategoryIconCatalog.FileNameFor(SelectedCategory?.IconKey);
    public bool IsEdit => _item.Id > 0;
    public ShopEntryPageModel(ShopRepository repo, CategoryRepository categoryRepo, ModalErrorHandler errors) { _repo = repo; _categoryRepo = categoryRepo; _errors = errors; LoadCategoriesAsync().FireAndForgetSafeAsync(_errors); }
    public void ApplyQueryAttributes(IDictionary<string, object> query) { if (query.TryGetValue("id", out var v) && int.TryParse(v?.ToString(), out var id)) LoadAsync(id).FireAndForgetSafeAsync(_errors); else LoadCategoriesAsync().FireAndForgetSafeAsync(_errors); }
    private async Task LoadCategoriesAsync() { Categories = await _categoryRepo.ListAsync(); SelectedCategory = Categories.FirstOrDefault(c => c.Name.Equals("Default Shop", StringComparison.OrdinalIgnoreCase)); }
    private async Task LoadAsync(int id) { try { Categories = await _categoryRepo.ListAsync(); _item = await _repo.GetAsync(id) ?? throw new InvalidOperationException("Shop not found."); Name = _item.Name; SelectedCategory = Categories.FirstOrDefault(c => c.Id == _item.CategoryId) ?? Categories.First(); OnPropertyChanged(nameof(IsEdit)); } catch (Exception ex) { _errors.HandleError(ex); } }
    partial void OnSelectedCategoryChanged(Category? value) => OnPropertyChanged(nameof(CategoryIcon));
    [RelayCommand] private async Task Save() { if (IsBusy) return; Name = Name.Trim(); if (string.IsNullOrWhiteSpace(Name) || SelectedCategory is null) { await Shell.Current.DisplayAlertAsync("Validation", "Shop name and category are required.", "OK"); return; } if (Name.Length > 225) { await Shell.Current.DisplayAlertAsync("Validation", "Shop name cannot exceed 225 characters.", "OK"); return; } IsBusy = true; try { if (await _repo.ExistsNameAsync(Name, _item.Id)) { await Shell.Current.DisplayAlertAsync("Validation", "A shop with this name already exists.", "OK"); return; } _item.Name = Name; _item.CategoryId = SelectedCategory.Id; _item.CategoryIconKey = SelectedCategory.IconKey; _item.Category = ShopCategoryCatalog.FromIconKey(SelectedCategory.IconKey); await _repo.SaveItemAsync(_item); await Shell.Current.GoToAsync(".."); } catch (Exception ex) { _errors.HandleError(ex); } finally { IsBusy = false; } }
    [RelayCommand] private Task Cancel() => Shell.Current.GoToAsync("..");
}
