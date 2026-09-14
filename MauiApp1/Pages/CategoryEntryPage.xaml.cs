using MauiApp1.PageModels;
namespace MauiApp1.Pages;
public partial class CategoryEntryPage : ContentPage { public CategoryEntryPage(CategoryEntryPageModel model) { InitializeComponent(); BindingContext = model; } }
