using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;

namespace MauiApp1.PageModels;

public partial class PaymentModeEntryPageModel : ObservableObject, IQueryAttributable
{
    private PaymentMode _item = new();
    private readonly PaymentModeRepository _repo;
    private readonly ModalErrorHandler _errors;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isBusy;
    public bool IsEdit => _item.Id > 0;
    public PaymentModeEntryPageModel(PaymentModeRepository repo, ModalErrorHandler errors) { _repo = repo; _errors = errors; }
    public void ApplyQueryAttributes(IDictionary<string, object> query) { if (query.TryGetValue("id", out var v) && int.TryParse(v?.ToString(), out var id)) LoadAsync(id).FireAndForgetSafeAsync(_errors); }
    private async Task LoadAsync(int id) { try { _item = await _repo.GetAsync(id) ?? throw new InvalidOperationException("Payment mode not found."); Name = _item.Name; OnPropertyChanged(nameof(IsEdit)); } catch (Exception ex) { _errors.HandleError(ex); } }
    [RelayCommand] private async Task Save() { if (IsBusy) return; Name = Name.Trim(); if (string.IsNullOrWhiteSpace(Name)) { await Shell.Current.DisplayAlertAsync("Validation", "Payment mode name is required.", "OK"); return; } if (Name.Length > 225) { await Shell.Current.DisplayAlertAsync("Validation", "Payment mode name cannot exceed 225 characters.", "OK"); return; } IsBusy = true; try { if (await _repo.ExistsNameAsync(Name, _item.Id)) { await Shell.Current.DisplayAlertAsync("Validation", "A payment mode with this name already exists.", "OK"); return; } _item.Name = Name; await _repo.SaveItemAsync(_item); await Shell.Current.GoToAsync(".."); } catch (Exception ex) { _errors.HandleError(ex); } finally { IsBusy = false; } }
    [RelayCommand] private Task Cancel() => Shell.Current.GoToAsync("..");
}
