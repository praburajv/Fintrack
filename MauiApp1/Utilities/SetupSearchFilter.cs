using MauiApp1.Models;

namespace MauiApp1.Utilities;

public static class SetupSearchFilter
{
    public static (List<T> Items, IReadOnlyList<SetupSearchSuggestion> Suggestions) Apply<T>(
        IEnumerable<T> source, string? searchText, int? selectedId, Func<T, int> getId, Func<T, string> getName)
    {
        var all = source.ToList();
        var term = searchText?.Trim();
        if (string.IsNullOrEmpty(term)) return (all, []);

        var matches = all.Where(item => getName(item).Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        var suggestions = matches.Take(8).Select(item => new SetupSearchSuggestion(getId(item), getName(item))).ToArray();
        return (selectedId is int id ? matches.Where(item => getId(item) == id).ToList() : matches, suggestions);
    }
}
