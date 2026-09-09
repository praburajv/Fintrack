using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class BackupRestorePage : ContentPage
{
    public BackupRestorePage(BackupRestorePageModel model) { InitializeComponent(); BindingContext = model; }
}
