using FinTracker.Models;

namespace FinTracker.Utilities;

/// <summary>Shared local filtering of a complete, account-scoped selector list.</summary>
public static class SelectionSearch
{
    public static bool SameItem(object? left, object? right) => (left, right) switch
    {
        (Shop a, Shop b) => a.Id == b.Id && a.FinancialAccountId == b.FinancialAccountId,
        (Category a, Category b) => a.Id == b.Id && a.FinancialAccountId == b.FinancialAccountId,
        _ => ReferenceEquals(left, right)
    };
    public static string Name(object? item) => item switch
    {
        Shop shop => shop.Name,
        Category category => category.Name,
        _ => item?.ToString() ?? string.Empty
    };

    public static string Detail(object? item) => item is Shop shop ? shop.CategoryName : string.Empty;

    public static IEnumerable<object> Filter(IEnumerable<object> items, string? text)
    {
        var prefix = text?.Trim() ?? string.Empty;
        return items.Where(item => prefix.Length == 0 || Name(item).StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .OrderBy(Name, StringComparer.OrdinalIgnoreCase);
    }
}
