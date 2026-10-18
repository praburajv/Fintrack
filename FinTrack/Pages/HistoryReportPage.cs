using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Pages.Controls;
using FinTracker.Services;
using FinTracker.Utilities;
using Microsoft.Maui.Layouts;

namespace FinTracker.Pages;

public sealed class HistoryReportPage : ContentPage, IQueryAttributable
{
    private readonly HistoryReportService _history;
    private readonly ShopRepository _shops;
    private readonly CategoryRepository _categories;
    private readonly FinancialAccountContext _accounts;
    private readonly IHistoryDocumentService _documents;
    private readonly ModalErrorHandler _errors;
    private readonly SearchableSelector _selector = new();
    private readonly Picker _order = new() { Title = "Bill order", ItemsSource = new[] { "Date newest first", "Date oldest first", "Amount highest first", "Amount lowest first" }, SelectedIndex = 0 };
    private readonly VerticalStackLayout _results = new() { Spacing = 10 };
    private readonly FlexLayout _years = new() { Direction = FlexDirection.Row, Wrap = FlexWrap.Wrap, AlignItems = FlexAlignItems.Center };
    private readonly Label _summary = new() { LineBreakMode = LineBreakMode.WordWrap };
    private readonly Button _preview = new() { Text = "Preview report", IsEnabled = false };
    private readonly ProcessingOverlay _overlay = new() { ShowProgress = false };
    private readonly List<(int Year, CheckBox Check)> _yearChecks = [];
    private readonly VerticalStackLayout _body;
    private HistoryReport? _report;
    private CancellationTokenSource? _load;
    private FinancialTransactionType _type;
    private HistorySearchMode _mode;
    private int? _loadedAccount;
    private bool _loadingMasters, _subscribed;

    public HistoryReportPage(HistoryReportService history, ShopRepository shops, CategoryRepository categories,
        FinancialAccountContext accounts, IHistoryDocumentService documents, ModalErrorHandler errors)
    {
        _history = history; _shops = shops; _categories = categories; _accounts = accounts; _documents = documents; _errors = errors;
        this.SetDynamicResource(BackgroundColorProperty, "Background");
        var heading = new Label { FontSize = 24, FontAttributes = FontAttributes.Bold };
        heading.SetBinding(Label.TextProperty, new Binding(nameof(Title), source: this));
        var search = new Button { Text = "Search" };
        search.Clicked += async (_, _) => await SearchAsync();
        _preview.Clicked += async (_, _) =>
        {
            if (_report is not null && _report.AccountId == _accounts.Current?.Id && !_overlay.IsBusy)
                await Navigation.PushAsync(new HistoryPreviewPage(_report, _documents, _accounts, _errors));
        };
        _selector.SelectionChanged += (_, _) => { if (!_loadingMasters) Invalidate(); };
        _order.SelectedIndexChanged += (_, _) => Reorder();
        var buttons = new VerticalStackLayout { Spacing = 8, Children = { search, _preview } };
        _body = new VerticalStackLayout { Padding = 16, Spacing = 12, Children =
        {
            heading, _selector, new Label { Text = "Select years (current year and previous four)" },
            _years, _order, buttons, _summary, _results
        } };
        var root = new Grid { RowDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        root.Add(new ScrollView { Content = _body }); root.Add(new BottomTaskBar(), 0, 1);
        Grid.SetRowSpan(_overlay, 2); root.Add(_overlay); Content = root;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _type = query.TryGetValue("type", out var type) && Enum.TryParse(type?.ToString(), true, out FinancialTransactionType parsedType) ? parsedType : FinancialTransactionType.Expense;
        _mode = query.TryGetValue("mode", out var mode) && Enum.TryParse(mode?.ToString(), true, out HistorySearchMode parsedMode) ? parsedMode : HistorySearchMode.Category;
        Title = $"{_type} by {_mode}";
        _selector.Placeholder = $"Select a {_mode.ToString().ToLowerInvariant()}";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_subscribed) { _accounts.Changed += OnCompanyChanged; _subscribed = true; }
        EnsureYears();
        if (_loadedAccount != _accounts.Current?.Id || _selector.ItemsSource is null) await LoadMastersAsync();
    }

    protected override void OnDisappearing()
    {
        _load?.Cancel();
        if (_subscribed) { _accounts.Changed -= OnCompanyChanged; _subscribed = false; }
        base.OnDisappearing();
    }

    private void OnCompanyChanged(object? sender, EventArgs args) => MainThread.BeginInvokeOnMainThread(async () =>
    {
        Invalidate(); _selector.SelectedItem = null; _selector.ItemsSource = null;
        await LoadMastersAsync();
    });

    private void EnsureYears()
    {
        if (_yearChecks.Count > 0 && _yearChecks[0].Year == DateTime.Today.Year) return;
        _years.Children.Clear(); _yearChecks.Clear(); Invalidate();
        foreach (var year in Enumerable.Range(DateTime.Today.Year - 4, 5).Reverse())
        {
            var check = new CheckBox { IsChecked = year == DateTime.Today.Year };
            SemanticProperties.SetDescription(check, $"Include {year}");
            check.CheckedChanged += (_, _) => Invalidate();
            _yearChecks.Add((year, check));
            _years.Add(new HorizontalStackLayout { Margin = new Thickness(0, 0, 12, 6), Children = { check, new Label { Text = year.ToString(), VerticalOptions = LayoutOptions.Center } } });
        }
    }

    private async Task LoadMastersAsync()
    {
        Invalidate();
        var id = _accounts.Current?.Id;
        if (id is null) { _summary.Text = "Choose a company before searching."; return; }
        _loadingMasters = true; _overlay.IsBusy = true; _overlay.Message = "Loading available items...";
        try
        {
            var choices = _mode == HistorySearchMode.Shop ? (await _shops.ListAsync()).Cast<object>().ToArray() : (await _categories.ListAsync()).Cast<object>().ToArray();
            if (_accounts.Current?.Id != id) return;
            _selector.SelectedItem = null; _selector.ItemsSource = choices; _loadedAccount = id;
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { _loadingMasters = false; _overlay.IsBusy = false; }
    }

    private void Invalidate()
    {
        _load?.Cancel(); _report = null; _preview.IsEnabled = false; _results.Children.Clear(); _summary.Text = "Choose filters, then select Search.";
    }

    private async Task SearchAsync()
    {
        if (_overlay.IsBusy) return;
        var selected = _selector.SelectedItem;
        var id = selected switch { Shop shop => shop.Id, Category category => category.Id, _ => 0 };
        var years = _yearChecks.Where(x => x.Check.IsChecked).Select(x => x.Year).ToArray();
        if (id == 0 || years.Length == 0) { await ThemeDialog.AlertAsync("Search History", "Select an item and at least one year."); return; }
        _load?.Cancel(); _load?.Dispose(); var cancellation = _load = new CancellationTokenSource();
        _overlay.Message = "Searching history..."; _overlay.IsBusy = true;
        _report = null; _preview.IsEnabled = false; _results.Children.Clear();
        try
        {
            await Task.Yield();
            var report = await _history.SearchAsync(_type, _mode, id, SelectionSearch.Name(selected), years, (HistoryBillOrder)_order.SelectedIndex, cancellation.Token);
            if (cancellation.IsCancellationRequested || _accounts.Current?.Id != report.AccountId) return;
            _report = report; Render();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _summary.Text = "Search failed. Please try again."; _errors.HandleError(ex); }
        finally { if (ReferenceEquals(_load, cancellation)) _overlay.IsBusy = false; }
    }

    private void Reorder()
    {
        if (_report is null || _order.SelectedIndex < 0) return;
        static IEnumerable<HistoryBill> Rows(HistoryGroup group) => group.Bills.Concat(group.Children.SelectMany(Rows));
        var order = (HistoryBillOrder)_order.SelectedIndex;
        _report = _report with { Order = order, Groups = HistoryGrouping.Build(_report.Groups.SelectMany(Rows), _mode, order) };
        Render();
    }

    private void Render()
    {
        _results.Children.Clear();
        if (_report is null) return;
        _summary.Text = $"{_report.Company} · {string.Join(", ", _report.Years)}\n{_report.Count} bills · Total: {_report.TotalText}";
        _preview.IsEnabled = _report.Count > 0;
        if (_report.Count == 0) _results.Add(new Label { Text = "No matching transactions found." });
        foreach (var group in _report.Groups) _results.Add(HistoryViews.Group(group, _type));
    }
}

internal static class HistoryViews
{
    public static View Group(HistoryGroup group, FinancialTransactionType type)
    {
        var content = new VerticalStackLayout { Spacing = 6, IsVisible = false };
        var toggle = new Button { Text = "▸", FontSize = 18, WidthRequest = 44, MinimumHeightRequest = 44, Padding = 0, VerticalOptions = LayoutOptions.Center };
        SemanticProperties.SetDescription(toggle, $"Expand or collapse {group.Title}");
        var title = new Label { Text = group.Title, FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap };
        var total = new Label { Text = $"{EuroFormatter.Format(group.Total)} · {group.Count} bills", LineBreakMode = LineBreakMode.WordWrap };
        var header = new Grid { ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
        header.Add(toggle); header.Add(new VerticalStackLayout { Spacing = 3, Children = { title, total } }, 1);
        var populated = false;
        void Toggle()
        {
            if (!populated)
            {
                foreach (var child in group.Children) content.Add(Group(child, type));
                foreach (var bill in group.Bills) content.Add(Bill(bill, type));
                populated = true;
            }
            content.IsVisible = !content.IsVisible;
            toggle.Text = content.IsVisible ? "▾" : "▸";
        }
        toggle.Clicked += (_, _) => Toggle();
        foreach (var label in new[] { title, total })
        {
            var tap = new TapGestureRecognizer(); tap.Tapped += (_, _) => Toggle();
            label.GestureRecognizers.Add(tap);
        }
        return new Border { Padding = 10, Content = new VerticalStackLayout { Spacing = 6, Children = { header, content } } };
    }

    public static View Bill(HistoryBill bill, FinancialTransactionType type)
    {
        var details = new VerticalStackLayout { Spacing = 2, Children =
        {
            new Label { Text = bill.Date.ToString("dd/MM/yyyy") }, new Label { Text = bill.ShopName, FontAttributes = FontAttributes.Bold },
            new Label { Text = $"Reference: {bill.Reference}", IsVisible = bill.Reference is not null, FontSize = 12 },
            new Label { Text = bill.PaymentMode, FontSize = 12 }
        } };
        var grid = new Grid { Padding = new Thickness(6, 8), ColumnSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
        grid.Add(details); grid.Add(new Label { Text = EuroFormatter.Format(bill.Amount), VerticalOptions = LayoutOptions.Center,
            TextColor = Color.FromArgb(type == FinancialTransactionType.Income ? "#027A48" : "#B42318") }, 1);
        return grid;
    }
}
