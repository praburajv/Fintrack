using System.Windows.Input;

namespace FinTracker.Pages.Controls;

/// <summary>Reusable header actions. Shell supplies the native Menu/Back slot.</summary>
public sealed class AppBar : ContentView
{
    public static readonly BindableProperty AppNameProperty = BindableProperty.Create(nameof(AppName), typeof(string), typeof(AppBar), "FinTracker");
    public static readonly BindableProperty CompanyNameProperty = BindableProperty.Create(nameof(CompanyName), typeof(string), typeof(AppBar), string.Empty);
    public static readonly BindableProperty LogoSourceProperty = BindableProperty.Create(nameof(LogoSource), typeof(ImageSource), typeof(AppBar), ImageSource.FromFile("appicon_runtime.png"));
    public static readonly BindableProperty ShowMenuProperty = Flag(nameof(ShowMenu));
    public static readonly BindableProperty ShowSearchProperty = Flag(nameof(ShowSearch));
    public static readonly BindableProperty ShowNotificationProperty = Flag(nameof(ShowNotification));
    public static readonly BindableProperty ShowUserProfileProperty = Flag(nameof(ShowUserProfile));
    public static readonly BindableProperty NotificationCountProperty = BindableProperty.Create(nameof(NotificationCount), typeof(int), typeof(AppBar), 0, propertyChanged: (b, _, _) => ((AppBar)b).UpdateBadge());
    public static readonly BindableProperty UserInitialsProperty = BindableProperty.Create(nameof(UserInitials), typeof(string), typeof(AppBar), "FT");
    public static readonly BindableProperty ProfileImageSourceProperty = BindableProperty.Create(nameof(ProfileImageSource), typeof(ImageSource), typeof(AppBar), null, propertyChanged: (b, _, value) => ((AppBar)b)._profileImage.IsVisible = value is not null);
    public static readonly BindableProperty MenuCommandProperty = CommandProperty(nameof(MenuCommand));
    public static readonly BindableProperty SearchCommandProperty = CommandProperty(nameof(SearchCommand));
    public static readonly BindableProperty NotificationCommandProperty = CommandProperty(nameof(NotificationCommand));
    public static readonly BindableProperty ProfileCommandProperty = CommandProperty(nameof(ProfileCommand));
    private readonly Label _badge;
    private readonly Image _profileImage;

    public string AppName { get => (string)GetValue(AppNameProperty); set => SetValue(AppNameProperty, value); }
    public string CompanyName { get => (string)GetValue(CompanyNameProperty); set => SetValue(CompanyNameProperty, value); }
    public ImageSource LogoSource { get => (ImageSource)GetValue(LogoSourceProperty); set => SetValue(LogoSourceProperty, value); }
    public bool ShowMenu { get => (bool)GetValue(ShowMenuProperty); set => SetValue(ShowMenuProperty, value); }
    public bool ShowSearch { get => (bool)GetValue(ShowSearchProperty); set => SetValue(ShowSearchProperty, value); }
    public bool ShowNotification { get => (bool)GetValue(ShowNotificationProperty); set => SetValue(ShowNotificationProperty, value); }
    public bool ShowUserProfile { get => (bool)GetValue(ShowUserProfileProperty); set => SetValue(ShowUserProfileProperty, value); }
    public int NotificationCount { get => (int)GetValue(NotificationCountProperty); set => SetValue(NotificationCountProperty, value); }
    public string UserInitials { get => (string)GetValue(UserInitialsProperty); set => SetValue(UserInitialsProperty, value); }
    public ImageSource? ProfileImageSource { get => (ImageSource?)GetValue(ProfileImageSourceProperty); set => SetValue(ProfileImageSourceProperty, value); }
    public ICommand? MenuCommand { get => (ICommand?)GetValue(MenuCommandProperty); set => SetValue(MenuCommandProperty, value); }
    public ICommand? SearchCommand { get => (ICommand?)GetValue(SearchCommandProperty); set => SetValue(SearchCommandProperty, value); }
    public ICommand? NotificationCommand { get => (ICommand?)GetValue(NotificationCommandProperty); set => SetValue(NotificationCommandProperty, value); }
    public ICommand? ProfileCommand { get => (ICommand?)GetValue(ProfileCommandProperty); set => SetValue(ProfileCommandProperty, value); }

    public AppBar()
    {
        MinimumHeightRequest = 52;
        HorizontalOptions = LayoutOptions.Fill;
        var grid = new Grid { ColumnSpacing = 2, ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto) } };
        grid.SetDynamicResource(BackgroundColorProperty, "PrimaryDark");
        grid.Add(Action("menu", nameof(ShowMenu), () => MenuCommand), 0);
        var branding = new Grid { ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star) }, ColumnSpacing = 6, InputTransparent = true, VerticalOptions = LayoutOptions.Center };
        var logo = new Image { HeightRequest = 32, WidthRequest = 32, Aspect = Aspect.AspectFit };
        logo.SetBinding(Image.SourceProperty, new Binding(nameof(LogoSource), source: this));
        var title = new Label { FontFamily = "OpenSansSemibold", FontSize = 19, LineBreakMode = LineBreakMode.WordWrap, VerticalOptions = LayoutOptions.Center };
        title.SetDynamicResource(Label.TextColorProperty, "TextOnPrimary");
        title.SetDynamicResource(Label.FontFamilyProperty, "SharedHeadingFontFamily");
        title.SetDynamicResource(Label.FontSizeProperty, "SharedHeaderFontSize");
        title.SetBinding(Label.TextProperty, new Binding(nameof(AppName), source: this));
        var company = new Label { FontSize = 10, LineBreakMode = LineBreakMode.WordWrap };
        company.SetDynamicResource(Label.TextColorProperty, "TextOnPrimary");
        company.SetBinding(Label.TextProperty, new Binding(nameof(CompanyName), source: this));
        var titles = new VerticalStackLayout { Spacing = 0, VerticalOptions = LayoutOptions.Center, Children = { title, company } };
        branding.Add(logo); branding.Add(titles, 1); grid.Add(branding, 1);
        grid.Add(Action("search", nameof(ShowSearch), () => SearchCommand), 2);
        var notifications = Action("notification", nameof(ShowNotification), () => NotificationCommand);
        _badge = new Label { FontSize = 10, TextColor = Colors.White, BackgroundColor = Colors.OrangeRed, HorizontalOptions = LayoutOptions.End, VerticalOptions = LayoutOptions.Start, InputTransparent = true };
        notifications.Add(_badge); UpdateBadge(); grid.Add(notifications, 3);
        var profile = new Grid { WidthRequest = 44, HeightRequest = 44 };
        profile.SetBinding(IsVisibleProperty, new Binding(nameof(ShowUserProfile), source: this));
        var initials = new Label { FontSize = 15, FontFamily = "OpenSansSemibold", HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center, InputTransparent = true };
        initials.SetDynamicResource(Label.TextColorProperty, "TextOnPrimary");
        initials.SetBinding(Label.TextProperty, new Binding(nameof(UserInitials), source: this));
        profile.Add(initials);
        _profileImage = new Image { WidthRequest = 36, HeightRequest = 36, Aspect = Aspect.AspectFill, IsVisible = false, InputTransparent = true };
        _profileImage.SetBinding(Image.SourceProperty, new Binding(nameof(ProfileImageSource), source: this));
        profile.Add(_profileImage);
        profile.Add(NavigationIcon.HitTarget("User profile and account actions", (_, _) => Execute(ProfileCommand)));
        grid.Add(profile, 4);
        Content = grid;
    }

    private Grid Action(string icon, string visibility, Func<ICommand?> command)
    {
        var grid = new Grid { WidthRequest = 44, HeightRequest = 44 };
        grid.SetBinding(IsVisibleProperty, new Binding(visibility, source: this));
        grid.Add(NavigationIcon.Create(icon, "TextOnPrimary"));
        grid.Add(NavigationIcon.HitTarget(icon, (_, _) => Execute(command())));
        return grid;
    }
    private void UpdateBadge() { _badge.Text = NotificationCount > 99 ? "99+" : NotificationCount.ToString(); _badge.IsVisible = NotificationCount > 0; }
    private static void Execute(ICommand? command) { if (command?.CanExecute(null) == true) command.Execute(null); }
    private static BindableProperty Flag(string name) => BindableProperty.Create(name, typeof(bool), typeof(AppBar), false);
    private static BindableProperty CommandProperty(string name) => BindableProperty.Create(name, typeof(ICommand), typeof(AppBar));
}
