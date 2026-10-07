using FinTracker.PageModels;

namespace FinTracker.Pages;

public partial class ReadOnlySqlAnalyzerPage : ContentPage
{
    public ReadOnlySqlAnalyzerPage(ReadOnlySqlAnalyzerPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
