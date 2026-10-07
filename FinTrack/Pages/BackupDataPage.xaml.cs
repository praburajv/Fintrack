using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class BackupDataPage : ContentPage
{
    public BackupDataPage(BackupDataPageModel model) { InitializeComponent(); BindingContext = model; }
}
