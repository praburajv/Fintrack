using AndroidX.Biometric;
using AndroidX.Core.Content;
using Microsoft.Maui.ApplicationModel;
using FinTracker.Services;

namespace FinTracker.Platforms.Android;

public sealed class AndroidBiometricAuthenticationService : IBiometricAuthenticationService
{
    private const string EnabledKey = "biometric_login_enabled";
    private const string UserIdKey = "biometric_login_user_id";
    private int _promptActive;

    public Task<int?> GetConfiguredUserIdAsync() => GetConfiguredUserIdCoreAsync();

    private static async Task<int?> GetConfiguredUserIdCoreAsync()
    {
        var enabled = await SecureStorage.Default.GetAsync(EnabledKey);
        var id = await SecureStorage.Default.GetAsync(UserIdKey);
        return string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(id, out var userId) && userId > 0 ? userId : null;
    }

    public async Task SetConfiguredUserIdAsync(int userId)
    {
        await SecureStorage.Default.SetAsync(UserIdKey, userId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        await SecureStorage.Default.SetAsync(EnabledKey, "true");
    }

    public Task ClearConfigurationAsync()
    {
        SecureStorage.Default.Remove(EnabledKey);
        SecureStorage.Default.Remove(UserIdKey);
        return Task.CompletedTask;
    }

    public Task<BiometricAvailability> GetAvailabilityAsync()
    {
        var activity = Platform.CurrentActivity;
        if (activity is null) return Task.FromResult(BiometricAvailability.Unavailable);
        var result = BiometricManager.From(activity).CanAuthenticate(BiometricManager.Authenticators.BiometricWeak);
        return Task.FromResult(result switch
        {
            BiometricManager.BiometricSuccess => BiometricAvailability.Available,
            BiometricManager.BiometricErrorNoneEnrolled => BiometricAvailability.NoEnrollment,
            7 or 9 => BiometricAvailability.Lockout,
            _ => BiometricAvailability.Unavailable
        });
    }

    public async Task<BiometricAuthenticationResult> AuthenticateAsync()
    {
        if (Interlocked.Exchange(ref _promptActive, 1) == 1)
            return new(BiometricAuthenticationStatus.Canceled);

        try
        {
            var availability = await GetAvailabilityAsync();
            if (availability == BiometricAvailability.NoEnrollment)
                return new(BiometricAuthenticationStatus.Unavailable, "No fingerprint is currently configured on this device. Configure fingerprint authentication in Android Settings and try again.");
            if (availability == BiometricAvailability.Lockout)
                return new(BiometricAuthenticationStatus.LockedOut, "Fingerprint authentication is currently unavailable. Please login with your username and password.");
            if (availability != BiometricAvailability.Available)
                return new(BiometricAuthenticationStatus.Unavailable, "Fingerprint authentication is unavailable on this device.");

            var activity = Platform.CurrentActivity;
            if (activity is null) return new(BiometricAuthenticationStatus.Unavailable);
            var completion = new TaskCompletionSource<BiometricAuthenticationResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            var executor = ContextCompat.GetMainExecutor(activity) ?? throw new InvalidOperationException("The Android main executor is unavailable.");
            if (activity is not AndroidX.Fragment.App.FragmentActivity fragmentActivity)
                return new(BiometricAuthenticationStatus.Unavailable, "Fingerprint authentication is unavailable on this device.");
            var prompt = new BiometricPrompt(fragmentActivity, executor, new PromptCallback(completion));
            var info = new BiometricPrompt.PromptInfo.Builder()
                .SetTitle("Household Expense Tracker")
                .SetSubtitle("Verify your identity")
                .SetDescription("Use your fingerprint to access Household Expense Tracker.")
                .SetAllowedAuthenticators(BiometricManager.Authenticators.BiometricWeak)
                .SetNegativeButtonText("Cancel")
                .Build();
            prompt.Authenticate(info);
            return await completion.Task;
        }
        finally { Volatile.Write(ref _promptActive, 0); }
    }

    private sealed class PromptCallback(TaskCompletionSource<BiometricAuthenticationResult> completion) : BiometricPrompt.AuthenticationCallback
    {
        public override void OnAuthenticationSucceeded(BiometricPrompt.AuthenticationResult result) => completion.TrySetResult(new(BiometricAuthenticationStatus.Success));
        public override void OnAuthenticationFailed() { }
        public override void OnAuthenticationError(int errorCode, Java.Lang.ICharSequence? errString)
        {
            var status = errorCode is BiometricPrompt.ErrorLockout or BiometricPrompt.ErrorLockoutPermanent
                ? BiometricAuthenticationStatus.LockedOut
                : errorCode is BiometricPrompt.ErrorUserCanceled or BiometricPrompt.ErrorNegativeButton or BiometricPrompt.ErrorCanceled
                    ? BiometricAuthenticationStatus.Canceled
                    : BiometricAuthenticationStatus.Failed;
            completion.TrySetResult(new(status));
        }
    }
}
