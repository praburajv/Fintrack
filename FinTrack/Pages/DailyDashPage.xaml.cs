using FinTracker.Models;
using FinTracker.PageModels;
using FinTracker.Pages.Controls;

namespace FinTracker.Pages;

public partial class DailyDashPage : ContentPage
{
    private bool _navigationInProgress;
    private bool _budgetNavigationInProgress;
    private bool _reportNavigationInProgress;
    private readonly ModalErrorHandler _errors;
    public DailyDashPage(DailyDashPageModel model, ModalErrorHandler errors) { _errors = errors; InitializeComponent(); BindingContext = model; }
    private async void OnPaymentModeSelected(object? sender, PaymentModeExpenseChartData item)
    {
        if (_navigationInProgress || sender is not PaymentModeExpenseChart chart) return;
        _navigationInProgress = true;
        try
        {
            var name = Uri.EscapeDataString(item.PaymentModeName);
            await Shell.Current.GoToAsync($"paymentmodebills?paymentModeId={item.PaymentModeId}&paymentModeName={name}&start={chart.StartDate:yyyy-MM-dd}&end={chart.EndDate:yyyy-MM-dd}");
        }
        catch { _navigationInProgress = false; }
    }
    protected override void OnAppearing() { base.OnAppearing(); _navigationInProgress = false; _budgetNavigationInProgress = false; _reportNavigationInProgress = false; }
    private async void OnBudgetClicked(object? sender, EventArgs e)
    {
        if (_budgetNavigationInProgress || BindingContext is not DailyDashPageModel model || model.IsOverlayBusy) return;

        _budgetNavigationInProgress = true;
        model.ProcessingMessage = "Opening Monthly Budget...";
        model.IsOpeningBudget = true;
        await Task.Yield();
        try
        {
            await Shell.Current.GoToAsync($"monthlybudget?reference={model.SelectedDate:yyyy-MM-dd}");
        }
        catch (Exception ex)
        {
            _errors.HandleError(ex);
            _budgetNavigationInProgress = false;
        }
        finally
        {
            model.IsOpeningBudget = false;
            model.ProcessingMessage = "Loading DailyDash...";
        }
    }
    private async void OnExpenseAnalysisClicked(object? sender, EventArgs e) => await Shell.Current.GoToAsync($"expenseanalysis?reference={DateTime.Today:yyyy-MM-dd}&period=Daily");
    private async void OnDailyReportClicked(object? sender, EventArgs e) => await OpenExpenseReportAsync(ExpensePeriodType.Daily);
    private async void OnWeeklyReportClicked(object? sender, EventArgs e) => await OpenExpenseReportAsync(ExpensePeriodType.Weekly);
    private async void OnMonthlyReportClicked(object? sender, EventArgs e) => await OpenExpenseReportAsync(ExpensePeriodType.Monthly);

    private async Task OpenExpenseReportAsync(ExpensePeriodType periodType)
    {
        if (_reportNavigationInProgress || BindingContext is not DailyDashPageModel model || model.IsOverlayBusy) return;

        _reportNavigationInProgress = true;
        model.ProcessingMessage = $"Opening {periodType} Expense Report...";
        model.IsOpeningReport = true;
        await Task.Yield();
        try
        {
            await Shell.Current.GoToAsync($"expensereport?reference={DateTime.Today:yyyy-MM-dd}&period={periodType}");
        }
        catch (Exception ex)
        {
            _errors.HandleError(ex);
            _reportNavigationInProgress = false;
        }
        finally
        {
            model.IsOpeningReport = false;
            model.ProcessingMessage = "Loading DailyDash...";
        }
    }
}
