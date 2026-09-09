using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class MonthlyExpenseCalendarPage : ContentPage
{
    public MonthlyExpenseCalendarPage(MonthlyExpenseCalendarPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    private static void OnTileSizeChanged(object? sender, EventArgs e)
    {
        if (sender is Border tile && tile.Width > 0)
        {
            var desiredHeight = tile.Width;
            if (Math.Abs(tile.HeightRequest - desiredHeight) > 0.5)
            {
                tile.HeightRequest = desiredHeight;
            }
        }
    }
}
