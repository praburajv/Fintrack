using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using System.Globalization;

namespace MauiApp1.Models;

public partial class ExpenseMonthGroup : ObservableObject
{
    public int Year { get; init; }
    public int MonthNumber { get; init; }
    public string MonthName => CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(MonthNumber);
    public decimal TotalValue => Bills.Sum(b => b.BillValue);
    public bool HasBills => Bills.Count > 0;
    public string NoBillsText => $"No bills for {MonthName} {Year}.";
    public string ExpandGlyph => IsExpanded ? "−" : "+";
    public ObservableCollection<BillTransaction> Bills { get; } = [];

    [ObservableProperty]
    private bool _isExpanded;

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandGlyph));
}
