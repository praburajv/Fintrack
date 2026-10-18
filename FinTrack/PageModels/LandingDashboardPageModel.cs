using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Services;

namespace FinTracker.PageModels;

public partial class LandingDashboardPageModel : ObservableObject
{
    private readonly DashboardSearchService search;
    private readonly FinancialAccountContext accounts;
    private readonly ModalErrorHandler errors;

    public LandingDashboardPageModel(DashboardSearchService search, FinancialAccountContext accounts, ModalErrorHandler errors)
    {
        this.search = search; this.accounts = accounts; this.errors = errors;
        // Shell pages may outlive a company selection. Do not retain results or in-flight requests.
        // The weak subscription also lets transient dashboard models be collected.
        var weak = new WeakReference<LandingDashboardPageModel>(this);
        EventHandler? changed = null;
        changed = (_, _) =>
        {
            if (weak.TryGetTarget(out var model)) model.ResetSearch();
            else accounts.Changed -= changed;
        };
        accounts.Changed += changed;
    }

    private readonly SemaphoreSlim _navigationGate = new(1, 1);
    private CancellationTokenSource? _searchCancellation;
    private int _searchVersion;
    private bool _applyingSelection;
    private int? _resultAccount;
    [ObservableProperty] private bool _hasMoreResults;

    public void ResetSearch()
    {
        _searchCancellation?.Cancel();
        Interlocked.Increment(ref _searchVersion);
        _applyingSelection = true;
        try { SearchText = string.Empty; SelectedShop = null; Suggestions = []; HasSearched = false; IsSearching = false; HasMoreResults = false; _resultAccount = null; }
        finally { _applyingSelection = false; }
    }

    private void EnsureAccount()
    {
        if (_resultAccount != accounts.Current?.Id) ResetSearch();
    }

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private DashboardSearchResult? _selectedShop;
    [ObservableProperty] private IReadOnlyList<DashboardSearchResult> _suggestions = [];
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
        SelectedShop = null;
        SearchAsync(value).FireAndForgetSafeAsync(errors);
        OpenAnalysisCommand.NotifyCanExecuteChanged();
    }

    partial void OnSuggestionsChanged(IReadOnlyList<DashboardSearchResult> value)
    {
        OnPropertyChanged(nameof(HasSuggestions));
        OnPropertyChanged(nameof(ShowNoMatches));
    }

    partial void OnIsSearchingChanged(bool value) => OnPropertyChanged(nameof(ShowNoMatches));
    partial void OnHasSearchedChanged(bool value) => OnPropertyChanged(nameof(ShowNoMatches));
    partial void OnSelectedShopChanged(DashboardSearchResult? value) { OnPropertyChanged(nameof(CanSearch)); OpenAnalysisCommand.NotifyCanExecuteChanged(); }
    partial void OnIsOpeningAnalysisChanged(bool value) { OnPropertyChanged(nameof(CanSearch)); OpenAnalysisCommand.NotifyCanExecuteChanged(); }

    [RelayCommand]
    private void SelectShop(DashboardSearchResult shop)
    {
        if (shop is null) return;
        if (_resultAccount != accounts.Current?.Id) { ResetSearch(); return; }
        _searchCancellation?.Cancel();
        Interlocked.Increment(ref _searchVersion);
        IsSearching = false;
        HasMoreResults = false;
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
        EnsureAccount();
        var shop = SelectedShop;
        if (shop is null || !await _navigationGate.WaitAsync(0)) return;
        try
        {
            IsOpeningAnalysis = true;
            ProcessingMessage = $"Opening {shop.Name} bill analysis...";
            await Task.Yield();
            if (_resultAccount != accounts.Current?.Id) { ResetSearch(); return; }
            var route = $"billanalysis?shopId={shop.ShopId}";
            if (shop.IsReference) route += $"&year={shop.Date.Year}&reference={Uri.EscapeDataString(shop.BillReferenceNo!)}";
            await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsOpeningAnalysis = false; _navigationGate.Release(); }
    }

    [RelayCommand]
    private Task LoadMore() => IsSearching || !HasMoreResults ? Task.CompletedTask : SearchAsync(SearchText, append: true);

    private async Task SearchAsync(string text, bool append = false)
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        var token = _searchCancellation.Token;
        var version = Interlocked.Increment(ref _searchVersion);
        var prefix = text.Trim();
        var account = accounts.Current?.Id;
        var offset = append ? Suggestions.Count : 0;
        if (!append) { Suggestions = []; HasMoreResults = false; }
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
            var matches = await search.SearchAsync(prefix, offset, token);
            if (account != accounts.Current?.Id || token.IsCancellationRequested || version != _searchVersion || !string.Equals(prefix, SearchText.Trim(), StringComparison.Ordinal)) return;
            _resultAccount = account;
            HasMoreResults = matches.Count > DashboardSearchService.PageSize;
            var page = matches.Take(DashboardSearchService.PageSize);
            Suggestions = append ? Suggestions.Concat(page).ToArray() : page.ToArray();
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
