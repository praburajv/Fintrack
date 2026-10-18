using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using FinTracker.Utilities;

namespace FinTracker.Models;

public partial class EtlMonthGroup : ObservableObject
{
    public required DateTime Month { get; init; }
    public required int FinancialAccountId { get; init; }
    public string Title => Month.ToString("MMMM yyyy", System.Globalization.CultureInfo.CurrentCulture);
    public ObservableCollection<BillTransaction> Bills { get; } = [];
    public int Count => Bills.Count;
    public string Summary => $"{Count} bills · {EuroFormatter.Format(Bills.Sum(x => x.BillValue))}";
    [ObservableProperty] private bool _isExpanded;
    public string ExpandGlyph => IsExpanded ? "▾" : "▸";
    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandGlyph));
}
