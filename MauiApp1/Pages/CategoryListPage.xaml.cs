using MauiApp1.PageModels;
namespace MauiApp1.Pages;
public partial class CategoryListPage : ContentPage { public CategoryListPage(CategoryListPageModel model) { InitializeComponent(); BindingContext = model; } }
