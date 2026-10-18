namespace FinTracker.Models;

public sealed class Category
{
    public int Id { get; set; }
    public int FinancialAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconKey { get; set; } = "default_shop";
    public decimal Budgeted { get; set; }
    public string FriendlyIconName => CategoryIconCatalog.DisplayNameFor(IconKey);
    public string IconFileName => CategoryIconCatalog.FileNameFor(IconKey);
    public override string ToString() => Name;
}
