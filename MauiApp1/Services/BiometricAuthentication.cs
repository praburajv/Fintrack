namespace MauiApp1.Services;

public enum BiometricAvailability
{
    Available,
    NoEnrollment,
    Unavailable,
    Lockout
}

public enum BiometricAuthenticationStatus
{
    Success,
    Failed,
    Canceled,
    Unavailable,
    LockedOut
}

public sealed record BiometricAuthenticationResult(BiometricAuthenticationStatus Status, string Message = "");

public interface IBiometricAuthenticationService
{
    Task<BiometricAvailability> GetAvailabilityAsync();
    Task<BiometricAuthenticationResult> AuthenticateAsync();
    Task<int?> GetConfiguredUserIdAsync();
    Task SetConfiguredUserIdAsync(int userId);
    Task ClearConfigurationAsync();
}

public sealed class DefaultBiometricAuthenticationService : IBiometricAuthenticationService
{
    public Task<BiometricAvailability> GetAvailabilityAsync() => Task.FromResult(BiometricAvailability.Unavailable);
    public Task<BiometricAuthenticationResult> AuthenticateAsync() => Task.FromResult(new BiometricAuthenticationResult(BiometricAuthenticationStatus.Unavailable));
    public Task<int?> GetConfiguredUserIdAsync() => Task.FromResult<int?>(null);
    public Task SetConfiguredUserIdAsync(int userId) => Task.CompletedTask;
    public Task ClearConfigurationAsync() => Task.CompletedTask;
}
