using CommunityToolkit.Mvvm.ComponentModel;

namespace FinTracker.Models;

public partial class BackupMonthSelection : ObservableObject
{
    public int MonthNumber { get; init; }
    public string MonthName { get; init; } = string.Empty;
    [ObservableProperty] private bool _isSelected;
}
