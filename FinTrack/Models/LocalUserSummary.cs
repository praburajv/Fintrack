namespace FinTracker.Models;

public sealed class LocalUserSummary
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
