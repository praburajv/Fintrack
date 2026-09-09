using CommunityToolkit.Mvvm.ComponentModel;

namespace MauiApp1.PageModels;

public partial class BackupRestorePageModel : ObservableObject
{
    public Task OpenBackupAsync() => Shell.Current.GoToAsync("backup");
    public Task OpenRestoreAsync() => Shell.Current.GoToAsync("restore");
}
