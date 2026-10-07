using FinTracker.PageModels;
namespace FinTracker.Pages;
public partial class CategoryEntryPage : ContentPage { public CategoryEntryPage(CategoryEntryPageModel model) { InitializeComponent(); BindingContext = model; } }
