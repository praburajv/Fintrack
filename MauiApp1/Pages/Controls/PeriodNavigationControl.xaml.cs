using System.Windows.Input;

namespace MauiApp1.Pages.Controls;

public partial class PeriodNavigationControl : Border
{
    public static readonly BindableProperty PeriodTextProperty = BindableProperty.Create(nameof(PeriodText), typeof(string), typeof(PeriodNavigationControl), string.Empty);
    public static readonly BindableProperty PreviousCommandProperty = BindableProperty.Create(nameof(PreviousCommand), typeof(ICommand), typeof(PeriodNavigationControl));
    public static readonly BindableProperty NextCommandProperty = BindableProperty.Create(nameof(NextCommand), typeof(ICommand), typeof(PeriodNavigationControl));
    public string PeriodText { get => (string)GetValue(PeriodTextProperty); set => SetValue(PeriodTextProperty, value); }
    public ICommand? PreviousCommand { get => (ICommand?)GetValue(PreviousCommandProperty); set => SetValue(PreviousCommandProperty, value); }
    public ICommand? NextCommand { get => (ICommand?)GetValue(NextCommandProperty); set => SetValue(NextCommandProperty, value); }
    public PeriodNavigationControl() => InitializeComponent();
}
