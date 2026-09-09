using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class BackupDataPage : ContentPage
{
    public BackupDataPage(BackupDataPageModel model) { InitializeComponent(); BindingContext = model; }
}
