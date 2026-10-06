using MauiApp1.Utilities;

namespace MauiApp1.Models;

public sealed class RecurringBill
{
    public int Id { get; set; }
    public int ShopId { get; set; }
    public int PaymentModeId { get; set; }
    public decimal Amount { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime StartMonth { get; set; } = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    public DateTime? EndMonth { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string ShopName { get; set; } = string.Empty;
    public string PaymentModeName { get; set; } = string.Empty;
    public string AmountText => EuroFormatter.Format(Amount);
    public string StatusText => IsActive ? "Active • Monthly" : "Inactive • Monthly";
}
