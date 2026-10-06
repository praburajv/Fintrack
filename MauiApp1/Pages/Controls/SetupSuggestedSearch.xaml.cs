using System.Windows.Input;
using MauiApp1.Models;

namespace MauiApp1.Pages.Controls;

public partial class SetupSuggestedSearch : ContentView
{
    public static readonly BindableProperty SearchTextProperty = BindableProperty.Create(
        nameof(SearchText), typeof(string), typeof(SetupSuggestedSearch), string.Empty,
        BindingMode.TwoWay, propertyChanged: OnSearchChanged);
    public static readonly BindableProperty SuggestionsProperty = BindableProperty.Create(
        nameof(Suggestions), typeof(IReadOnlyList<SetupSearchSuggestion>), typeof(SetupSuggestedSearch),
        defaultValue: Array.Empty<SetupSearchSuggestion>(), propertyChanged: OnSuggestionsChanged);
    public static readonly BindableProperty SelectionCommandProperty = BindableProperty.Create(
        nameof(SelectionCommand), typeof(ICommand), typeof(SetupSuggestedSearch));
    public static readonly BindableProperty PlaceholderProperty = BindableProperty.Create(
        nameof(Placeholder), typeof(string), typeof(SetupSuggestedSearch), "Search");

    private bool _suggestionSelected;
    public string SearchText { get => (string)GetValue(SearchTextProperty); set => SetValue(SearchTextProperty, value); }
    public IReadOnlyList<SetupSearchSuggestion> Suggestions { get => (IReadOnlyList<SetupSearchSuggestion>)GetValue(SuggestionsProperty); set => SetValue(SuggestionsProperty, value); }
    public ICommand? SelectionCommand { get => (ICommand?)GetValue(SelectionCommandProperty); set => SetValue(SelectionCommandProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public bool HasSuggestions => !_suggestionSelected && !string.IsNullOrWhiteSpace(SearchText) && Suggestions.Count > 0;
    public bool ShowNoMatches => !string.IsNullOrWhiteSpace(SearchText) && Suggestions.Count == 0;

    public SetupSuggestedSearch() => InitializeComponent();

    private static void OnSearchChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var control = (SetupSuggestedSearch)bindable;
        control._suggestionSelected = false;
        control.NotifySuggestionsChanged();
    }

    private static void OnSuggestionsChanged(BindableObject bindable, object oldValue, object newValue)
        => ((SetupSuggestedSearch)bindable).NotifySuggestionsChanged();

    private void NotifySuggestionsChanged()
    {
        OnPropertyChanged(nameof(HasSuggestions));
        OnPropertyChanged(nameof(ShowNoMatches));
    }

    private void OnSuggestionTapped(object? sender, TappedEventArgs e)
    {
        if (e.Parameter is not SetupSearchSuggestion suggestion) return;
        if (SelectionCommand?.CanExecute(suggestion) != true) return;
        SelectionCommand.Execute(suggestion);
        _suggestionSelected = true;
        NotifySuggestionsChanged();
    }
}
