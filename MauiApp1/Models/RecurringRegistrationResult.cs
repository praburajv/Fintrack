namespace MauiApp1.Models;

public sealed record RecurringRegistrationItem(RecurringBill Bill, bool Created);
public sealed record RecurringRegistrationResult(IReadOnlyList<RecurringRegistrationItem> Items)
{
    public int CreatedCount => Items.Count(x => x.Created);
    public int ExistingCount => Items.Count(x => !x.Created);
    public decimal CreatedTotal => Items.Where(x => x.Created).Sum(x => x.Bill.Amount);
    public bool IsComplete => Items.Count > 0 && Items.All(x => !x.Created);
}
