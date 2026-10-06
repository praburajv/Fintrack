using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Utilities;
using System.Collections.ObjectModel;

namespace MauiApp1.PageModels;

public partial class DailyBillListPageModel : ObservableObject, IQueryAttributable
{
    private readonly BillTransactionRepository _bills;
    private readonly IncomeTransactionRepository _income;
    private readonly ShopRepository _shops;
    private readonly ModalErrorHandler _errors;
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _sortGate = new(1, 1);
    [ObservableProperty] private DateTime _selectedDate;
    [ObservableProperty] private DateTime _rangeStart;
    [ObservableProperty] private DateTime _rangeEndExclusive;
    [ObservableProperty] private int? _categoryId;
    [ObservableProperty] private int? _shopId;
    [ObservableProperty] private string _shopName = string.Empty;
    [ObservableProperty] private string _categoryName = string.Empty;
    [ObservableProperty] private ObservableCollection<BillTransaction> _items = [];
    [ObservableProperty] private ObservableCollection<CategoryExpenseGroup> _groups = [];
    [ObservableProperty] private decimal _dailyTotal;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isBillListLoading;
    [ObservableProperty] private bool _isSorting;
    [ObservableProperty] private FinancialTransactionType _transactionType = FinancialTransactionType.Expense;
    [ObservableProperty] private string _processingMessage = "Loading bills...";
    private bool? _valueSortAscending;

    public string DateTitle => CategoryId.HasValue
        ? $"{(TransactionType == FinancialTransactionType.Income ? "Income" : "Bills")} — {CategoryName} — {RangeDisplayText}"
        : ShopId.HasValue
            ? $"Bills — {ShopName} — {(HasValidRange ? RangeDisplayText : SelectedDate.ToString("dd/MM/yyyy"))}"
            : $"{(TransactionType == FinancialTransactionType.Income ? "Income" : "Bills")} — {SelectedDate:dd/MM/yyyy}";
    public string TotalText => $"Total: {EuroFormatter.Format(DailyTotal)}";
    public bool HasBills => Items.Count > 0;
    public bool HasGroups => Groups.Count > 0;
    public string EmptyText => CategoryId.HasValue
        ? $"No {CategoryName} bills found for {RangeDisplayText}."
        : ShopId.HasValue
            ? $"No {ShopName} bills found for {(HasValidRange ? RangeDisplayText : SelectedDate.ToString("dd/MM/yyyy"))}."
            : $"No bills found for {SelectedDate:dd/MM/yyyy}.";
    public bool IsOverlayBusy => IsBusy || IsBillListLoading || IsSorting;
    private bool HasValidRange => RangeStart != default && RangeEndExclusive > RangeStart;
    private string RangeDisplayText => RangeStart != default && RangeEndExclusive == RangeStart.AddYears(1)
        ? RangeStart.ToString("yyyy")
        : RangeStart != default && RangeStart.Day == 1 && RangeEndExclusive == RangeStart.AddMonths(1)
            ? RangeStart.ToString("MMMM yyyy")
            : $"{RangeStart:dd/MM/yyyy} – {RangeEndExclusive.AddDays(-1):dd/MM/yyyy}";
    public string ValueSortText => _valueSortAscending switch
    {
        true => "Value ↑",
        false => "Value ↓",
        null => "Value ↕"
    };

    public DailyBillListPageModel(BillTransactionRepository bills, IncomeTransactionRepository income, ShopRepository shops, ModalErrorHandler errors) { _bills = bills; _income = income; _shops = shops; _errors = errors; }
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsBillListLoadingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsSortingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        // A reused page must not retain a shop/category/range from a previous drilldown.
        SelectedDate = default; RangeStart = default; RangeEndExclusive = default;
        ShopId = null; CategoryId = null; ShopName = string.Empty; CategoryName = string.Empty;
        TransactionType = FinancialTransactionType.Expense;
        if (query.TryGetValue("date", out var value) && DateTime.TryParseExact(value?.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
            SelectedDate = date;
        if (query.TryGetValue("from", out var from) && DateTime.TryParseExact(from?.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedFrom))
            RangeStart = parsedFrom;
        if (query.TryGetValue("to", out var to) && DateTime.TryParseExact(to?.ToString(), "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsedTo))
            RangeEndExclusive = parsedTo;
        if (query.TryGetValue("categoryId", out var category) && int.TryParse(category?.ToString(), out var parsedCategory))
            CategoryId = parsedCategory;
        if (query.TryGetValue("shopId", out var shop) && int.TryParse(shop?.ToString(), out var parsedShop) && parsedShop > 0)
            ShopId = parsedShop;
        if (query.TryGetValue("categoryName", out var categoryName))
            CategoryName = categoryName?.ToString() ?? "Category";
        if (query.TryGetValue("type", out var type) && Enum.TryParse<FinancialTransactionType>(type?.ToString(), true, out var parsedType)) TransactionType = parsedType;
        OnPropertyChanged(nameof(DateTitle)); OnPropertyChanged(nameof(TotalText)); OnPropertyChanged(nameof(EmptyText));
    }

    [RelayCommand]
    private async Task Appearing()
    {
        if (CategoryId.HasValue && !HasValidRange) return;
        if (!CategoryId.HasValue && !(ShopId.HasValue && HasValidRange) && SelectedDate == default) return;
        if (!await _loadGate.WaitAsync(0)) return;
        try
        {
            ProcessingMessage = CategoryId.HasValue || (ShopId.HasValue && HasValidRange)
                ? $"Loading bills for {RangeDisplayText}..."
                : $"Loading bills for {SelectedDate:dd MMMM yyyy}...";
            IsBillListLoading = true;
            await Task.Yield();
            if (ShopId.HasValue)
            {
                var shop = await _shops.GetAsync(ShopId.Value) ?? throw new InvalidOperationException("The selected shop no longer exists.");
                ShopName = shop.Name;
                OnPropertyChanged(nameof(DateTitle));
                OnPropertyChanged(nameof(EmptyText));
            }
            List<BillTransaction> displayBills;
            if (TransactionType == FinancialTransactionType.Income)
            {
                var useRange = CategoryId.HasValue || (ShopId.HasValue && HasValidRange);
                var income = await _income.ListAsync(useRange ? RangeStart : SelectedDate.Date, useRange ? RangeEndExclusive : SelectedDate.Date.AddDays(1));
                displayBills = income.Where(item => (!CategoryId.HasValue || item.CategoryId == CategoryId.Value) && (!ShopId.HasValue || item.ShopId == ShopId.Value)).Select(item => new BillTransaction { Id = item.Id, BillDate = item.TransactionDate, ShopId = item.ShopId, PaymentModeId = item.PaymentModeId, BillValue = item.Amount, Note = item.Note, ShopName = item.ShopName, PaymentModeName = item.PaymentModeName, ShopCategoryId = item.CategoryId, ShopCategoryName = item.CategoryName, ShopCategoryIconKey = item.CategoryIconKey, TransactionType = FinancialTransactionType.Income }).ToList();
            }
            else
            {
                displayBills = CategoryId.HasValue
                    ? await _bills.GetBillsByCategoryAsync(CategoryId.Value, RangeStart, RangeEndExclusive)
                    : ShopId.HasValue
                        ? RangeStart != default && RangeEndExclusive > RangeStart
                            ? await _bills.GetBillsByShopAndRangeAsync(ShopId.Value, RangeStart, RangeEndExclusive)
                            : await _bills.GetBillsByShopAndDateAsync(ShopId.Value, SelectedDate)
                        : await _bills.GetBillsForDateAsync(SelectedDate);
            }
            Items = new ObservableCollection<BillTransaction>(displayBills);
            Groups = CategoryExpenseGrouping.Group(Items);
            ApplyValueSort();
            DailyTotal = Items.Sum(item => item.BillValue);
            OnPropertyChanged(nameof(HasBills)); OnPropertyChanged(nameof(HasGroups)); OnPropertyChanged(nameof(TotalText)); OnPropertyChanged(nameof(EmptyText));
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { IsBillListLoading = false; _loadGate.Release(); }
    }

    [RelayCommand]
    private static Task ToggleCategory(CategoryExpenseGroup group)
    {
        if (group is not null) group.IsExpanded = !group.IsExpanded;
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ToggleValueSort()
    {
        if (IsOverlayBusy || !await _sortGate.WaitAsync(0)) return;
        var targetAscending = _valueSortAscending is null or false;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            IsSorting = true;
            ProcessingMessage = targetAscending
                ? "Sorting bills by value (ascending)..."
                : "Sorting bills by value (descending)...";
            await Task.Yield();

            var snapshots = Groups.Select(group => new GroupSortSnapshot(
                group.CategoryId,
                group.CategoryName,
                group.IconFileName,
                group.Budgeted,
                group.IsExpanded,
                group.IsHighest,
                group.IsIncome,
                group.Bills.ToArray())).ToArray();

            var sorted = await Task.Run(() => snapshots.Select(snapshot => new SortedGroupResult(
                snapshot,
                (targetAscending
                    ? snapshot.Bills.OrderBy(bill => bill.BillValue).ThenBy(bill => bill.BillDate).ThenBy(bill => bill.Id)
                    : snapshot.Bills.OrderByDescending(bill => bill.BillValue).ThenByDescending(bill => bill.BillDate).ThenByDescending(bill => bill.Id))
                .ToArray())).ToArray());

            var replacement = new ObservableCollection<CategoryExpenseGroup>();
            foreach (var result in sorted)
            {
                var group = new CategoryExpenseGroup
                {
                    CategoryId = result.Source.CategoryId,
                    CategoryName = result.Source.CategoryName,
                    IconFileName = result.Source.IconFileName,
                    IsIncome = result.Source.IsIncome,
                    Budgeted = result.Source.Budgeted,
                    IsExpanded = result.Source.IsExpanded,
                    IsHighest = result.Source.IsHighest
                };
                foreach (var bill in result.Bills)
                    group.Bills.Add(bill);
                replacement.Add(group);
            }

            Groups = replacement;
            _valueSortAscending = targetAscending;
            OnPropertyChanged(nameof(ValueSortText));
            System.Diagnostics.Debug.WriteLine($"Bill value sort completed: {Items.Count} bills, {Groups.Count} categories, {timer.ElapsedMilliseconds} ms.");
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally
        {
            IsSorting = false;
            ProcessingMessage = "Loading bills...";
            _sortGate.Release();
        }
    }

    private void ApplyValueSort()
    {
        if (!_valueSortAscending.HasValue) return;
        foreach (var group in Groups)
        {
            var ordered = _valueSortAscending.Value
                ? group.Bills.OrderBy(bill => bill.BillValue).ThenBy(bill => bill.BillDate).ThenBy(bill => bill.Id).ToArray()
                : group.Bills.OrderByDescending(bill => bill.BillValue).ThenByDescending(bill => bill.BillDate).ThenByDescending(bill => bill.Id).ToArray();
            group.Bills.Clear();
            foreach (var bill in ordered) group.Bills.Add(bill);
        }
    }

    private sealed record GroupSortSnapshot(
        int CategoryId,
        string CategoryName,
        string IconFileName,
        decimal Budgeted,
        bool IsExpanded,
        bool IsHighest,
        bool IsIncome,
        BillTransaction[] Bills);

    private sealed record SortedGroupResult(GroupSortSnapshot Source, BillTransaction[] Bills);

}
