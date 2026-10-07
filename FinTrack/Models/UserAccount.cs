namespace FinTracker.Models;

public sealed class UserAccount
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
    public byte[] PasswordHash { get; init; } = [];
    public byte[] PasswordSalt { get; init; } = [];
    public DateTime CreatedAt { get; init; }
}
