using System.Collections;
using FinTracker.Services;
using FinTracker.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace FinTracker.Pages.Controls;

/// <summary>Touch opens every available item; typing filters without inventing a selected ID.</summary>
public sealed class SearchableSelector : ContentView
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(nameof(ItemsSource), typeof(IEnumerable), typeof(SearchableSelector), propertyChanged: Refresh);
    public static readonly BindableProperty SelectedItemProperty = BindableProperty.Create(nameof(SelectedItem), typeof(object), typeof(SearchableSelector), defaultBindingMode: BindingMode.TwoWay, propertyChanged: Refresh);
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(nameof(Placeholder), typeof(string), typeof(SearchableSelector), "Choose an item", propertyChanged: Refresh);
    private readonly Label _name = new() { LineBreakMode = LineBreakMode.WordWrap };
    private readonly Label _detail = new() { FontSize = 12, LineBreakMode = LineBreakMode.WordWrap };
    private bool _opening;
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public object? SelectedItem { get => GetValue(SelectedItemProperty); set => SetValue(SelectedItemProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public event EventHandler? SelectionChanged;

    public SearchableSelector()
    {
        _name.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
        _detail.SetDynamicResource(Label.TextColorProperty, "TextSecondary");
        var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, Padding = 12 };
        grid.Add(new VerticalStackLayout { Spacing = 3, InputTransparent = true, Children = { _name, _detail } });
        grid.Add(new Label { Text = "⌕  ▾", InputTransparent = true, VerticalOptions = LayoutOptions.Center }, 1);
        var hit = NavigationIcon.HitTarget("Open searchable list", async (_, _) => await OpenAsync());
        Grid.SetColumnSpan(hit, 2); grid.Add(hit);
        var border = new Border { Content = grid, Padding = 0, StrokeThickness = 1, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 } };
        border.SetDynamicResource(Border.StrokeProperty, "Primary");
        border.SetDynamicResource(BackgroundColorProperty, "SurfaceSecondary");
        Content = border;
        UpdateText();
    }

    private static void Refresh(BindableObject sender, object oldValue, object newValue)
    {
        var selector = (SearchableSelector)sender;
        selector.UpdateText();
        if (!ReferenceEquals(oldValue, newValue)) selector.SelectionChanged?.Invoke(selector, EventArgs.Empty);
    }

    private void UpdateText()
    {
        _name.Text = SelectedItem is null ? Placeholder : SelectionSearch.Name(SelectedItem);
        _detail.Text = SelectionSearch.Detail(SelectedItem);
        _detail.IsVisible = !string.IsNullOrWhiteSpace(_detail.Text);
        SemanticProperties.SetDescription(this, $"{Placeholder}: {_name.Text}");
    }

    private async Task OpenAsync()
    {
        if (_opening || Shell.Current is null) return;
        _opening = true;
        var accounts = Handler?.MauiContext?.Services.GetService<FinancialAccountContext>();
        var accountId = accounts?.Current?.Id;
        var original = SelectedItem;
        var accepted = false;
        var page = new ContentPage { Title = Placeholder };
        page.SetDynamicResource(BackgroundColorProperty, "Background");
        var search = new Entry { Placeholder = "Type to search", ClearButtonVisibility = ClearButtonVisibility.WhileEditing };
        SemanticProperties.SetDescription(search, "Filter the complete list");
        var empty = new Label { Text = "No matching items found.", IsVisible = false };
        var list = new CollectionView { SelectionMode = SelectionMode.Single, ItemSizingStrategy = ItemSizingStrategy.MeasureAllItems };
        list.ItemTemplate = new DataTemplate(() =>
        {
            var name = new Label { FontAttributes = FontAttributes.Bold, LineBreakMode = LineBreakMode.WordWrap };
            name.SetBinding(Label.TextProperty, nameof(Choice.Name));
            var detail = new Label { FontSize = 12, LineBreakMode = LineBreakMode.WordWrap };
            detail.SetBinding(Label.TextProperty, nameof(Choice.Detail));
            return new VerticalStackLayout { Padding = new Thickness(12, 10), Spacing = 3, Children = { name, detail } };
        });
        void Filter()
        {
            var choices = SelectionSearch.Filter(ItemsSource?.Cast<object>() ?? [], search.Text).Select(x => new Choice(x)).ToArray();
            list.ItemsSource = choices; empty.IsVisible = choices.Length == 0;
        }
        search.TextChanged += (_, _) => { SelectedItem = null; Filter(); };
        var closing = false;
        async Task Close()
        {
            if (closing) return;
            closing = true;
            if (page.Navigation.ModalStack.Contains(page)) await page.Navigation.PopModalAsync();
        }
        list.SelectionChanged += async (_, args) =>
        {
            if (args.CurrentSelection.FirstOrDefault() is not Choice choice || closing) return;
            if (accounts?.Current?.Id != accountId) { await Close(); return; }
            var current = ItemsSource?.Cast<object>().FirstOrDefault(x => SelectionSearch.SameItem(x, choice.Item));
            if (current is null) return;
            SelectedItem = current; accepted = true;
            await Close();
        };
        var cancel = new Button { Text = "Cancel" };
        cancel.Clicked += async (_, _) => await Close();
        var layout = new Grid { Padding = 16, RowSpacing = 10, RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) } };
        layout.Add(new Label { Text = Placeholder, FontSize = 22, FontAttributes = FontAttributes.Bold }, 0, 0);
        layout.Add(search, 0, 1); layout.Add(empty, 0, 2); layout.Add(list, 0, 3); layout.Add(cancel, 0, 4);
        page.Content = layout;
        void CompanyChanged(object? sender, EventArgs args) => MainThread.BeginInvokeOnMainThread(async () => { SelectedItem = null; await Close(); });
        void ItemsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) { if (args.PropertyName == nameof(ItemsSource)) Filter(); }
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        page.Disappearing += (_, _) => finished.TrySetResult();
        if (accounts is not null) accounts.Changed += CompanyChanged;
        PropertyChanged += ItemsChanged;
        try
        {
            Filter();
            await Shell.Current.Navigation.PushModalAsync(page);
            await finished.Task;
            if (!accepted && accounts?.Current?.Id == accountId)
                SelectedItem = ItemsSource?.Cast<object>().FirstOrDefault(x => SelectionSearch.SameItem(x, original));
        }
        catch (Exception ex)
        {
            if (accounts?.Current?.Id == accountId) SelectedItem = original;
            await ThemeDialog.AlertAsync("Selection", $"The list could not be opened: {ex.Message}");
        }
        finally
        {
            if (accounts is not null) accounts.Changed -= CompanyChanged;
            PropertyChanged -= ItemsChanged;
            _opening = false;
        }
    }

    private sealed record Choice(object Item)
    {
        public string Name => SelectionSearch.Name(Item);
        public string Detail => SelectionSearch.Detail(Item);
    }
}
