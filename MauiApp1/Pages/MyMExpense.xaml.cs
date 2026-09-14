namespace MauiApp1.Pages;

public partial class MyMExpense : ContentPage
{
    private readonly ExpenseListPageModel _model;

    public MyMExpense(ExpenseListPageModel model)
    {
        InitializeComponent();
        _model = model;
        BindingContext = model;
    }

    protected override void OnAppearing()
    {
        _model.RefreshAuthorization();
        base.OnAppearing();
    }
}
