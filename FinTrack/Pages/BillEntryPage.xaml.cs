namespace FinTracker.Pages;

public partial class BillEntryPage : ContentPage
{
    private readonly BillEntryPageModel _model;

    public BillEntryPage(BillEntryPageModel model)
    {
        InitializeComponent();
        _model = model;
        BindingContext = model;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _model.EnsureAuthenticatedAsync();
    }
}
