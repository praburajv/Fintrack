using FinTracker.Models;

namespace FinTracker.Services;

public interface IAuthenticationService
{
    bool IsAuthenticated { get; }
    UserAccount? CurrentUser { get; }
    event EventHandler? AuthenticationStateChanged;
    Task<AuthenticationResult> RegisterAsync(string name, string username, string password);
    Task<AuthenticationResult> LoginAsync(string username, string password);
    Task<AuthenticationResult> AuthenticateLocalUserAfterBiometricAsync(int userId);
    Task<IReadOnlyList<LocalUserSummary>> GetLocalUsersAsync();
    Task<AuthenticationResult> ResetPasswordAsync(int userId, string newPassword);
    void Logout();
}

public sealed record AuthenticationResult(bool Succeeded, string Message, UserAccount? User = null);
