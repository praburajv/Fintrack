using MauiApp1.PageModels;

namespace MauiApp1.Pages;

public partial class ReadOnlySqlAnalyzerPage : ContentPage
{
    public ReadOnlySqlAnalyzerPage(ReadOnlySqlAnalyzerPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
