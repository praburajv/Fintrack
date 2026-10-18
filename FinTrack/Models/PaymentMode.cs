namespace FinTracker.Models;

public class PaymentMode
{
    public int Id { get; set; }
    public int FinancialAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public override string ToString() => Name;
}
