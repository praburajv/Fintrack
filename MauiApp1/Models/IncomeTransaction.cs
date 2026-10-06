namespace MauiApp1.Models;

public sealed class IncomeTransaction
{
    public int Id { get; set; }
    public DateTime TransactionDate { get; set; } = DateTime.Today;
    public int ShopId { get; set; }
    public int PaymentModeId { get; set; }
    public decimal Amount { get; set; }
    public string Note { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string PaymentModeName { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = "Credits";
    public string CategoryIconKey { get; set; } = "default_shop";
}
