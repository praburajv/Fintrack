using FinTracker.PageModels;
namespace FinTracker.Pages;
public partial class CategoryListPage : ContentPage { public CategoryListPage(CategoryListPageModel model) { InitializeComponent(); BindingContext = model; } }
