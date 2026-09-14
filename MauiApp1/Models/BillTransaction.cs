namespace MauiApp1.Models;

public class BillTransaction
{
    public int Id { get; set; }
    public DateTime BillDate { get; set; } = DateTime.Today;
    public int ShopId { get; set; }
    public int PaymentModeId { get; set; }
    public decimal BillValue { get; set; }
    public string Note { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public ShopCategory ShopCategory { get; set; } = ShopCategory.DefaultShop;
    public int ShopCategoryId { get; set; }
    public string ShopCategoryIconKey { get; set; } = "default_shop";
    public string ShopIconFileName => CategoryIconCatalog.FileNameFor(ShopCategoryIconKey);
    public string PaymentModeName { get; set; } = string.Empty;
    public int DisplayNumber { get; set; }
}
