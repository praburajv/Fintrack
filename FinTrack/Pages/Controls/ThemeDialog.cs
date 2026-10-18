namespace FinTracker.Pages.Controls;

/// <summary>Theme-aware modal with a bordered title and explicit actions.</summary>
public static class ThemeDialog
{
    // Compatibility overloads let existing alert/confirmation call sites use the
    // same themed modal without changing their accepted/cancelled behavior.
    public static Task DisplayAlertAsync(string title, string message, string cancel)
        => AlertAsync(title, message, cancel);

    public static Task<bool> DisplayAlertAsync(string title, string message, string accept, string cancel)
        => ConfirmAsync(title, message, accept, cancel);

    public static async Task AlertAsync(string title, string message, string action = "OK")
        => await ShowAsync(title, message, null, [(action, action)]);

    public static async Task<bool> ConfirmAsync(string title, string message, string accept, string cancel = "Cancel")
        => await ShowAsync(title, message, null, [(cancel, "cancel"), (accept, "accept")]) == "accept";

    public static async Task<string?> PromptAsync(string title, string message, string? initialValue = null, int maxLength = 250)
    {
        var entry = new Entry { Text = initialValue ?? string.Empty, MaxLength = maxLength, ClearButtonVisibility = ClearButtonVisibility.WhileEditing };
        var result = await ShowAsync(title, message, entry, [("Cancel", "cancel"), ("Continue", "accept")]);
        return result == "accept" ? entry.Text : null;
    }

    public static async Task<string?> ChooseAsync(string title, string message, IReadOnlyList<string> options)
    {
        var picker = new Picker { Title = "Choose an option", ItemsSource = options.ToArray() };
        var result = await ShowAsync(title, message, picker, [("Cancel", "cancel"), ("Select", "accept")]);
        return result == "accept" && picker.SelectedIndex >= 0 ? options[picker.SelectedIndex] : null;
    }

    private static async Task<string?> ShowAsync(string title, string message, View? input, IReadOnlyList<(string Text, string Value)> actions)
    {
        if (!MainThread.IsMainThread)
            return await MainThread.InvokeOnMainThreadAsync(() => ShowAsync(title, message, input, actions));
        var host = Shell.Current ?? throw new InvalidOperationException("The app shell is not available.");
        var page = new DialogPage(title, message, input, actions);
        await host.Navigation.PushModalAsync(page, false);
        return await page.Completion;
    }

    private sealed class DialogPage : ContentPage
    {
        private readonly TaskCompletionSource<string?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool _closing;
        public Task<string?> Completion => _completion.Task;

        public DialogPage(string title, string message, View? input, IReadOnlyList<(string Text, string Value)> actions)
        {
            NavigationPage.SetHasNavigationBar(this, false);
            BackgroundColor = Color.FromArgb("#99000000");
            var heading = new Label { Text = title, FontSize = 20, FontAttributes = FontAttributes.Bold,
                HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
            heading.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
            var headingBorder = new Border { Content = heading, Padding = new Thickness(12, 11), StrokeThickness = 2,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 } };
            headingBorder.SetDynamicResource(Border.StrokeProperty, "Primary");
            headingBorder.SetDynamicResource(Border.BackgroundColorProperty, "SurfaceSecondary");
            var body = new Label { Text = message, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
            body.SetDynamicResource(Label.TextColorProperty, "TextPrimary");
            var actionsRow = new HorizontalStackLayout { Spacing = 9, HorizontalOptions = LayoutOptions.End };
            foreach (var (text, value) in actions)
            {
                var button = new Button { Text = text, MinimumWidthRequest = 85, Padding = new Thickness(10, 8) };
                if (value == "cancel") { button.SetDynamicResource(Button.BackgroundColorProperty, "SurfaceSecondary"); button.SetDynamicResource(Button.TextColorProperty, "TextPrimary"); }
                button.Clicked += async (_, _) => await CloseAsync(value);
                actionsRow.Children.Add(button);
            }
            var stack = new VerticalStackLayout { Spacing = 17, Padding = new Thickness(16), Children = { headingBorder, body } };
            if (input is not null) stack.Children.Add(input);
            stack.Children.Add(actionsRow);
            var scrollingContent = new ScrollView { Content = stack, MaximumHeightRequest = DeviceDisplay.MainDisplayInfo.Height / DeviceDisplay.MainDisplayInfo.Density * 0.8 };
            var card = new Border { Content = scrollingContent, MaximumWidthRequest = 420, HorizontalOptions = LayoutOptions.Fill,
                StrokeThickness = 1, StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 } };
            card.SetDynamicResource(Border.BackgroundColorProperty, "Surface");
            card.SetDynamicResource(Border.StrokeProperty, "Primary");
            Content = new Grid { Padding = new Thickness(16), Children = { card } };
            card.VerticalOptions = LayoutOptions.Center;
        }

        protected override bool OnBackButtonPressed()
        {
            _ = CloseAsync(null);
            return true;
        }

        private async Task CloseAsync(string? result)
        {
            if (_closing) return;
            _closing = true;
            try { await Navigation.PopModalAsync(false); }
            finally { _completion.TrySetResult(result); }
        }
    }
}
