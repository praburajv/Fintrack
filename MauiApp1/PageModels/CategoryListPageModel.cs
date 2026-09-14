using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Data;
using MauiApp1.Models;

namespace MauiApp1.PageModels;

public partial class CategoryListPageModel : ObservableObject
{
    private readonly CategoryRepository _repo; private readonly ShopRepository _shops; private readonly ModalErrorHandler _errors;
    [ObservableProperty] private List<Category> _items = [];
    public CategoryListPageModel(CategoryRepository repo, ShopRepository shops, ModalErrorHandler errors) { _repo = repo; _shops = shops; _errors = errors; }
    [RelayCommand] private async Task Appearing() { try { await _shops.ListAsync(); Items = await _repo.ListAsync(); } catch (Exception ex) { _errors.HandleError(ex); } }
    [RelayCommand] private Task Add() => Shell.Current.GoToAsync("category");
    [RelayCommand] private Task Edit(Category item) => Shell.Current.GoToAsync($"category?id={item.Id}");
    [RelayCommand] private async Task Delete(Category item)
    {
        if (item.Name.Equals("Default Shop", StringComparison.OrdinalIgnoreCase)) { await Shell.Current.DisplayAlertAsync("Category", "Default Shop cannot be deleted.", "OK"); return; }
        if (await _repo.GetShopUsageCountAsync(item.Id) > 0) { await Shell.Current.DisplayAlertAsync("Category in use", "This category is assigned to one or more shops.", "OK"); return; }
        if (!await Shell.Current.DisplayAlertAsync("Delete category?", item.Name, "Delete", "Cancel")) return;
        try { await _repo.DeleteAsync(item); Items = await _repo.ListAsync(); } catch (Exception ex) { _errors.HandleError(ex); }
    }
}
