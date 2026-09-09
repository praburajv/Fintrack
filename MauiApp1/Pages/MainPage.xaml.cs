using MauiApp1.Models;
using MauiApp1.PageModels;
using MauiApp1.Pages.Controls;
using MauiApp1.Services;

namespace MauiApp1.Pages
{
    public partial class MainPage : ContentPage
    {
        private bool _shopNavigationInProgress;
        private bool _monthNavigationInProgress;
        private bool _dayNavigationInProgress;
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
            _shopNavigationInProgress = false;
            _monthNavigationInProgress = false;
            _dayNavigationInProgress = false;
        }

        private async void OnShopSelected(object? sender, ShopExpenseChartData shop)
        {
            if (_shopNavigationInProgress || BindingContext is not MainPageModel model)
                return;

            _shopNavigationInProgress = true;
            try
            {
                var chart = (ShopExpenseChart)sender!;
                var filter = chart.IsCurrentMonth ? $"&month={model.CurrentMonth}" : string.Empty;
                await Shell.Current.GoToAsync($"shopbills?shopId={shop.ShopId}&year={model.CurrentYear}{filter}&shopName={Uri.EscapeDataString(shop.ShopName)}");
            }
            catch
            {
                // Permit retry if navigation itself fails. On success the guard
                // remains set until Dashboard reappears after a normal back.
                _shopNavigationInProgress = false;
            }
        }

        private async void OnShopLegendTapped(object? sender, ShopExpenseChartData shop)
        {
            try
            {
                await DisplayAlertAsync("Shop", shop.ShopName, "OK");
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
    }
}
