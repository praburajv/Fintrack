using MauiApp1.PageModels;
using MauiApp1.Models;
namespace MauiApp1.Pages;
public partial class ExpenseAnalysisPage : ContentPage
{
    private bool _navigationPending;
    private readonly ModalErrorHandler _errors;

    public ExpenseAnalysisPage(ExpenseAnalysisPageModel model, ModalErrorHandler errors) { _errors = errors; InitializeComponent(); BindingContext = model; }

    private async void OnTrendDaySelected(object? sender, DailyExpenseCalendarItem day)
    {
        if (_navigationPending) return;
        try
        {
            _navigationPending = true;
            await Shell.Current.GoToAsync($"dailybills?date={day.Date:yyyy-MM-dd}");
        }
        finally { _navigationPending = false; }
    }

    private async void OnCategorySelected(object? sender, CategoryExpenseChartData category)
    {
        if (_navigationPending || BindingContext is not ExpenseAnalysisPageModel model || model.IsOverlayBusy) return;
        _navigationPending = true;
        model.ProcessingMessage = $"Opening {category.CategoryName} bills...";
        model.IsOpeningCategoryBills = true;
        await Task.Yield();
        try
        {
            var name = Uri.EscapeDataString(category.CategoryName);
            await Shell.Current.GoToAsync($"dailybills?from={model.StartDate:yyyy-MM-dd}&to={model.EndExclusive:yyyy-MM-dd}&categoryId={category.CategoryId}&categoryName={name}");
        }
        catch (Exception ex)
        {
            _errors.HandleError(ex);
            _navigationPending = false;
        }
        finally
        {
            model.IsOpeningCategoryBills = false;
            model.ProcessingMessage = "Loading expense analysis...";
            _navigationPending = false;
        }
    }

    private async void OnShopSelected(object? sender, ShopExpenseChartData shop)
    {
        if (_navigationPending || BindingContext is not ExpenseAnalysisPageModel model || model.IsOverlayBusy) return;
        _navigationPending = true;
        try
        {
            await Shell.Current.GoToAsync($"dailybills?shopId={shop.ShopId}&date={model.StartDate:yyyy-MM-dd}&from={model.StartDate:yyyy-MM-dd}&to={model.EndExclusive:yyyy-MM-dd}");
        }
        finally { _navigationPending = false; }
    }
}
