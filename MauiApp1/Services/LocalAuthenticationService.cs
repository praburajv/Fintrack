using System.Security.Cryptography;
using MauiApp1.Data;
using MauiApp1.Models;
using Microsoft.Extensions.Logging;

namespace MauiApp1.Services;

public sealed class LocalAuthenticationService(UserAccountRepository accounts, ILogger<LocalAuthenticationService> logger) : IAuthenticationService
{
    public const int MinimumPasswordLength = 8;
    private const int SaltSize = 16;
    private const int HashSize = 32;
    private const int Iterations = 120_000;
    private readonly WeakEventManager _authenticationStateChanged = new();
    private UserAccount? _currentUser;

    public bool IsAuthenticated => _currentUser is not null;
    public UserAccount? CurrentUser => _currentUser;
    public event EventHandler? AuthenticationStateChanged
    {
        add => _authenticationStateChanged.AddEventHandler(value);
        remove => _authenticationStateChanged.RemoveEventHandler(value);
    }

    public async Task<AuthenticationResult> RegisterAsync(string name, string username, string password)
    {
        if (await accounts.HasAnyAccountAsync()) return new(false, "A local account already exists.");
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(password, salt);
        try
        {
            var user = await accounts.CreateAsync(name, username, hash, salt);
            return new(true, "Account created. You can now log in.", user);
        }
        catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            return new(false, "Username already exists.");
        }
    }

    public async Task<AuthenticationResult> LoginAsync(string username, string password)
    {
        var user = await accounts.GetByUsernameAsync(username);
        logger.LogDebug("Local login lookup: user found = {UserFound}, stored hash exists = {HashExists}, stored salt exists = {SaltExists}", user is not null, user?.PasswordHash.Length > 0, user?.PasswordSalt.Length > 0);
        if (user is null) return new(false, "Invalid username or password.");
        var candidate = Derive(password, user.PasswordSalt);
        var verified = CryptographicOperations.FixedTimeEquals(candidate, user.PasswordHash);
        logger.LogDebug("Local login password verification result = {Verified}", verified);
        if (!verified) return new(false, "Invalid username or password.");
        _currentUser = user;
        _authenticationStateChanged.HandleEvent(this, EventArgs.Empty, nameof(AuthenticationStateChanged));
        return new(true, "", user);
    }

    public void Logout()
    {
        if (_currentUser is null) return;
        _currentUser = null;
        _authenticationStateChanged.HandleEvent(this, EventArgs.Empty, nameof(AuthenticationStateChanged));
    }

    private static byte[] Derive(string password, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
}
