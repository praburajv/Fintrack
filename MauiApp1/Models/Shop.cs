namespace MauiApp1.Models;

public class Shop
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ShopCategory Category { get; set; } = ShopCategory.DefaultShop;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryIconKey { get; set; } = "default_shop";
    public string CategoryIconFileName => CategoryIconCatalog.FileNameFor(CategoryIconKey);
    public string IconFileName => CategoryId > 0 ? CategoryIconFileName : ShopCategoryCatalog.IconFor(Category);
    public override string ToString() => Name;
}
