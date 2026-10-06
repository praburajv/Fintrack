using MauiApp1.Models;

namespace MauiApp1.Pages;

public partial class MyMExpense : ContentPage, IQueryAttributable
{
    private readonly ExpenseListPageModel _model;

    public MyMExpense(ExpenseListPageModel model)
    {
        InitializeComponent();
        _model = model;
        BindingContext = model;
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("mode", out var value) && Enum.TryParse<ExpenseListMode>(value?.ToString(), true, out var mode))
            _model.SetAccessMode(mode);
        query.TryGetValue("year", out var yearValue);
        query.TryGetValue("month", out var monthValue);
        if (int.TryParse(yearValue?.ToString(), out var year)
            && int.TryParse(monthValue?.ToString(), out var month)
            && month is >= 1 and <= 12)
            _model.RequestSpecificMonth(year, month);
    }

    protected override void OnAppearing()
    {
        _model.RefreshAuthorization();
        base.OnAppearing();
    }

}
