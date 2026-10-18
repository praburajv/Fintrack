using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Services;
using FinTracker.Utilities;

namespace FinTracker.PageModels;

public sealed record DashboardMiniBar(double Height);

public sealed class DashboardCategoryRow
{
    public required int Id { get; init; }
    public required string Name { get; init; }
    public required string Icon { get; init; }
    public required string Amount { get; init; }
    public required string Percent { get; init; }
    public required double Fraction { get; init; }
    public required Color Color { get; init; }
}

public sealed class DashboardTransactionRow
{
    public required int Id { get; init; }
    public required FinancialTransactionType Type { get; init; }
    public required DateTime Date { get; init; }
    public required string Name { get; init; }
    public required string Category { get; init; }
    public required string Icon { get; init; }
    public required string Amount { get; init; }
    public bool IsIncome => Type == FinancialTransactionType.Income;
    public Color AmountColor => IsIncome ? Color.FromArgb("#027A48") : Color.FromArgb("#B42318");
    public string DateText => Date.ToString("dd MMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
}

public partial class FinancialDashboardPageModel(
    BillTransactionRepository bills,
    IncomeTransactionRepository income,
    FinancialAccountRepository companies,
    FinancialAccountContext accounts,
    ICriticalOperationState criticalOperations,
    IAuthenticationService authentication,
    LandingDashboardPageModel search,
    ModalErrorHandler errors) : ObservableObject
{
    private const int PreviewCount = 5;
    private int _loadVersion;
    private bool _active;
    private bool _showAllCategories;
    private bool _showAllTransactions;
    private bool _restoringPeriod;
    private string _loadedPeriod = "1M";
    private IReadOnlyList<DashboardCategoryRow> _allCategories = [];
    private IReadOnlyList<DashboardTransactionRow> _allTransactions = [];
    private FinancialDashboardRange _range = FinancialDashboardDates.For(FinancialDashboardPeriod.Month, DateTime.Today);
    private IReadOnlyList<FinancialAccount> _companyList = [];
    private int _companyWindow;

    public LandingDashboardPageModel Search { get; } = search;
    public IReadOnlyList<string> PeriodOptions { get; } = ["1M", "L 3M", "This Year"];
    [ObservableProperty] private string _selectedPeriod = "1M";
    [ObservableProperty] private bool _isSearchVisible;
    [ObservableProperty] private bool _isOpeningLink;
    [ObservableProperty] private string _processingMessage = "Loading dashboard...";
    public bool IsProcessing => IsBusy || IsOpeningLink;
    [ObservableProperty] private string _greeting = "Good morning";
    [ObservableProperty] private string _userName = string.Empty;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _firstCompanyName = string.Empty;
    [ObservableProperty] private string _secondCompanyName = string.Empty;
    [ObservableProperty] private bool _showFirstCompany;
    [ObservableProperty] private bool _showSecondCompany;
    [ObservableProperty] private bool _canMoveCompaniesBack;
    [ObservableProperty] private bool _canMoveCompaniesForward;
    [ObservableProperty] private Color _firstCompanyColor = Colors.Transparent;
    [ObservableProperty] private Color _secondCompanyColor = Colors.Transparent;
    [ObservableProperty] private string _periodCaption = string.Empty;
    [ObservableProperty] private string _comparisonCaption = "vs last month";
    [ObservableProperty] private string _incomeAmount = EuroFormatter.Format(0);
    [ObservableProperty] private string _expenseAmount = EuroFormatter.Format(0);
    [ObservableProperty] private string _categoryTotal = EuroFormatter.Format(0);
    [ObservableProperty] private string _incomeChange = "No prior data";
    [ObservableProperty] private string _expenseChange = "No prior data";
    [ObservableProperty] private IReadOnlyList<DashboardMiniBar> _incomeBars = [];
    [ObservableProperty] private IReadOnlyList<DashboardMiniBar> _expenseBars = [];
    [ObservableProperty] private IReadOnlyList<CategoryExpenseChartData> _categoryChart = [];
    [ObservableProperty] private IReadOnlyList<DashboardCategoryRow> _visibleCategories = [];
    [ObservableProperty] private IReadOnlyList<DashboardTransactionRow> _visibleTransactions = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasCategories;
    [ObservableProperty] private bool _hasTransactions;
    [ObservableProperty] private bool _canExpandCategories;
    [ObservableProperty] private bool _canExpandTransactions;
    [ObservableProperty] private string _categoryActionText = "View All";
    [ObservableProperty] private string _transactionActionText = "View All";

    partial void OnSelectedPeriodChanged(string value)
    {
        if (_restoringPeriod) return;
        _showAllCategories = false;
        _showAllTransactions = false;
        CategoryActionText = "View All";
        TransactionActionText = "View All";
        if (_active) _ = ReloadAsync(minimumVisible: true);
    }

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsProcessing));
    partial void OnIsOpeningLinkChanged(bool value) => OnPropertyChanged(nameof(IsProcessing));

    public async Task OnNavigatedToAsync()
    {
        _active = true;
        var now = DateTime.Now;
        Greeting = FinancialDashboardDates.Greeting(now);
        UserName = authentication.CurrentUser?.Name ?? string.Empty;
        CompanyName = accounts.Current?.Name ?? string.Empty;
        _companyList = await companies.ListAsync();
        var index = _companyList.ToList().FindIndex(x => x.Id == accounts.RequireId());
        _companyWindow = Math.Max(0, index / 2 * 2);
        UpdateCompanyWindow();
        await ReloadAsync();
    }

    private void UpdateCompanyWindow()
    {
        var first = _companyList.ElementAtOrDefault(_companyWindow);
        var second = _companyList.ElementAtOrDefault(_companyWindow + 1);
        ShowFirstCompany = first is not null;
        ShowSecondCompany = second is not null;
        FirstCompanyName = first?.Name ?? string.Empty;
        SecondCompanyName = second?.Name ?? string.Empty;
        FirstCompanyColor = first?.Id == accounts.Current?.Id ? Color.FromArgb("#0878EB") : Colors.Transparent;
        SecondCompanyColor = second?.Id == accounts.Current?.Id ? Color.FromArgb("#0878EB") : Colors.Transparent;
        CanMoveCompaniesBack = _companyWindow > 0;
        CanMoveCompaniesForward = _companyWindow + 2 < _companyList.Count;
    }

    [RelayCommand]
    private void PreviousCompanies()
    {
        if (IsProcessing || _companyWindow == 0) return;
        _companyWindow = Math.Max(0, _companyWindow - 2);
        UpdateCompanyWindow();
    }

    [RelayCommand]
    private void NextCompanies()
    {
        if (IsProcessing || _companyWindow + 2 >= _companyList.Count) return;
        _companyWindow += 2;
        UpdateCompanyWindow();
    }

    [RelayCommand]
    private Task SelectFirstCompany() => SelectCompany(0);

    [RelayCommand]
    private Task SelectSecondCompany() => SelectCompany(1);

    private async Task SelectCompany(int slot)
    {
        if (IsProcessing || criticalOperations.IsBusy) return;
        var company = _companyList.ElementAtOrDefault(_companyWindow + slot);
        if (company is null || company.Id == accounts.Current?.Id) return;
        var fresh = await companies.GetAsync(company.Id);
        if (fresh is null || !fresh.IsActive) return;
        Interlocked.Increment(ref _loadVersion);
        Search.ResetSearch();
        accounts.Select(fresh);
        CompanyName = fresh.Name;
        UpdateCompanyWindow();
        await ReloadAsync(minimumVisible: true);
    }

    public void OnNavigatedAway()
    {
        _active = false;
        IsSearchVisible = false;
        Search.ResetSearch();
        Interlocked.Increment(ref _loadVersion);
        IsBusy = false;
    }

    [RelayCommand]
    private async Task ToggleCategories()
    {
        if (!_active || IsProcessing) return;
        var version = _loadVersion;
        IsBusy = true;
        ProcessingMessage = _showAllCategories ? "Showing fewer categories..." : $"Loading categories for {SelectedPeriod}...";
        try
        {
            await Task.Yield();
            await Task.Delay(250);
            if (!_active || version != _loadVersion) return;
            _showAllCategories = !_showAllCategories;
            VisibleCategories = _showAllCategories ? _allCategories : _allCategories.Take(PreviewCount).ToArray();
            CategoryActionText = _showAllCategories ? "Show Less" : "View All";
        }
        finally { if (version == _loadVersion) IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleTransactions()
    {
        if (!_active || IsProcessing) return;
        var version = _loadVersion;
        IsBusy = true;
        ProcessingMessage = _showAllTransactions ? "Showing fewer transactions..." : $"Loading transactions for {SelectedPeriod}...";
        try
        {
            await Task.Yield();
            await Task.Delay(250);
            if (!_active || version != _loadVersion) return;
            _showAllTransactions = !_showAllTransactions;
            VisibleTransactions = _showAllTransactions ? _allTransactions : _allTransactions.Take(PreviewCount).ToArray();
            TransactionActionText = _showAllTransactions ? "Show Less" : "View All";
        }
        finally { if (version == _loadVersion) IsBusy = false; }
    }

    public async Task OpenCategoryAsync(int categoryId, string categoryName)
    {
        var name = Uri.EscapeDataString(categoryName);
        await NavigateAsync($"dailybills?from={_range.Start:yyyy-MM-dd}&to={_range.EndExclusive:yyyy-MM-dd}&categoryId={categoryId}&categoryName={name}");
    }

    [ObservableProperty] private IReadOnlyList<CategoryExpenseChartData> _incomeCategoryChart = [];
    [ObservableProperty] private IReadOnlyList<DashboardCategoryRow> _incomeCategoryRows = [];
    public bool HasIncomeCategories => IncomeCategoryRows.Count > 0;
    partial void OnIncomeCategoryRowsChanged(IReadOnlyList<DashboardCategoryRow> value) => OnPropertyChanged(nameof(HasIncomeCategories));

    public Task OpenIncomeCategoryAsync(int categoryId, string categoryName)
        => NavigateAsync($"dailybills?from={_range.Start:yyyy-MM-dd}&to={_range.EndExclusive:yyyy-MM-dd}&categoryId={categoryId}&categoryName={Uri.EscapeDataString(categoryName)}&type=Income");

    public Task OpenTransactionAsync(DashboardTransactionRow row)
        => NavigateAsync($"bill?id={row.Id}&type={row.Type}");

    private static async Task NavigateAsync(string route)
    {
        if (Shell.Current is not null) await Shell.Current.GoToAsync(route);
    }

    private async Task ReloadAsync(bool minimumVisible = false)
    {
        if (!_active || !authentication.IsAuthenticated || accounts.Current is null) return;
        var version = Interlocked.Increment(ref _loadVersion);
        var started = Environment.TickCount64;
        ProcessingMessage = $"Loading {SelectedPeriod} dashboard...";
        IsBusy = true;
        try
        {
            var period = SelectedPeriod switch
            {
                "L 3M" => FinancialDashboardPeriod.LastThreeMonths,
                "This Year" => FinancialDashboardPeriod.ThisYear,
                _ => FinancialDashboardPeriod.Month
            };
            var range = FinancialDashboardDates.For(period, DateTime.Today);
            var expenseTask = bills.GetBillsForDateRangeAsync(range.Start, range.EndExclusive);
            var incomeTask = income.ListAsync(range.Start, range.EndExclusive);
            var previousExpenseTask = bills.GetMonthlyTotalsAsync(range.PreviousStart, range.PreviousEndExclusive);
            var previousIncomeTask = income.GetMonthlyTotalsAsync(range.PreviousStart, range.PreviousEndExclusive);
            await Task.WhenAll(expenseTask, incomeTask, previousExpenseTask, previousIncomeTask);
            var remaining = 250 - (Environment.TickCount64 - started);
            if (minimumVisible && remaining > 0) await Task.Delay((int)remaining);
            if (!_active || version != _loadVersion) return;

            var expenseRows = expenseTask.Result;
            var incomeRows = incomeTask.Result;
            var expenseTotal = expenseRows.Sum(x => x.BillValue);
            var incomeTotal = incomeRows.Sum(x => x.Amount);
            IncomeAmount = EuroFormatter.Format(incomeTotal);
            ExpenseAmount = EuroFormatter.Format(expenseTotal);
            CategoryTotal = EuroFormatter.Format(expenseTotal);
            IncomeChange = FinancialDashboardDates.Change(incomeTotal, previousIncomeTask.Result.Values.Sum());
            ExpenseChange = FinancialDashboardDates.Change(expenseTotal, previousExpenseTask.Result.Values.Sum());
            IncomeBars = MakeBars(incomeRows.Select(x => (x.TransactionDate, x.Amount)), range);
            ExpenseBars = MakeBars(expenseRows.Select(x => (x.BillDate, x.BillValue)), range);
            PeriodCaption = period switch
            {
                FinancialDashboardPeriod.Month => range.Start.ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture),
                FinancialDashboardPeriod.LastThreeMonths => $"{range.Start:MMM}–{range.EndExclusive.AddMonths(-1):MMM yyyy}",
                _ => $"{range.Start:yyyy} to date"
            };
            ComparisonCaption = period switch
            {
                FinancialDashboardPeriod.Month => "vs last month",
                FinancialDashboardPeriod.LastThreeMonths => "vs previous 3 months",
                _ => "vs last year to date"
            };

            var categories = expenseRows.GroupBy(x => x.ShopCategoryId)
                .Select(g =>
                {
                    var first = g.First();
                    var name = string.IsNullOrWhiteSpace(first.ShopCategoryName) ? "Uncategorized" : first.ShopCategoryName;
                    var amount = g.Sum(x => x.BillValue);
                    var color = (ChartColorPalette.ForShop(name) as SolidColorBrush)?.Color ?? Colors.Blue;
                    return new { Id = g.Key, Name = name, Icon = CategoryIconCatalog.FileNameFor(first.ShopCategoryIconKey), Amount = amount, Color = color };
                })
                .OrderByDescending(x => x.Amount).ThenBy(x => x.Name)
                .ToArray();
            CategoryChart = categories.Select(x => new CategoryExpenseChartData
            {
                CategoryId = x.Id, CategoryName = x.Name, TotalValue = x.Amount, Color = new SolidColorBrush(x.Color)
            }).ToArray();
            _allCategories = categories.Select(x => new DashboardCategoryRow
            {
                Id = x.Id, Name = x.Name, Icon = x.Icon, Amount = EuroFormatter.Format(x.Amount),
                Fraction = expenseTotal > 0 ? (double)(x.Amount / expenseTotal) : 0,
                Percent = expenseTotal > 0 ? $"{x.Amount / expenseTotal:P0}" : "0%", Color = x.Color
            }).ToArray();
            VisibleCategories = _showAllCategories ? _allCategories : _allCategories.Take(PreviewCount).ToArray();
            HasCategories = _allCategories.Count > 0;
            CanExpandCategories = _allCategories.Count > PreviewCount;

            var incomeCategories = incomeRows.GroupBy(x => x.CategoryId).Select(group =>
            {
                var first = group.First();
                var name = string.IsNullOrWhiteSpace(first.CategoryName) ? "Uncategorized" : first.CategoryName;
                return new { Id = group.Key, Name = name, Amount = group.Sum(x => x.Amount), Icon = CategoryIconCatalog.FileNameFor(first.CategoryIconKey),
                    Color = (ChartColorPalette.ForShop(name) as SolidColorBrush)?.Color ?? Colors.Green };
            }).OrderByDescending(x => x.Amount).ThenBy(x => x.Name).ToArray();
            IncomeCategoryChart = incomeCategories.Select(x => new CategoryExpenseChartData
            { CategoryId = x.Id, CategoryName = x.Name, TotalValue = x.Amount, Color = new SolidColorBrush(x.Color) }).ToArray();
            IncomeCategoryRows = incomeCategories.Select(x => new DashboardCategoryRow
            {
                Id = x.Id, Name = x.Name, Icon = x.Icon, Amount = EuroFormatter.Format(x.Amount), Color = x.Color,
                Fraction = incomeTotal > 0 ? (double)(x.Amount / incomeTotal) : 0,
                Percent = incomeTotal > 0 ? $"{x.Amount / incomeTotal:P0}" : "0%"
            }).ToArray();

            _allTransactions = expenseRows.Select(x => new DashboardTransactionRow
            {
                Id = x.Id, Type = FinancialTransactionType.Expense, Date = x.BillDate,
                Name = x.ShopName, Category = x.ShopCategoryName, Icon = x.ShopIconFileName,
                Amount = $"− {EuroFormatter.Format(x.BillValue)}"
            }).Concat(incomeRows.Select(x => new DashboardTransactionRow
            {
                Id = x.Id, Type = FinancialTransactionType.Income, Date = x.TransactionDate,
                Name = x.ShopName, Category = x.CategoryName, Icon = CategoryIconCatalog.FileNameFor(x.CategoryIconKey),
                Amount = $"+ {EuroFormatter.Format(x.Amount)}"
            })).OrderByDescending(x => x.Date).ThenByDescending(x => x.Id).ToArray();
            VisibleTransactions = _showAllTransactions ? _allTransactions : _allTransactions.Take(PreviewCount).ToArray();
            HasTransactions = _allTransactions.Count > 0;
            CanExpandTransactions = _allTransactions.Count > PreviewCount;
            _range = range;
            _loadedPeriod = SelectedPeriod;
        }
        catch (Exception ex)
        {
            if (version == _loadVersion)
            {
                _restoringPeriod = true;
                try { SelectedPeriod = _loadedPeriod; }
                finally { _restoringPeriod = false; }
                errors.HandleError(ex);
            }
        }
        finally
        {
            if (version == _loadVersion) IsBusy = false;
        }
    }

    private static IReadOnlyList<DashboardMiniBar> MakeBars(IEnumerable<(DateTime Date, decimal Amount)> rows, FinancialDashboardRange range)
    {
        const int count = 8;
        var buckets = new decimal[count];
        var days = Math.Max(1, (range.EndExclusive - range.Start).TotalDays);
        foreach (var (date, amount) in rows)
        {
            var index = Math.Clamp((int)((date - range.Start).TotalDays / days * count), 0, count - 1);
            buckets[index] += amount;
        }
        var maximum = buckets.Max();
        return buckets.Select(value => new DashboardMiniBar(maximum > 0 ? 5 + (double)(value / maximum) * 25 : 5)).ToArray();
    }
}
