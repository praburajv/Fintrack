using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class BackupRestorePage : ContentPage
{
    public BackupRestorePage(BackupRestorePageModel model) { InitializeComponent(); BindingContext = model; }
}
