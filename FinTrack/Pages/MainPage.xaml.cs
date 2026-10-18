using FinTracker.Models;
using FinTracker.PageModels;
using FinTracker.Pages.Controls;
using FinTracker.Services;

namespace FinTracker.Pages
{
    public partial class MainPage : ContentPage
    {
        private bool _categoryNavigationInProgress;
        private bool _monthNavigationInProgress;
        private bool _dayNavigationInProgress;
        private bool _analysisNavigationInProgress;
        private readonly ModalErrorHandler _errors;

        public MainPage(MainPageModel model, ModalErrorHandler errors)
        {
            _errors = errors;
            InitializeComponent();
            BindingContext = model;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // Keep the guard set while this page is covered by ShopBillDetailsPage.
            // It is reset only when the existing Dashboard instance is visible again.
            _categoryNavigationInProgress = false;
            _monthNavigationInProgress = false;
            _dayNavigationInProgress = false;
            _analysisNavigationInProgress = false;
        }

        private async void OnCategorySelected(object? sender, CategoryExpenseChartData category)
        {
            if (_categoryNavigationInProgress || BindingContext is not MainPageModel model || model.IsOverlayBusy)
                return;

            _categoryNavigationInProgress = true;
            model.ProcessingMessage = $"Opening {category.CategoryName} bills...";
            model.IsOpeningCategoryBills = true;
            await Task.Yield();
            try
            {
                var isCurrentMonth = sender is CategoryExpenseChart { IsCurrentMonth: true };
                var start = isCurrentMonth ? new DateTime(model.CurrentYear, model.CurrentMonth, 1) : new DateTime(model.CurrentYear, 1, 1);
                var end = isCurrentMonth ? start.AddMonths(1) : start.AddYears(1);
                var name = Uri.EscapeDataString(category.CategoryName);
                await Shell.Current.GoToAsync($"dailybills?from={start:yyyy-MM-dd}&to={end:yyyy-MM-dd}&categoryId={category.CategoryId}&categoryName={name}");
            }
            catch (Exception ex)
            {
                _errors.HandleError(ex);
                _categoryNavigationInProgress = false;
            }
            finally
            {
                model.IsOpeningCategoryBills = false;
                model.ProcessingMessage = "Loading analytics dashboard...";
            }
        }

        private async void OnCategoryLegendTapped(object? sender, CategoryExpenseChartData category)
        {
            try
            {
                await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("Category", category.CategoryName, "OK");
            }
            catch (Exception ex)
            {
                _errors.HandleError(ex);
            }
        }

        private async void OnMonthSelected(object? sender, MonthlyExpenseChartData month)
        {
            if (_monthNavigationInProgress || BindingContext is not MainPageModel model)
                return;

            _monthNavigationInProgress = true;
            try
            {
                await Shell.Current.GoToAsync($"monthlycalendar?year={model.CurrentYear}&month={month.MonthNumber}");
            }
            catch
            {
                _monthNavigationInProgress = false;
            }
        }

        private async void OnWeeklyDaySelected(object? sender, WeeklyExpenseChartData day)
        {
            if (_dayNavigationInProgress)
                return;

            _dayNavigationInProgress = true;
            try
            {
                await Shell.Current.GoToAsync($"dailybills?date={day.Date:yyyy-MM-dd}");
            }
            catch
            {
                _dayNavigationInProgress = false;
            }
        }

        private async void OnExpenseAnalysisClicked(object? sender, EventArgs e)
        {
            if (_analysisNavigationInProgress || BindingContext is not MainPageModel model || model.IsOverlayBusy)
                return;

            _analysisNavigationInProgress = true;
            model.ProcessingMessage = "Opening Expense Analysis...";
            model.IsOpeningAnalysis = true;
            await Task.Yield();
            try
            {
                var reference = new DateTime(model.CurrentYear, model.CurrentMonth, 1);
                await Shell.Current.GoToAsync($"expenseanalysis?reference={reference:yyyy-MM-dd}&period=Monthly");
            }
            catch (Exception ex)
            {
                _errors.HandleError(ex);
                _analysisNavigationInProgress = false;
            }
            finally
            {
                model.IsOpeningAnalysis = false;
                model.ProcessingMessage = "Loading analytics dashboard...";
            }
        }
    }
}
