using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;

namespace FinTracker.PageModels;

public partial class EtlTextMapPageModel : ObservableObject, IQueryAttributable
{
    private readonly EtlTextMapRepository _repository;
    private readonly ShopRepository _shopsRepository;
    private readonly ModalErrorHandler _errors;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private bool _isSaving;
    private EtlTextMap _editing = new();
    private int? _queryId;

    [ObservableProperty] private List<EtlTextMap> _items = [];
    [ObservableProperty] private List<Shop> _shops = [];
    [ObservableProperty] private string _lookupText = string.Empty;
    [ObservableProperty] private Shop? _selectedShop;
    [ObservableProperty] private bool _isBusy = true;

    public bool IsEdit => _editing.Id > 0;

    public EtlTextMapPageModel(EtlTextMapRepository repository, ShopRepository shopsRepository, ModalErrorHandler errors)
    {
        _repository = repository;
        _shopsRepository = shopsRepository;
        _errors = errors;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("id", out var value) && int.TryParse(value?.ToString(), out var id))
            _queryId = id;
        else
            _queryId = null;
    }

    [RelayCommand]
    private async Task Appearing()
    {
        await LoadAsync(_queryId);
    }

    private async Task LoadAsync(int? id = null)
    {
        if (!await _loadGate.WaitAsync(0)) return;
        IsBusy = true;
        try
        {
            await Task.Yield();
            Shops = await _shopsRepository.ListAsync();
            Items = await _repository.ListAsync();
            if (id.HasValue)
            {
                _editing = await _repository.GetAsync(id.Value) ?? throw new InvalidOperationException("The ETL mapping was not found.");
                LookupText = _editing.LookupText;
                SelectedShop = Shops.FirstOrDefault(x => x.Id == _editing.ShopId);
            }
            else ResetForm();
            OnPropertyChanged(nameof(IsEdit));
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = _isSaving; _loadGate.Release(); }
    }

    [RelayCommand]
    private async Task Save()
    {
        if (IsBusy) return;
        IsBusy = true;
        _isSaving = true;
        try
        {
            LookupText = LookupText.Trim();
            if (string.IsNullOrWhiteSpace(LookupText) || SelectedShop is null)
            {
                await Shell.Current.DisplayAlertAsync("Validation", "Lookup text and Shop are required.", "OK");
                return;
            }
            if (LookupText.Length > 225)
            {
                await Shell.Current.DisplayAlertAsync("Validation", "Lookup text cannot exceed 225 characters.", "OK");
                return;
            }
            if (await _repository.ExistsLookupTextAsync(LookupText, _editing.Id))
            {
                await Shell.Current.DisplayAlertAsync("Validation", "This lookup text is already mapped to a Shop.", "OK");
                return;
            }
            _editing.LookupText = LookupText;
            _editing.ShopId = SelectedShop.Id;
            _editing.ShopName = SelectedShop.Name;
            await _repository.SaveItemAsync(_editing);
            _queryId = null;
            await LoadAsync();
            await Shell.Current.DisplayAlertAsync("ETL DATA MAPPING", "Mapping saved.", "OK");
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { _isSaving = false; IsBusy = false; }
    }

    [RelayCommand]
    private async Task Edit(EtlTextMap item)
    {
        if (IsBusy) return;
        IsBusy = true;
        try { await Shell.Current.GoToAsync($"etldatamapping?id={item.Id}"); }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Delete(EtlTextMap item)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            if (!await Shell.Current.DisplayAlertAsync("Delete mapping?", $"{item.LookupText} → {item.ShopName}", "Delete", "Cancel")) return;
            await _repository.DeleteItemAsync(item);
            Items = await _repository.ListAsync();
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task Cancel() => Shell.Current.GoToAsync("..");

    private void ResetForm()
    {
        _editing = new EtlTextMap();
        LookupText = string.Empty;
        SelectedShop = null;
    }
}
