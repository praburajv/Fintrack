using FinTracker.PageModels;
namespace FinTracker.Pages;
public partial class RecurringBillsPage : ContentPage { public RecurringBillsPage(RecurringBillsPageModel model) { InitializeComponent(); BindingContext = model; } }
