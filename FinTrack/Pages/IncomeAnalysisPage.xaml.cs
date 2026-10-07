using FinTracker.Pages.Controls;

namespace FinTracker.Pages;

public partial class IncomeAnalysisPage : ContentPage
{
    public IncomeAnalysisPage(PageModels.IncomeAnalysisPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
        if (Content is ScrollView scroll && scroll.Content is VerticalStackLayout stack)
        {
            var category = new CategoryExpenseChart { HeightRequest = 260 };
            category.SetBinding(CategoryExpenseChart.ItemsSourceProperty, nameof(PageModels.IncomeAnalysisPageModel.CategoryChartData));
            category.CategorySelected += OnCategorySelected;
            var monthly = new MonthlyExpenseChart { HeightRequest = 260 };
            monthly.SetBinding(MonthlyExpenseChart.ItemsSourceProperty, nameof(PageModels.IncomeAnalysisPageModel.MonthlyData));
            monthly.MonthSelected += OnMonthSelected;
            var weekly = new WeeklyExpenseChart { HeightRequest = 260 };
            weekly.SetBinding(WeeklyExpenseChart.ItemsSourceProperty, nameof(PageModels.IncomeAnalysisPageModel.WeeklyData));
            weekly.DaySelected += OnWeekDaySelected;
            stack.Add(new Label { Text = "Income by Category", Style = (Style)Application.Current!.Resources["Title2"] });
            stack.Add(category);
            stack.Add(new Label { Text = "Monthly Income", Style = (Style)Application.Current!.Resources["Title2"] });
            stack.Add(monthly);
            stack.Add(new Label { Text = "Current Week Income", Style = (Style)Application.Current!.Resources["Title2"] });
            stack.Add(weekly);
            var overlay = new ProcessingOverlay();
            overlay.SetBinding(ProcessingOverlay.IsBusyProperty, nameof(PageModels.IncomeAnalysisPageModel.IsBusy));
            overlay.SetBinding(ProcessingOverlay.MessageProperty, nameof(PageModels.IncomeAnalysisPageModel.ProcessingMessage));
            Content = new Grid { Children = { scroll, overlay } };
        }
    }

    private async void OnCategorySelected(object? sender, Models.CategoryExpenseChartData category)
    {
        if (BindingContext is not PageModels.IncomeAnalysisPageModel model) return;
        var start = new DateTime(model.ReferenceDate.Year, 1, 1);
        await Shell.Current.GoToAsync($"dailybills?from={start:yyyy-MM-dd}&to={start.AddYears(1):yyyy-MM-dd}&categoryId={category.CategoryId}&categoryName={Uri.EscapeDataString(category.CategoryName)}&type=Income");
    }

    private async void OnMonthSelected(object? sender, Models.MonthlyExpenseChartData month)
    {
        if (BindingContext is PageModels.IncomeAnalysisPageModel model)
            await Shell.Current.GoToAsync($"monthlycalendar?year={model.ReferenceDate.Year}&month={month.MonthNumber}&type=Income");
    }

    private async void OnWeekDaySelected(object? sender, Models.WeeklyExpenseChartData day)
        => await Shell.Current.GoToAsync($"dailybills?date={day.Date:yyyy-MM-dd}&type=Income");
}
