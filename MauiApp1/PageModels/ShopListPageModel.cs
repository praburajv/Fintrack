using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;

namespace MauiApp1.PageModels;
public partial class ShopListPageModel:ObservableObject
{
 private readonly ShopRepository _shops; private readonly BillTransactionRepository _bills; private readonly ModalErrorHandler _errors;
 [ObservableProperty] private List<Shop> _items=[];
 public ShopListPageModel(ShopRepository shops,BillTransactionRepository bills,ModalErrorHandler errors){_shops=shops;_bills=bills;_errors=errors;}
 [RelayCommand] private async Task Appearing(){try{Items=await _shops.ListAsync();}catch(Exception ex){_errors.HandleError(ex);}}
 [RelayCommand] private Task Add()=>Shell.Current.GoToAsync("shop");
 [RelayCommand] private Task Edit(Shop item)=>Shell.Current.GoToAsync($"shop?id={item.Id}");
 [RelayCommand] private async Task Delete(Shop item){if(await _bills.IsShopReferencedAsync(item.Id)){await Shell.Current.DisplayAlertAsync("Shop in use","This shop cannot be deleted because it is used by existing bills.","OK");return;}if(!await Shell.Current.DisplayAlertAsync("Delete shop?",item.Name,"Delete","Cancel"))return;try{await _shops.DeleteItemAsync(item);Items=await _shops.ListAsync();}catch(Exception ex){_errors.HandleError(ex);}}
}
