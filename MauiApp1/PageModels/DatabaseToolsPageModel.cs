using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;

namespace MauiApp1.PageModels;

public partial class DatabaseToolsPageModel(UserAccountRepository accounts, IAuthenticationService authentication, ModalErrorHandler errors) : ObservableObject
{
    [ObservableProperty] private List<LocalUserSummary> _users = [];
    [ObservableProperty] private bool _isBusy;

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
            await Shell.Current.DisplayAlertAsync("Local Users", "There are no registered local users.", "OK");
            return;
        }

        var details = string.Join(Environment.NewLine + Environment.NewLine, Users.Select(user =>
            $"ID: {user.Id}{Environment.NewLine}Name: {user.Name}{Environment.NewLine}Username: {user.Username}{Environment.NewLine}Created: {user.CreatedAt:G}"));
        await Shell.Current.DisplayAlertAsync("Local Users", details, "OK");
    }

    [RelayCommand]
    private async Task DeleteAllLocalUsers()
    {
        if (IsBusy) return;
        await RefreshUsersAsync();
        if (Users.Count == 0)
        {
            await Shell.Current.DisplayAlertAsync("Local Users", "There are no local users to delete.", "OK");
            return;
        }

        var confirmed = await Shell.Current.DisplayAlertAsync(
            "Delete All Local Users?",
            "This will permanently remove all locally registered login accounts from this device.\n\nYour Bills, Shops, Payment Modes and expense history will NOT be deleted.\n\nYou will need to register a new local account before logging in again.",
            "Delete All Users",
            "Cancel");
        if (!confirmed) return;

        IsBusy = true;
        try
        {
            var deletedCount = await accounts.DeleteAllLocalUsersAsync();
            if (deletedCount <= 0)
            {
                await Shell.Current.DisplayAlertAsync("Unable to Delete Local Users", "No local user account was deleted.", "OK");
                return;
            }

            authentication.Logout();
            Users = [];
            OnPropertyChanged(nameof(UserCount));
            await Shell.Current.DisplayAlertAsync(
                "Local Users Deleted",
                $"{deletedCount} local user account(s) were removed.\n\nAll locally registered users have been removed. Your Bills, Shops, Payment Modes and expense history were preserved. You can now register a new local account.",
                "OK");
        }
        catch (Exception ex)
        {
            errors.HandleError(ex);
            await Shell.Current.DisplayAlertAsync("Unable to Delete Local Users", "No changes were completed.", "OK");
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
