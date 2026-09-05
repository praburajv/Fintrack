namespace MauiApp1.Pages;

public partial class MyMExpense : ContentPage
{
    public MyMExpense(ExpenseListPageModel model)
    {
        InitializeComponent();
        BindingContext = model;
    }
}
