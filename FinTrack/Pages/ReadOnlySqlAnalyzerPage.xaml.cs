using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class ReadOnlySqlAnalyzerPage : ContentPage
{
    public ReadOnlySqlAnalyzerPage(ReadOnlySqlAnalyzerPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (BindingContext is ReadOnlySqlAnalyzerPageModel model) await model.RefreshTableStructuresAsync();
    }
}
