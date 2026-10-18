using FinTracker.Data;
using FinTracker.Pages.Controls;
using FinTracker.Services;
using FinTracker.Utilities;

namespace FinTracker.Pages;

/// <summary>Account-aware entry point to the reports already implemented by the app.</summary>
public sealed class ReportsHubPage : ContentPage
{
    private readonly BillTransactionRepository _bills;
    private readonly IncomeTransactionRepository _income;
    private readonly FinancialAccountContext _accounts;
    private readonly Label _company = new();
    private readonly Label _incomeTotal = new();
    private readonly Label _expenseTotal = new();
    private readonly Label _netTotal = new();
    private readonly Grid _chart = new() { ColumnSpacing = 4, HeightRequest = 95 };
    private readonly ProcessingOverlay _overlay = new() { Message = "Loading company reports...", ShowProgress = false };
    private readonly Picker _period = new() { Title = "Period", ItemsSource = new[] { "1M", "L 3M", "This Year" }, SelectedIndex = 0 };
    private int _loadVersion;
    private bool _navigating;

    public ReportsHubPage(BillTransactionRepository bills, IncomeTransactionRepository income, FinancialAccountContext accounts)
    {
        _bills = bills; _income = income; _accounts = accounts;
        Title = "Reports";
        this.SetDynamicResource(BackgroundColorProperty, "Background");
        var heading = new Label { Text = "Reports", FontSize = 31, FontAttributes = FontAttributes.Bold };
        heading.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        _company.SetDynamicResource(Label.TextColorProperty, "Primary");
        _period.SelectedIndexChanged += async (_, _) => await LoadAsync();
        var filters = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(165)) }, ColumnSpacing = 10 };
        filters.Add(_company, 0); filters.Add(_period, 1);
        var summary = new VerticalStackLayout { Spacing = 12 };
        var totals = new Grid { ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }, ColumnSpacing = 6 };
        totals.Add(Metric("Total Income", _incomeTotal, "#00A769"), 0);
        totals.Add(Metric("Total Expenses", _expenseTotal, "#EC3F4A"), 1);
        totals.Add(Metric("Net Balance", _netTotal, "#0878EB"), 2);
        summary.Children.Add(totals);
        summary.Children.Add(_chart);
        var summaryCard = Card(summary);
        var stack = new VerticalStackLayout { Padding = new Thickness(16, 20), Spacing = 18,
            Children = { heading, filters, summaryCard,
                Section("Financial Reports", ("Expense Report", "expensereport", "#0878EB"), ("Income Report", "//incomeanalysis", "#00A769"),
                    ("Expense Statement", "expensereport", "#8644E7"), ("Budget vs Actual", "monthlybudget", "#FF9F0A")),
                Section("Analysis", ("Expense by Category", "expenseanalysis", "#00A769"), ("Expense by Shop", "expenseanalysis", "#FF9F0A"),
                    ("Monthly Trend", "//incomeflow", "#0878EB"), ("Bill Analysis", "//main", "#EC3F4A")),
                SearchHistorySection(),
                Section("Export", ("View PDF", "expensereport", "#EC3F4A"), ("Share PDF", "expensereport", "#8644E7")) } };
        var root = new Grid { RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) } };
        root.Add(new ScrollView { Content = stack }, 0, 0);
        root.Add(new BottomTaskBar(), 0, 1);
        Grid.SetRowSpan(_overlay, 2); root.Add(_overlay);
        Content = root;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _company.Text = _accounts.Current?.Name ?? "Choose a company";
        await LoadAsync();
    }

    protected override void OnDisappearing()
    {
        Interlocked.Increment(ref _loadVersion);
        base.OnDisappearing();
    }

    private async Task LoadAsync()
    {
        var account = _accounts.Current;
        if (account is null) return;
        var version = Interlocked.Increment(ref _loadVersion);
        _overlay.IsBusy = true;
        try
        {
            var period = _period.SelectedIndex switch { 1 => FinancialDashboardPeriod.LastThreeMonths, 2 => FinancialDashboardPeriod.ThisYear, _ => FinancialDashboardPeriod.Month };
            var range = FinancialDashboardDates.For(period, DateTime.Today);
            var expenseTask = _bills.GetBillsForDateRangeAsync(range.Start, range.EndExclusive);
            var incomeTask = _income.ListAsync(range.Start, range.EndExclusive);
            await Task.WhenAll(expenseTask, incomeTask);
            if (version != _loadVersion || _accounts.Current?.Id != account.Id) return;
            var expenses = expenseTask.Result;
            var incomes = incomeTask.Result;
            var expenseAmount = expenses.Sum(x => x.BillValue);
            var incomeAmount = incomes.Sum(x => x.Amount);
            _incomeTotal.Text = EuroFormatter.Format(incomeAmount);
            _expenseTotal.Text = EuroFormatter.Format(expenseAmount);
            _netTotal.Text = EuroFormatter.Format(incomeAmount - expenseAmount);
            _company.Text = account.Name;
            DrawChart(expenses.Select(x => (x.BillDate, x.BillValue)), incomes.Select(x => (x.TransactionDate, x.Amount)), range);
        }
        catch (Exception ex)
        {
            if (version == _loadVersion) await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Reports", $"Reports could not be loaded: {ex.Message}", "OK");
        }
        finally { if (version == _loadVersion) _overlay.IsBusy = false; }
    }

    private void DrawChart(IEnumerable<(DateTime Date, decimal Amount)> expenses, IEnumerable<(DateTime Date, decimal Amount)> incomes, FinancialDashboardRange range)
    {
        _chart.Children.Clear(); _chart.ColumnDefinitions.Clear();
        var groups = 8;
        var days = Math.Max(1, (range.EndExclusive - range.Start).TotalDays);
        int Bucket(DateTime date) => Math.Clamp((int)((date - range.Start).TotalDays / days * groups), 0, groups - 1);
        var expenseTotals = expenses.GroupBy(x => Bucket(x.Date)).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var incomeTotals = incomes.GroupBy(x => Bucket(x.Date)).ToDictionary(x => x.Key, x => x.Sum(y => y.Amount));
        var max = Math.Max(1m, expenseTotals.Values.Concat(incomeTotals.Values).DefaultIfEmpty(0).Max());
        for (var index = 0; index < groups; index++)
        {
            _chart.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            var bars = new HorizontalStackLayout { Spacing = 2, VerticalOptions = LayoutOptions.End, HorizontalOptions = LayoutOptions.Center };
            bars.Children.Add(new BoxView { WidthRequest = 10, HeightRequest = 4 + 82 * (double)(incomeTotals.GetValueOrDefault(index) / max), Color = Color.FromArgb("#34C995"), VerticalOptions = LayoutOptions.End });
            bars.Children.Add(new BoxView { WidthRequest = 10, HeightRequest = 4 + 82 * (double)(expenseTotals.GetValueOrDefault(index) / max), Color = Color.FromArgb("#FF8791"), VerticalOptions = LayoutOptions.End });
            _chart.Add(bars, index);
        }
    }

    private static View Metric(string title, Label value, string color)
    {
        var caption = new Label { Text = title, FontSize = 11, MaxLines = 2, LineBreakMode = LineBreakMode.WordWrap, TextColor = Color.FromArgb(color) };
        value.FontSize = 15; value.FontAttributes = FontAttributes.Bold; value.Text = "—"; value.MaxLines = 1; value.LineBreakMode = LineBreakMode.TailTruncation;
        value.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        return new VerticalStackLayout { Spacing = 5, Children = { caption, value } };
    }

    private View Section(string title, params (string Label, string Route, string Accent)[] links)
    {
        var label = new Label { Text = title, FontSize = 22, FontAttributes = FontAttributes.Bold };
        label.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        var grid = new Grid { ColumnSpacing = 8, RowSpacing = 9, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) } };
        for (var index = 0; index < links.Length; index++)
        {
            if (index % 2 == 0) grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            var link = links[index];
            var button = new Button { Text = link.Label + "  ›", FontSize = 13, Padding = new Thickness(10, 14), BackgroundColor = Color.FromArgb(link.Accent).WithAlpha(0.12f), TextColor = Color.FromArgb(link.Accent), CornerRadius = 14 };
            button.Clicked += async (_, _) => await OpenAsync(link.Route);
            grid.Add(button, index % 2, index / 2);
        }
        return Card(new VerticalStackLayout { Spacing = 12, Children = { label, grid } });
    }

    private static Border Card(View content)
    {
        var card = new Border { Content = content, Padding = new Thickness(14), StrokeThickness = 0,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 20 } };
        card.SetDynamicResource(Border.BackgroundColorProperty, "Surface");
        return card;
    }

    private View SearchHistorySection()
    {
        var grid = new Grid { ColumnSpacing = 10, RowSpacing = 8, ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) },
            RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) } };
        foreach (var (type, column, accent) in new[] { ("Expense", 0, "#B42318"), ("Income", 1, "#027A48") })
        {
            grid.Add(new Label { Text = type + " by", TextColor = Color.FromArgb(accent), FontAttributes = FontAttributes.Bold }, column, 0);
            foreach (var (mode, row) in new[] { ("Category", 1), ("Shop", 2) })
            {
                var action = new Button { Text = mode + "  ›", FontSize = 14, TextColor = Color.FromArgb(accent), BackgroundColor = Color.FromArgb(accent).WithAlpha(0.10f) };
                action.Clicked += async (_, _) => await OpenAsync($"history?type={type}&mode={mode}");
                grid.Add(action, column, row);
            }
        }
        return Card(new VerticalStackLayout { Spacing = 12, Children = { new Label { Text = "Search History", FontSize = 22, FontAttributes = FontAttributes.Bold }, grid } });
    }

    private async Task OpenAsync(string route)
    {
        if (_navigating || _overlay.IsBusy || _accounts.Current is null) return;
        _navigating = true; _overlay.Message = "Opening report..."; _overlay.IsBusy = true;
        try
        {
            if (route == "//main")
            {
                await Shell.Current.GoToAsync(route);
                if (Shell.Current.CurrentPage is FinancialDashboardPage page) page.ToggleSearch();
            }
            else if (route == "expensereport") await Shell.Current.GoToAsync($"expensereport?reference={DateTime.Today:yyyy-MM-dd}&period=Monthly");
            else if (route == "monthlybudget") await Shell.Current.GoToAsync($"monthlybudget?reference={DateTime.Today:yyyy-MM-dd}");
            else if (route == "expenseanalysis") await Shell.Current.GoToAsync($"expenseanalysis?reference={DateTime.Today:yyyy-MM-dd}&period=Monthly");
            else await Shell.Current.GoToAsync(route);
        }
        catch (Exception ex) { await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Reports", $"This report could not be opened: {ex.Message}", "OK"); }
        finally { _overlay.IsBusy = false; _navigating = false; }
    }
}
