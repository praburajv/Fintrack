using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Data;
using MauiApp1.Models;

namespace MauiApp1.PageModels;

public partial class LandingDashboardPageModel(ShopRepository shops, ModalErrorHandler errors) : ObservableObject
{
    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private CancellationTokenSource? _searchCancellation;
    private int _searchVersion;
    private bool _applyingSelection;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private Shop? _selectedShop;
    [ObservableProperty] private IReadOnlyList<Shop> _suggestions = [];
    [ObservableProperty] private bool _isSearching;
    [ObservableProperty] private bool _hasSearched;
    [ObservableProperty] private bool _isOpeningAnalysis;
    [ObservableProperty] private string _processingMessage = "Opening bill analysis...";

    public bool HasSuggestions => Suggestions.Count > 0;
    public bool ShowNoMatches => HasSearched && !IsSearching && SearchText.Trim().Length > 0 && !HasSuggestions;
    public bool CanSearch => SelectedShop is not null && !IsOpeningAnalysis;

    partial void OnSearchTextChanged(string value)
    {
        if (_applyingSelection) return;
        if (SelectedShop is not null && !string.Equals(value.Trim(), SelectedShop.Name, StringComparison.OrdinalIgnoreCase))
            SelectedShop = null;
        SearchAsync(value).FireAndForgetSafeAsync(errors);
        OpenAnalysisCommand.NotifyCanExecuteChanged();
    }

    partial void OnSuggestionsChanged(IReadOnlyList<Shop> value)
    {
        OnPropertyChanged(nameof(HasSuggestions));
        OnPropertyChanged(nameof(ShowNoMatches));
    }

    partial void OnIsSearchingChanged(bool value) => OnPropertyChanged(nameof(ShowNoMatches));
    partial void OnHasSearchedChanged(bool value) => OnPropertyChanged(nameof(ShowNoMatches));
    partial void OnSelectedShopChanged(Shop? value) { OnPropertyChanged(nameof(CanSearch)); OpenAnalysisCommand.NotifyCanExecuteChanged(); }
    partial void OnIsOpeningAnalysisChanged(bool value) { OnPropertyChanged(nameof(CanSearch)); OpenAnalysisCommand.NotifyCanExecuteChanged(); }

    [RelayCommand]
    private void SelectShop(Shop shop)
    {
        if (shop is null) return;
        _searchCancellation?.Cancel();
        _applyingSelection = true;
        try
        {
            SelectedShop = shop;
            SearchText = shop.Name;
            Suggestions = [];
            HasSearched = false;
        }
        finally { _applyingSelection = false; }
    }

    [RelayCommand(CanExecute = nameof(CanSearch))]
    private async Task OpenAnalysis()
    {
        var shop = SelectedShop;
        if (shop is null || !await _navigationGate.WaitAsync(0)) return;
        try
        {
            IsOpeningAnalysis = true;
            ProcessingMessage = $"Opening {shop.Name} bill analysis...";
            await Task.Yield();
            await Shell.Current.GoToAsync($"billanalysis?shopId={shop.Id}");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsOpeningAnalysis = false; _navigationGate.Release(); }
    }

    private async Task SearchAsync(string text)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        var token = _searchCancellation.Token;
        var version = Interlocked.Increment(ref _searchVersion);
        var prefix = text.Trim();
        if (prefix.Length == 0)
        {
            Suggestions = [];
            HasSearched = false;
            IsSearching = false;
            return;
        }

        try
        {
            IsSearching = true;
            HasSearched = false;
            await Task.Delay(275, token);
            var matches = await shops.SearchByPrefixAsync(prefix, 8, token);
            if (token.IsCancellationRequested || version != _searchVersion || !string.Equals(prefix, SearchText.Trim(), StringComparison.Ordinal)) return;
            Suggestions = matches;
            HasSearched = true;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (version == _searchVersion) errors.HandleError(ex);
        }
        finally
        {
            if (version == _searchVersion) IsSearching = false;
        }
    }
}
