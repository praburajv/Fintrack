using FinTracker.PageModels;
namespace FinTracker.Pages;
public partial class RecurringBillEntryPage : ContentPage { public RecurringBillEntryPage(RecurringBillEntryPageModel model) { InitializeComponent(); BindingContext = model; } }
