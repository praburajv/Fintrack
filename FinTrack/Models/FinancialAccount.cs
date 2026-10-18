namespace FinTracker.Models;

public sealed class FinancialAccount
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string LogoPath { get; set; } = string.Empty;
    public string ProfilePicturePath { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = "EUR";
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
}
