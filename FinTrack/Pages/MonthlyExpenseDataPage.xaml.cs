namespace FinTracker.Pages;

public partial class MonthlyExpenseDataPage : ContentPage
{
    private readonly PageModels.MonthlyExpenseDataPageModel _model;
    public MonthlyExpenseDataPage(PageModels.MonthlyExpenseDataPageModel model)
    {
        InitializeComponent();
        _model = model;
        BindingContext = model;
    }

    protected override void OnDisappearing()
    {
        // Leaving during review cancels the pending choice and releases its
        // command gate; it does not interrupt an already-confirmed transaction.
        _model.CancelDeletionReviewCommand.Execute(null);
        base.OnDisappearing();
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_model.IsBusy) return base.OnBackButtonPressed();
        if (_model.IsAwaitingDeletionChoice) _model.CancelDeletionReviewCommand.Execute(null);
        return true;
    }
}
