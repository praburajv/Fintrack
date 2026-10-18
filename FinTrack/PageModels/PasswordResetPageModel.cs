using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;

namespace FinTracker.PageModels;

public partial class PasswordResetPageModel(IAuthenticationService authentication, ModalErrorHandler errors) : ObservableObject
{
    [ObservableProperty] private List<LocalUserSummary> _users = [];
    [ObservableProperty] private LocalUserSummary? _selectedUser;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _retypePassword = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _isPassword = true;

    public bool HasUsers => Users.Count > 0;
    public bool NoUsers => !HasUsers;
    public string PasswordToggleText => IsPassword ? "Show password" : "Hide password";

    [RelayCommand]
    private async Task Appearing()
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            Users = (await authentication.GetLocalUsersAsync()).ToList();
            SelectedUser = Users.Count == 1 ? Users[0] : null;
            OnPropertyChanged(nameof(HasUsers));
            OnPropertyChanged(nameof(NoUsers));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void TogglePassword()
    {
        IsPassword = !IsPassword;
        OnPropertyChanged(nameof(PasswordToggleText));
    }

    [RelayCommand]
    private async Task ChangePassword()
    {
        if (IsBusy) return;
        if (SelectedUser is null || SelectedUser.Id <= 0)
        {
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Reset Password", "Select a local account.", "OK");
            return;
        }
        if (string.IsNullOrEmpty(NewPassword) || NewPassword.Length < LocalAuthenticationService.MinimumPasswordLength)
        {
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Reset Password", $"Password must be at least {LocalAuthenticationService.MinimumPasswordLength} characters.", "OK");
            return;
        }
        if (NewPassword != RetypePassword)
        {
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Reset Password", "Passwords do not match.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var username = SelectedUser.Username;
            var result = await authentication.ResetPasswordAsync(SelectedUser.Id, NewPassword);
            if (!result.Succeeded)
            {
                await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Reset Password", result.Message, "OK");
                return;
            }

            NewPassword = string.Empty;
            RetypePassword = string.Empty;
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Password Changed", $"The password for {username} has been changed successfully.", "Login");
            await Shell.Current.GoToAsync("..");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task BackToLogin() => Shell.Current.GoToAsync("..");

    [RelayCommand]
    private Task Register() => Shell.Current.GoToAsync("register");
}
