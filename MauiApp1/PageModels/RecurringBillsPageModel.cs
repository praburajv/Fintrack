using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Data;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Utilities;
using System.Collections.ObjectModel;

namespace MauiApp1.PageModels;

public partial class RecurringBillsPageModel(RecurringBillRepository repository, MonthlyRecurringBillRegistrationService registration, ModalErrorHandler errors) : ObservableObject
{
    [ObservableProperty] private ObservableCollection<RecurringBill> _items = [];
    [ObservableProperty] private DateTime _targetMonth = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private bool _isBusy;
    public string TargetMonthText => TargetMonth.ToString("MMMM yyyy");
    public bool HasItems => Items.Count > 0;
    public string RegistrationButtonText => "Register " + TargetMonth.ToString("MMMM yyyy") + " Bills";

    [RelayCommand] private async Task Appearing() { if (!IsBusy) await LoadAsync(); }
    [RelayCommand] private Task Add() => Shell.Current.GoToAsync("recurringbill");
    [RelayCommand] private Task Edit(RecurringBill item) => Shell.Current.GoToAsync($"recurringbill?id={item.Id}");
    [RelayCommand] private async Task Toggle(RecurringBill item) { try { await repository.ToggleAsync(item); await LoadAsync(); } catch (Exception ex) { errors.HandleError(ex); } }
    [RelayCommand] private async Task Delete(RecurringBill item) { if (!await Shell.Current.DisplayAlertAsync("Delete recurring bill?", item.ShopName, "Delete", "Cancel")) return; try { await repository.DeleteAsync(item.Id); await LoadAsync(); } catch (Exception ex) { await Shell.Current.DisplayAlertAsync("Cannot delete", ex.Message, "OK"); } }

    [RelayCommand]
    private async Task Register()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true; var result = await registration.RegisterMonthlyRecurringBillsAsync(TargetMonth);
            var message = result.Items.Count == 0 ? "No active recurring bills apply to this month." : $"{result.CreatedCount} new bill(s) registered.\n{result.ExistingCount} already registered.\nTotal: {EuroFormatter.Format(result.CreatedTotal)}";
            await Shell.Current.DisplayAlertAsync("Registration complete", message, "OK");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    private async Task LoadAsync() { try { IsBusy = true; Items = new ObservableCollection<RecurringBill>(await repository.ListAsync()); OnPropertyChanged(nameof(HasItems)); OnPropertyChanged(nameof(TargetMonthText)); OnPropertyChanged(nameof(RegistrationButtonText)); } catch (Exception ex) { errors.HandleError(ex); } finally { IsBusy = false; } }
}
