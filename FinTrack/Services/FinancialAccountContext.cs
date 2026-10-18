using FinTracker.Models;

namespace FinTracker.Services;

/// <summary>The selected company for this authenticated app session.</summary>
public sealed class FinancialAccountContext(ICurrencyService currencies)
{
    private FinancialAccount? _current;
    public FinancialAccount? Current => Volatile.Read(ref _current);
    public int RequireId() => Current?.Id ?? throw new InvalidOperationException("Select a financial account before continuing.");
    public event EventHandler? Changed;

    public void Select(FinancialAccount account)
    {
        ArgumentNullException.ThrowIfNull(account);
        if (account.Id <= 0 || !account.IsActive) throw new InvalidOperationException("This financial account is not available.");
        Volatile.Write(ref _current, account);
        currencies.ApplyAccountCurrency(account.CurrencyCode);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        Volatile.Write(ref _current, null);
        currencies.ClearAccountCurrency();
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
