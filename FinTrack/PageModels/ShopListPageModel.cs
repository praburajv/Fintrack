using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;
using FinTracker.Utilities;

namespace FinTracker.PageModels;
public partial class ShopListPageModel:ObservableObject
{
 private readonly ShopRepository _shops; private readonly BillTransactionRepository _bills; private readonly ModalErrorHandler _errors;
 [ObservableProperty] private List<Shop> _items=[]; [ObservableProperty] private string _searchText=string.Empty; [ObservableProperty] private IReadOnlyList<SetupSearchSuggestion> _suggestions=[]; private List<Shop> _allItems=[]; private int? _selectedSuggestionId; partial void OnSearchTextChanged(string value){_selectedSuggestionId=null;ApplyFilter();}
 public ShopListPageModel(ShopRepository shops,BillTransactionRepository bills,ModalErrorHandler errors){_shops=shops;_bills=bills;_errors=errors;}
 [RelayCommand] private async Task Appearing(){try{_allItems=await _shops.ListAsync();ApplyFilter();}catch(Exception ex){_errors.HandleError(ex);}} private void ApplyFilter()=>(Items,Suggestions)=SetupSearchFilter.Apply(_allItems,SearchText,_selectedSuggestionId,item=>item.Id,item=>item.Name);
 [RelayCommand] private void SelectSuggestion(SetupSearchSuggestion suggestion){SearchText=suggestion.Name;_selectedSuggestionId=suggestion.Id;ApplyFilter();}
 [RelayCommand] private Task Add()=>Shell.Current.GoToAsync("shop");
 [RelayCommand] private Task Edit(Shop item)=>Shell.Current.GoToAsync($"shop?id={item.Id}");
 [RelayCommand] private async Task Delete(Shop item){if(await _bills.IsShopReferencedAsync(item.Id)){await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Shop in use","This shop cannot be deleted because it is used by existing bills.","OK");return;}if(!await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Delete shop?",item.Name,"Delete","Cancel"))return;try{await _shops.DeleteItemAsync(item);_allItems=await _shops.ListAsync();ApplyFilter();}catch(Exception ex){_errors.HandleError(ex);}}
}
