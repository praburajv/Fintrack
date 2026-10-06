using MauiApp1.PageModels;
namespace MauiApp1.Pages;
public partial class RecurringBillEntryPage : ContentPage { public RecurringBillEntryPage(RecurringBillEntryPageModel model) { InitializeComponent(); BindingContext = model; } }
