using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;

namespace FinTracker.PageModels;

public partial class DatabaseToolsPageModel(UserAccountRepository accounts, IAuthenticationService authentication, ModalErrorHandler errors, ICriticalOperationState criticalOperations) : ObservableObject
{
    [ObservableProperty] private List<LocalUserSummary> _users = [];
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private double _progress;
    public string ProgressText => $"{Progress:0}%";

    public string DatabaseFilename => Constants.DatabaseFilename;
    public string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, Constants.DatabaseFilename);
    public int UserCount => Users.Count;

    [RelayCommand]
    private async Task Appearing() => await RefreshUsersAsync();

    [RelayCommand]
    private async Task ViewLocalUsers()
    {
        await RefreshUsersAsync();
        if (Users.Count == 0)
        {
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Local Users", "There are no registered local users.", "OK");
            return;
        }

        var details = string.Join(Environment.NewLine + Environment.NewLine, Users.Select(user =>
            $"ID: {user.Id}{Environment.NewLine}Name: {user.Name}{Environment.NewLine}Username: {user.Username}{Environment.NewLine}Created: {user.CreatedAt:G}"));
        await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Local Users", details, "OK");
    }

    [RelayCommand]
    private async Task DeleteAllLocalUsers()
    {
        if (IsBusy) return;
        await RefreshUsersAsync();
        if (Users.Count == 0)
        {
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Local Users", "There are no local users to delete.", "OK");
            return;
        }

        var confirmed = await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync(
            "Delete All Local Users?",
            "This will permanently remove all locally registered login accounts from this device.\n\nYour Bills, Shops, Payment Modes and expense history will NOT be deleted.\n\nYou will need to register a new local account before logging in again.",
            "Delete All Users",
            "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        Progress = 0;
        try
        {
            using var lease = criticalOperations.Begin();
            var deletedCount = await accounts.DeleteAllLocalUsersAsync();
            Progress = 100;
            OnPropertyChanged(nameof(ProgressText));
            if (deletedCount <= 0)
            {
                await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Unable to Delete Local Users", "No local user account was deleted.", "OK");
                return;
            }

            authentication.Logout();
            Users = [];
            OnPropertyChanged(nameof(UserCount));
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync(
                "Local Users Deleted",
                $"{deletedCount} local user account(s) were removed.\n\nAll locally registered users have been removed. Your Bills, Shops, Payment Modes and expense history were preserved. You can now register a new local account.",
                "OK");
        }
        catch (Exception ex)
        {
            errors.HandleError(ex);
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Unable to Delete Local Users", "No changes were completed.", "OK");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task DeleteUser(LocalUserSummary? user)
    {
        if (user is null || IsBusy) return;
        var confirmed = await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync(
            "Delete Local User?",
            $"Delete the local user '{user.Name}' ({user.Username})?\n\nThis removes only this login account. Bills and expense history are preserved.",
            "Delete", "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        Progress = 0;
        try
        {
            using var lease = criticalOperations.Begin();
            var deleted = await accounts.DeleteLocalUserAsync(user.Id);
            Progress = 100;
            OnPropertyChanged(nameof(ProgressText));
            if (deleted == 0)
            {
                await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("User Not Found", "The selected user no longer exists.", "OK");
                return;
            }

            if (authentication.CurrentUser?.Id == user.Id)
                authentication.Logout();
            Users = (await accounts.ListUserSummariesAsync()).ToList();
            OnPropertyChanged(nameof(UserCount));
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("User Deleted", $"Local user '{user.Name}' was deleted.", "OK");
        }
        catch (Exception ex)
        {
            errors.HandleError(ex);
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Unable to Delete User", "No changes were completed.", "OK");
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task RegisterNewUser() => Shell.Current.GoToAsync("register");

    private async Task RefreshUsersAsync()
    {
        if (IsBusy) return;
        try
        {
            IsBusy = true;
            Users = (await accounts.ListUserSummariesAsync()).ToList();
            OnPropertyChanged(nameof(UserCount));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; }
    }
}
