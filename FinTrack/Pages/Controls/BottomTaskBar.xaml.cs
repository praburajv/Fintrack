namespace FinTracker.Pages.Controls;

public partial class BottomTaskBar : ContentView
{
    public BottomTaskBar() => InitializeComponent();

    private async void OnHomeClicked(object? sender, EventArgs e)
    {
        if (AppShell.IsClosing) return;
        await AppShell.NavigateToDashboardAsync();
    }

    private async void OnSetupClicked(object? sender, EventArgs e)
    {
        await AppShell.NavigateToSetupAsync();
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        if (Shell.Current is AppShell shell)
            await shell.CloseApplicationAsync();
    }
}
