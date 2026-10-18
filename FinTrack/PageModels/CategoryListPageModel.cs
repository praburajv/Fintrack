using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Utilities;

namespace FinTracker.PageModels;

public partial class CategoryListPageModel : ObservableObject
{
    private readonly CategoryRepository _repo; private readonly ShopRepository _shops; private readonly ModalErrorHandler _errors;
    [ObservableProperty] private List<Category> _items = [];
    private List<Category> _allItems = [];
    private int? _selectedSuggestionId;
    [ObservableProperty] private IReadOnlyList<SetupSearchSuggestion> _suggestions = [];
    [ObservableProperty] private string _searchText = string.Empty;
    partial void OnSearchTextChanged(string value) { _selectedSuggestionId = null; ApplyFilter(); }
    public CategoryListPageModel(CategoryRepository repo, ShopRepository shops, ModalErrorHandler errors) { _repo = repo; _shops = shops; _errors = errors; }
    [RelayCommand] private async Task Appearing() { try { await _shops.ListAsync(); _allItems = await _repo.ListAsync(); ApplyFilter(); } catch (Exception ex) { _errors.HandleError(ex); } }
    private void ApplyFilter() => (Items, Suggestions) = SetupSearchFilter.Apply(_allItems, SearchText, _selectedSuggestionId, item => item.Id, item => item.Name);
    [RelayCommand] private void SelectSuggestion(SetupSearchSuggestion suggestion) { SearchText = suggestion.Name; _selectedSuggestionId = suggestion.Id; ApplyFilter(); }
    [RelayCommand] private Task Add() => Shell.Current.GoToAsync("category");
    [RelayCommand] private Task Edit(Category item) => Shell.Current.GoToAsync($"category?id={item.Id}");
    [RelayCommand] private async Task Delete(Category item)
    {
        if (item.Name.Equals("Default Shop", StringComparison.OrdinalIgnoreCase)) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Category", "Default Shop cannot be deleted.", "OK"); return; }
        if (await _repo.GetShopUsageCountAsync(item.Id) > 0) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Category in use", "This category is assigned to one or more shops.", "OK"); return; }
        if (!await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Delete category?", item.Name, "Delete", "Cancel")) return;
        try { await _repo.DeleteAsync(item); _allItems = await _repo.ListAsync(); ApplyFilter(); } catch (Exception ex) { _errors.HandleError(ex); }
    }
}
