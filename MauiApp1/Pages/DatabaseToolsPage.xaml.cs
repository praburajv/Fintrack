namespace MauiApp1.Pages;

public partial class DatabaseToolsPage : ContentPage
{
    public DatabaseToolsPage(DatabaseToolsPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
