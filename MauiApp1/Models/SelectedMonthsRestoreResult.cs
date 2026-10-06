namespace MauiApp1.Models;

public sealed class SelectedMonthsRestoreResult
{
    public int Imported { get; init; }
    public int Replaced { get; init; }
    public int Failed { get; init; }
    public int Year { get; init; }
    public IReadOnlyList<int> Months { get; init; } = [];
}
