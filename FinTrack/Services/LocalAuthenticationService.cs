using System.Security.Cryptography;
using FinTracker.Data;
using FinTracker.Models;
using Microsoft.Extensions.Logging;

namespace FinTracker.Services;

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

    public async Task<AuthenticationResult> AuthenticateLocalUserAfterBiometricAsync(int userId)
    {
        if (userId <= 0) return new(false, "The biometric account was not found.");
        var user = await accounts.GetByIdAsync(userId);
        if (user is null) return new(false, "The biometric account was not found.");

        _currentUser = user;
        _authenticationStateChanged.HandleEvent(this, EventArgs.Empty, nameof(AuthenticationStateChanged));
        return new(true, "", user);
    }

    public Task<IReadOnlyList<LocalUserSummary>> GetLocalUsersAsync() => accounts.ListUserSummariesAsync();

    public async Task<AuthenticationResult> ResetPasswordAsync(int userId, string newPassword)
    {
        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < MinimumPasswordLength)
            return new(false, $"Password must be at least {MinimumPasswordLength} characters.");

        if (userId <= 0) return new(false, "The selected local account was not found.");

        var user = await accounts.GetByIdAsync(userId);
        if (user is null) return new(false, "The selected local account was not found.");

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Derive(newPassword, salt);
        var rowsAffected = await accounts.UpdatePasswordAsync(userId, hash, salt);
        if (rowsAffected != 1)
        {
            logger.LogWarning("Local password reset did not update exactly one account: UserId = {UserId}, RowsAffected = {RowsAffected}", userId, rowsAffected);
            return new(false, "The password could not be changed.");
        }

        var updatedUser = await accounts.GetByIdAsync(userId);
        var hashChanged = updatedUser is not null && !CryptographicOperations.FixedTimeEquals(updatedUser.PasswordHash, user.PasswordHash);
        var saltChanged = updatedUser is not null && !CryptographicOperations.FixedTimeEquals(updatedUser.PasswordSalt, user.PasswordSalt);
        var storedCredentialsMatch = updatedUser is not null
            && CryptographicOperations.FixedTimeEquals(updatedUser.PasswordHash, hash)
            && CryptographicOperations.FixedTimeEquals(updatedUser.PasswordSalt, salt);
        logger.LogDebug("Local password reset verification: UserId = {UserId}, RowsAffected = {RowsAffected}, PasswordHashChanged = {HashChanged}, PasswordSaltChanged = {SaltChanged}, StoredCredentialsMatch = {StoredCredentialsMatch}", userId, rowsAffected, hashChanged, saltChanged, storedCredentialsMatch);
        if (!hashChanged || !saltChanged || !storedCredentialsMatch)
            return new(false, "The password could not be verified after saving.");

        return new(true, "Password changed successfully.");
    }

    public void Logout()
    {
        if (_currentUser is null) return;
        _currentUser = null;
        _authenticationStateChanged.HandleEvent(this, EventArgs.Empty, nameof(AuthenticationStateChanged));
    }

    private static byte[] Derive(string password, byte[] salt) => Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
}
