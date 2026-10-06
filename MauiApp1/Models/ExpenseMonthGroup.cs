using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MauiApp1.Models;

public partial class ExpenseMonthGroup : ObservableObject
{
    public int Year { get; init; }
    public int MonthNumber { get; init; }
    public string MonthName => new DateTime(2000, MonthNumber, 1).ToString("MMM", CultureInfo.InvariantCulture);
    public string ShortMonthName => new DateTime(2000, MonthNumber, 1).ToString("MMM", CultureInfo.InvariantCulture);
    public decimal TotalValue => SummaryTotal ?? Bills.Sum(b => b.BillValue);
    public decimal TotalExpense => SummaryTotal ?? Bills.Sum(b => b.BillValue);
    private decimal? _incomeSummaryTotal;
    public decimal? IncomeSummaryTotal { get => _incomeSummaryTotal; set { if (SetProperty(ref _incomeSummaryTotal, value)) OnPropertyChanged(nameof(TotalIncome)); } }
    public decimal TotalIncome => IncomeSummaryTotal ?? IncomeBills.Sum(b => b.BillValue);
    private decimal? _summaryTotal;
    public decimal? SummaryTotal { get => _summaryTotal; set { if (SetProperty(ref _summaryTotal, value)) { OnPropertyChanged(nameof(TotalValue)); OnPropertyChanged(nameof(TotalExpense)); } } }
    public bool HasBills => Bills.Count > 0;
    public bool IsLoaded { get; set; }
    public string NoBillsText => $"No bills for {MonthName} {Year}.";
    public string ExpandGlyph => IsExpanded ? "−" : "+";
    public ObservableCollection<BillTransaction> Bills { get; } = [];
    public ObservableCollection<BillTransaction> IncomeBills { get; } = [];
    public ObservableCollection<CategoryExpenseGroup> Categories { get; } = [];
    public ObservableCollection<CategoryExpenseGroup> IncomeCategories { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandGlyph));
}
