using MauiApp1.Models;
using System.Text.RegularExpressions;

namespace MauiApp1.Services;

public sealed class EtlTextMappingService
{
    public int? FindMappedShopId(string description, IReadOnlyList<EtlTextMap> mappings, IReadOnlySet<int> availableShopIds)
    {
        var normalizedDescription = NormalizeForMatch(description);
        return mappings
            .Select(mapping => new { Mapping = mapping, Text = NormalizeForMatch(mapping.LookupText) })
            .Where(x => x.Mapping.ShopId > 0 && availableShopIds.Contains(x.Mapping.ShopId) && x.Text.Length > 0 && normalizedDescription.Contains(x.Text, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.Text.Length)
            .ThenBy(x => x.Mapping.Id)
            .Select(x => (int?)x.Mapping.ShopId)
            .FirstOrDefault();
    }

    public static string NormalizeForMatch(string value) => Regex.Replace(value ?? string.Empty, @"\s+", " ").Trim();
}
