using FinTracker.PageModels;
using FinTracker.Models;

namespace FinTracker.Pages;

public partial class MonthlyExpenseCalendarPage : ContentPage
{
    private bool _navigationPending;

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

    private async void OnTrendDaySelected(object? sender, DailyExpenseCalendarItem day)
    {
        if (_navigationPending) return;
        try
        {
            _navigationPending = true;
            var type = BindingContext is MonthlyExpenseCalendarPageModel model && model.TransactionType == FinancialTransactionType.Income ? "&type=Income" : string.Empty;
            await Shell.Current.GoToAsync($"dailybills?date={day.Date:yyyy-MM-dd}{type}");
        }
        finally { _navigationPending = false; }
    }

    private async void OnExpenseCategorySelected(object? sender, CategoryExpenseChartData category)
        => await NavigateToCategoryAsync(category, FinancialTransactionType.Expense);

    private async void OnIncomeCategorySelected(object? sender, CategoryExpenseChartData category)
        => await NavigateToCategoryAsync(category, FinancialTransactionType.Income);

    private async Task NavigateToCategoryAsync(CategoryExpenseChartData category, FinancialTransactionType transactionType)
    {
        if (_navigationPending || BindingContext is not MonthlyExpenseCalendarPageModel model) return;
        try
        {
            _navigationPending = true;
            var start = new DateTime(model.Year, model.Month, 1);
            var end = start.AddMonths(1);
            var name = Uri.EscapeDataString(category.CategoryName);
            await Shell.Current.GoToAsync($"dailybills?from={start:yyyy-MM-dd}&to={end:yyyy-MM-dd}&categoryId={category.CategoryId}&categoryName={name}&type={transactionType}");
        }
        finally { _navigationPending = false; }
    }
}
