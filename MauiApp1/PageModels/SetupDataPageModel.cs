using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
namespace MauiApp1.PageModels;
public partial class SetupDataPageModel : ObservableObject
{
    [ObservableProperty] private string _selectedTab = "Payment Modes";
    public bool IsPaymentModesTab => SelectedTab == "Payment Modes"; public bool IsCategoriesTab => SelectedTab == "Categories"; public bool IsShopsTab => SelectedTab == "Shops";
    partial void OnSelectedTabChanged(string value) { OnPropertyChanged(nameof(IsPaymentModesTab)); OnPropertyChanged(nameof(IsCategoriesTab)); OnPropertyChanged(nameof(IsShopsTab)); }
    [RelayCommand] private Task OpenPaymentModes() => Shell.Current.GoToAsync("paymentmodes");
    [RelayCommand] private Task OpenCategories() => Shell.Current.GoToAsync("categories");
    [RelayCommand] private Task OpenShops() => Shell.Current.GoToAsync("shops");
}
