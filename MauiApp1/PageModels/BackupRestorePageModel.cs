using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MauiApp1.PageModels;

public partial class BackupRestorePageModel : ObservableObject
{
    [RelayCommand]
    private async Task OpenBackup()
    {
        await Shell.Current.GoToAsync("backup");
    }

    [RelayCommand]
    private async Task OpenRestore()
    {
        await Shell.Current.GoToAsync("restore");
    }
}
