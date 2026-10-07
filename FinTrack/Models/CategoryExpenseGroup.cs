using CommunityToolkit.Mvvm.ComponentModel;
using FinTracker.Utilities;
using System.Collections.ObjectModel;
namespace FinTracker.Models;
public partial class CategoryExpenseGroup : ObservableObject
{
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = "Default Shop";
    public string IconFileName { get; init; } = "category_default_shop.svg";
    public ObservableCollection<BillTransaction> Bills { get; } = [];
    public decimal TotalExpense => Bills.Sum(x => x.BillValue);
    public decimal Budgeted { get; init; }
    public decimal Difference => Budgeted - TotalExpense;
    public string BudgetedText => Budgeted == 0 ? string.Empty : EuroFormatter.Format(Budgeted);
    public string DifferenceText => Budgeted == 0 ? string.Empty : EuroFormatter.Format(Difference);
    public Color DifferenceTextColor => Difference > 0 ? Colors.Green : Colors.Red;
    public bool HasBudget => Budgeted > 0;
    public bool HasExpense => TotalExpense > 0;
    public bool HasNoBudget => !HasBudget;
    public bool HasNoExpense => !HasExpense;
    public decimal BudgetUsedPercent => HasBudget ? TotalExpense / Budgeted * 100 : 0;
    public string BudgetUsedPercentText => HasBudget ? $"{BudgetUsedPercent:0.0}%" : string.Empty;
    public string BudgetStatusText => !HasBudget ? "No budget configured" : BudgetUsedPercent > 100 ? "Over Budget" : BudgetUsedPercent >= 80 ? "Approaching Budget" : HasExpense ? "Within Budget" : "No spending recorded this month";
    public string BudgetBalanceLabel => !HasBudget ? string.Empty : Difference >= 0 ? "Remaining" : "Over Budget";
    public string BudgetBalanceText => HasBudget ? EuroFormatter.Format(Math.Abs(Difference)) : string.Empty;
    public Color BudgetStatusColor => !HasBudget || BudgetUsedPercent < 80 ? Colors.Green : BudgetUsedPercent <= 100 ? Colors.Orange : Colors.Red;
    public int BillCount => Bills.Count;
    public string TotalText => EuroFormatter.Format(TotalExpense);
    public Color TotalTextColor => IsIncome ? (Application.Current?.Resources["Success"] as Color ?? Colors.Green) : (Application.Current?.Resources["TextPrimary"] as Color ?? Colors.Black);
    public string BillCountText => $"{BillCount} bill{(BillCount == 1 ? string.Empty : "s")}";
    public string HighestText => IsHighest ? "Highest Expense" : string.Empty;
    public Color? HighlightTextColor => IsHighest ? Colors.Red : null;
    public bool IsIncome { get; init; }
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isHighest;
    public string ExpandGlyph => IsExpanded ? "▲" : "▼";
    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandGlyph));
    partial void OnIsHighestChanged(bool value)
    {
        OnPropertyChanged(nameof(HighestText));
        OnPropertyChanged(nameof(HighlightTextColor));
    }
}
