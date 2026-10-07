using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using FinTracker.Models;
using System.Collections.ObjectModel;

namespace FinTracker.PageModels;

public partial class ExpenseListPageModel : ObservableObject
{
    private readonly BillTransactionRepository _bills;
    private readonly IncomeTransactionRepository _income;
    private readonly ModalErrorHandler _errors;
    private readonly IAuthenticationService _authentication;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly HashSet<(int Year, int Month)> _loadedMonths = [];
    private (int Year, int Month)? _selectedMonth;
    private (int Year, int Month)? _requestedMonth;
    private readonly HashSet<(int Year, int Month)> _pendingRefreshMonths = [];
    private bool _hasLoaded;
    [ObservableProperty] private ObservableCollection<ExpenseMonthGroup> _months = [];
    [ObservableProperty] private int _year = DateTime.Today.Year;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isMonthLoading;
    [ObservableProperty] private string _processingMessage = "Loading expenses...";
    public bool IsOverlayBusy => IsBusy || IsMonthLoading;
    public string YearTitle => Year.ToString();
    public bool HasNoExpenses => Months.Count > 0 && Months.All(month => month.TotalExpense == 0 && month.TotalIncome == 0);
    [ObservableProperty] private bool _canMaintainExpenses;
    [ObservableProperty] private ExpenseListMode _mode = ExpenseListMode.FullYear;
    public void SetAccessMode(ExpenseListMode mode)
    {
        if (Mode == mode) return;
        Mode = mode;
        _selectedMonth = null;
        _loadedMonths.Clear();
        _pendingRefreshMonths.Clear();
        _hasLoaded = false;
        OnPropertyChanged(nameof(CurrentMonthOnly));
        if (mode == ExpenseListMode.DashboardRecentMonths) Year = DateTime.Today.Year;
    }
    public async Task OpenSpecificMonthAsync(int year, int month)
    {
        await _loadGate.WaitAsync();
        IsMonthLoading = true;
        try
        {
            SetAccessMode(ExpenseListMode.FullYear);
            Year = year;
            // Keep summary/detail loading together so an Appearing refresh cannot
            // consume the gate and leave the requested month unopened.
            await LoadHeadersAndSummariesAsync(gateAlreadyHeld: true);
            var group = Months.FirstOrDefault(item => item.Year == year && item.MonthNumber == month);
            if (group is null) return;
            _selectedMonth = (year, month);
            group.IsExpanded = true;
            ProcessingMessage = $"Loading {group.MonthName} expenses...";
            await LoadMonthAsync(group, System.Diagnostics.Stopwatch.StartNew(), gateAlreadyHeld: true);
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsMonthLoading = false; ProcessingMessage = string.Empty; _loadGate.Release(); }
    }
    public void RequestSpecificMonth(int year, int month)
    {
        // Validate navigation values without starting an unobserved async load.
        _ = new DateTime(year, month, 1);
        _requestedMonth = (year, month);
    }
    public bool CurrentMonthOnly => Mode == ExpenseListMode.DashboardRecentMonths;
    public ExpenseListPageModel(BillTransactionRepository bills, IncomeTransactionRepository income, ModalErrorHandler errors, IAuthenticationService authentication)
    {
        _bills=bills; _income=income; _errors=errors; _authentication=authentication; CanMaintainExpenses = authentication.IsAuthenticated; _authentication.AuthenticationStateChanged += OnAuthenticationStateChanged;
        WeakReferenceMessenger.Default.Register<BillChangedMessage>(this, static (recipient, message) => ((ExpenseListPageModel)recipient).OnBillChanged(message.Value));
    }
    private void OnAuthenticationStateChanged(object? sender, EventArgs e) => RefreshAuthorization();
    public void RefreshAuthorization() => CanMaintainExpenses = _authentication.IsAuthenticated;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsMonthLoadingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    [RelayCommand] private async Task Appearing()
    {
        RefreshAuthorization();
        if (_requestedMonth is { } requested)
        {
            _requestedMonth = null;
            await OpenSpecificMonthAsync(requested.Year, requested.Month);
        }
        else if (!_hasLoaded) await LoadHeadersAndSummariesAsync();
        else if (_pendingRefreshMonths.Count > 0) await RefreshAffectedMonthsAsync();
    }
    [RelayCommand] private Task Load() => LoadHeadersAndSummariesAsync();
    [RelayCommand] private async Task PreviousYear() { if (!CurrentMonthOnly) { Year--; _selectedMonth = null; await LoadHeadersAndSummariesAsync(); } }
    [RelayCommand] private async Task NextYear() { if (!CurrentMonthOnly) { Year++; _selectedMonth = null; await LoadHeadersAndSummariesAsync(); } }

    private async Task LoadHeadersAndSummariesAsync(bool gateAlreadyHeld = false)
    {
        if (!gateAlreadyHeld && !await _loadGate.WaitAsync(0)) return;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            IsBusy = true; ProcessingMessage = "Loading expenses..."; _loadedMonths.Clear(); var now = DateTime.Now;
            await Task.Yield();
            var rangeStart = CurrentMonthOnly ? new DateTime(now.Year, now.Month, 1).AddMonths(-3) : new DateTime(Year, 1, 1);
            var rangeEnd = CurrentMonthOnly ? new DateTime(now.Year, now.Month, 1).AddMonths(1) : new DateTime(Year + 1, 1, 1);
            var totals = await _bills.GetMonthlyTotalsAsync(rangeStart, rangeEnd);
            var incomeTotals = await _income.GetMonthlyTotalsAsync(rangeStart, rangeEnd);
            var headers = CurrentMonthOnly
                ? Enumerable.Range(0, 4).Select(offset => new DateTime(now.Year, now.Month, 1).AddMonths(-3 + offset)).Select(date => new ExpenseMonthGroup { Year = date.Year, MonthNumber = date.Month, SummaryTotal = totals.GetValueOrDefault((date.Year, date.Month)), IncomeSummaryTotal = incomeTotals.GetValueOrDefault((date.Year, date.Month)) }).ToArray()
                : Enumerable.Range(1, 12).Select(month => new ExpenseMonthGroup { Year = Year, MonthNumber = month, SummaryTotal = totals.GetValueOrDefault((Year, month)), IncomeSummaryTotal = incomeTotals.GetValueOrDefault((Year, month)) }).ToArray();
            Months = new ObservableCollection<ExpenseMonthGroup>(headers); OnPropertyChanged(nameof(YearTitle)); OnPropertyChanged(nameof(HasNoExpenses));
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; ProcessingMessage = string.Empty; _hasLoaded = true; if (!gateAlreadyHeld) _loadGate.Release(); System.Diagnostics.Debug.WriteLine($"My Expense summary load: {timer.ElapsedMilliseconds} ms"); }
    }

    [RelayCommand]
    private async Task ToggleMonth(ExpenseMonthGroup group)
    {
        if (group is null || IsBusy || IsMonthLoading) return;
        _selectedMonth = (group.Year, group.MonthNumber);
        if (group.IsExpanded)
        {
            group.IsExpanded = false;
            return;
        }

        group.IsExpanded = true;
        if (!group.IsLoaded)
        {
            ProcessingMessage = $"Loading {group.MonthName} expenses...";
            IsMonthLoading = true;
            await Task.Yield();
            try { await LoadMonthAsync(group, System.Diagnostics.Stopwatch.StartNew()); }
            finally
            {
                IsMonthLoading = false;
                ProcessingMessage = string.Empty;
            }
        }
    }

    private async Task LoadMonthAsync(ExpenseMonthGroup month, System.Diagnostics.Stopwatch timer, bool gateAlreadyHeld = false)
    {
        var key = (month.Year, month.MonthNumber);
        if (_loadedMonths.Contains(key)) return;
        if (!gateAlreadyHeld && !await _loadGate.WaitAsync(0)) return;
        try
        {
            var start = new DateTime(month.Year, month.MonthNumber, 1); var bills = await _bills.ListAsync(start, start.AddMonths(1));
            var income = await _income.ListAsync(start, start.AddMonths(1));
            var number = 1; foreach (var bill in bills.OrderBy(b => b.BillDate).ThenBy(b => b.Id)) { bill.DisplayNumber = number++; month.Bills.Add(bill); }
            foreach (var categoryBills in month.Bills.GroupBy(b => string.IsNullOrWhiteSpace(b.ShopCategoryName) ? "Other" : b.ShopCategoryName).OrderByDescending(g => g.Sum(b => b.BillValue)).ThenBy(g => g.Key))
            {
                var first = categoryBills.First(); var category = new CategoryExpenseGroup { CategoryId = first.ShopCategoryId, CategoryName = categoryBills.Key, IconFileName = CategoryIconCatalog.FileNameFor(first.ShopCategoryIconKey) };
                foreach (var bill in categoryBills) category.Bills.Add(bill); month.Categories.Add(category);
            }
            var incomeNumber = 1;
            foreach (var item in income)
                month.IncomeBills.Add(new BillTransaction { Id = item.Id, BillDate = item.TransactionDate, ShopId = item.ShopId, PaymentModeId = item.PaymentModeId, BillValue = item.Amount, Note = item.Note, ShopName = item.ShopName, PaymentModeName = item.PaymentModeName, ShopCategoryId = item.CategoryId, ShopCategoryName = item.CategoryName, ShopCategoryIconKey = item.CategoryIconKey, DisplayNumber = incomeNumber++, TransactionType = FinancialTransactionType.Income });
            foreach (var categoryBills in month.IncomeBills.GroupBy(b => string.IsNullOrWhiteSpace(b.ShopCategoryName) ? "Credits" : b.ShopCategoryName).OrderByDescending(g => g.Sum(b => b.BillValue)).ThenBy(g => g.Key))
            {
                var first = categoryBills.First();
                var category = new CategoryExpenseGroup { CategoryId = first.ShopCategoryId, CategoryName = categoryBills.Key, IconFileName = CategoryIconCatalog.FileNameFor(first.ShopCategoryIconKey), IsIncome = true };
                foreach (var item in categoryBills) category.Bills.Add(item);
                month.IncomeCategories.Add(category);
            }
            month.IsLoaded = true; _loadedMonths.Add(key); OnPropertyChanged(nameof(HasNoExpenses));
            System.Diagnostics.Debug.WriteLine($"My Expense month query/group ({month.Year}-{month.MonthNumber:00}): {timer.ElapsedMilliseconds} ms, {bills.Count} bills");
        }
        catch (Exception ex)
        {
            month.IsExpanded = false;
            _errors.HandleError(ex);
        }
        finally { if (!gateAlreadyHeld) _loadGate.Release(); }
    }

    private void OnBillChanged(BillChanged change)
    {
        AddRefreshMonth(change.PreviousDate);
        AddRefreshMonth(change.CurrentDate);
    }

    private void AddRefreshMonth(DateTime? date)
    {
        if (date is { } value)
            _pendingRefreshMonths.Add((value.Year, value.Month));
    }

    private async Task RefreshAffectedMonthsAsync()
    {
        if (_pendingRefreshMonths.Count == 0 || !await _loadGate.WaitAsync(0)) return;
        var pending = _pendingRefreshMonths.ToArray();
        _pendingRefreshMonths.Clear();
        try
        {
            IsBusy = true;
            ProcessingMessage = "Refreshing transactions...";
            await Task.Yield();
            foreach (var month in Months.Where(item => pending.Contains((item.Year, item.MonthNumber))))
            {
                var wasExpanded = month.IsExpanded;
                month.Bills.Clear();
                month.IncomeBills.Clear();
                month.Categories.Clear();
                month.IncomeCategories.Clear();
                month.IsLoaded = false;
                _loadedMonths.Remove((month.Year, month.MonthNumber));
                var start = new DateTime(month.Year, month.MonthNumber, 1);
                var totals = await _bills.GetMonthlyTotalsAsync(start, start.AddMonths(1));
                month.SummaryTotal = totals.GetValueOrDefault((month.Year, month.MonthNumber));
                var incomeTotals = await _income.GetMonthlyTotalsAsync(start, start.AddMonths(1));
                month.IncomeSummaryTotal = incomeTotals.GetValueOrDefault((month.Year, month.MonthNumber));
                if (wasExpanded)
                {
                    ProcessingMessage = $"Loading {month.MonthName} expenses...";
                    await LoadMonthAsync(month, System.Diagnostics.Stopwatch.StartNew(), gateAlreadyHeld: true);
                }
            }
            OnPropertyChanged(nameof(HasNoExpenses));
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; ProcessingMessage = string.Empty; _loadGate.Release(); }
    }

    [RelayCommand] private void ToggleCategory(CategoryExpenseGroup group) { if (group is not null) group.IsExpanded = !group.IsExpanded; }
    [RelayCommand]
    private async Task AddBill()
    {
        if (!await RequireAuthenticationAsync()) return;
        await Shell.Current.GoToAsync("bill");
    }

    [RelayCommand]
    private async Task EditBill(BillTransaction bill)
    {
        if (bill is null || IsOverlayBusy || !await RequireAuthenticationAsync()) return;
        IsBusy = true; ProcessingMessage = "Opening transaction for editing...";
        try
        {
            await Task.Yield();
            await Shell.Current.GoToAsync($"bill?id={bill.Id}&type={bill.TransactionType}");
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBusy = false; ProcessingMessage = string.Empty; }
    }

    private async Task<bool> RequireAuthenticationAsync()
    {
        if (AppShell.IsClosing) return false;
        if (_authentication.IsAuthenticated) return true;
        await Shell.Current.DisplayAlertAsync("Authentication Required", "Please login to add or modify expenses.", "OK");
        return false;
    }
    [RelayCommand] private Task ManageShops()=>Shell.Current.GoToAsync("shops");
    [RelayCommand] private Task ManagePaymentModes()=>Shell.Current.GoToAsync("paymentmodes");
}
