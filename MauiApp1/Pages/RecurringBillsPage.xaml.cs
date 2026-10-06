using MauiApp1.PageModels;
namespace MauiApp1.Pages;
public partial class RecurringBillsPage : ContentPage { public RecurringBillsPage(RecurringBillsPageModel model) { InitializeComponent(); BindingContext = model; } }
