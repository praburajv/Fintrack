using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Data;
using MauiApp1.Models;
using System.Globalization;

namespace MauiApp1.PageModels;

public partial class CategoryEntryPageModel : ObservableObject, IQueryAttributable
{
    private readonly CategoryRepository _repo; private readonly ModalErrorHandler _errors; private Category _item = new();
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _budgetedText = string.Empty;
    [ObservableProperty] private CategoryIconOption _selectedIcon = CategoryIconCatalog.All[0];
    [ObservableProperty] private bool _isBusy;
    public IReadOnlyList<CategoryIconOption> IconOptions => CategoryIconCatalog.All;
    public string IconPreview => SelectedIcon.IconFileName;
    public bool IsEdit => _item.Id > 0;
    public CategoryEntryPageModel(CategoryRepository repo, ModalErrorHandler errors) { _repo = repo; _errors = errors; }
    public void ApplyQueryAttributes(IDictionary<string, object> query) { if (query.TryGetValue("id", out var v) && int.TryParse(v?.ToString(), out var id)) LoadAsync(id).FireAndForgetSafeAsync(_errors); }
    private async Task LoadAsync(int id) { try { _item = await _repo.GetAsync(id) ?? throw new InvalidOperationException("Category not found."); Name = _item.Name; BudgetedText = _item.Budgeted == 0 ? string.Empty : _item.Budgeted.ToString("N2", CultureInfo.GetCultureInfo("pt-PT")); SelectedIcon = CategoryIconCatalog.Find(_item.IconKey); OnPropertyChanged(nameof(IsEdit)); } catch (Exception ex) { _errors.HandleError(ex); } }
    partial void OnSelectedIconChanged(CategoryIconOption value) => OnPropertyChanged(nameof(IconPreview));
    [RelayCommand] private async Task Save() { if (IsBusy) return; Name = Name.Trim(); if (string.IsNullOrWhiteSpace(Name)) { await Shell.Current.DisplayAlertAsync("Validation", "Category name is required.", "OK"); return; } if (!decimal.TryParse(BudgetedText?.Trim(), NumberStyles.Number, CultureInfo.GetCultureInfo("pt-PT"), out var budgeted) && !decimal.TryParse(BudgetedText?.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out budgeted)) { await Shell.Current.DisplayAlertAsync("Validation", "Budgeted must be a valid amount, for example 3000,00.", "OK"); return; } if (budgeted < 0) { await Shell.Current.DisplayAlertAsync("Validation", "Budgeted cannot be negative.", "OK"); return; } if (_item.Name.Equals("Default Shop", StringComparison.OrdinalIgnoreCase) && !Name.Equals("Default Shop", StringComparison.OrdinalIgnoreCase)) { await Shell.Current.DisplayAlertAsync("Validation", "Default Shop cannot be renamed.", "OK"); return; } IsBusy = true; try { if (await _repo.ExistsByNameAsync(Name, _item.Id)) { await Shell.Current.DisplayAlertAsync("Validation", "A category with this name already exists.", "OK"); return; } _item.Name = Name; _item.IconKey = SelectedIcon.IconKey; _item.Budgeted = budgeted; if (_item.Id == 0) await _repo.InsertAsync(_item); else await _repo.UpdateAsync(_item); await Shell.Current.GoToAsync(".."); } catch (Exception ex) { _errors.HandleError(ex); } finally { IsBusy = false; } }
    [RelayCommand] private Task Cancel() => Shell.Current.GoToAsync("..");
}
