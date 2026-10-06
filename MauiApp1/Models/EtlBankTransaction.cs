namespace MauiApp1.Models;

public sealed class EtlBankTransaction
{
    public DateTime BillDate { get; init; }
    public string Description { get; init; } = string.Empty;
    public decimal DebitAmount { get; init; }
    public decimal CreditAmount { get; init; }
    public bool IsIncome => CreditAmount > 0;
}
