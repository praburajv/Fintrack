using FinTracker.PageModels;
namespace FinTracker.Pages;
public partial class SetupDataPage : ContentPage
{
    public SetupDataPage(SetupDataPageModel model) { InitializeComponent(); BindingContext = model; }
    private void SelectTab(object? sender, EventArgs e) { if (sender is Button button && BindingContext is SetupDataPageModel model) model.SelectedTab = button.Text; }
}
