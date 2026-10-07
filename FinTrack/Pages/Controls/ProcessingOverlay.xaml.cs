namespace FinTracker.Pages.Controls;

public partial class ProcessingOverlay : ContentView
{
    public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(nameof(IsBusy), typeof(bool), typeof(ProcessingOverlay), false);
    public static readonly BindableProperty MessageProperty = BindableProperty.Create(nameof(Message), typeof(string), typeof(ProcessingOverlay), "Processing...");
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(nameof(Progress), typeof(double), typeof(ProcessingOverlay), 0d);
    public static readonly BindableProperty ProgressTextProperty = BindableProperty.Create(nameof(ProgressText), typeof(string), typeof(ProcessingOverlay), "0%");
    public static readonly BindableProperty SpinnerColorProperty = BindableProperty.Create(nameof(SpinnerColor), typeof(Color), typeof(ProcessingOverlay), Color.FromArgb("#2B0B98"));
    public static readonly BindableProperty ShowProgressProperty = BindableProperty.Create(nameof(ShowProgress), typeof(bool), typeof(ProcessingOverlay), true);
    public static readonly BindableProperty StopCommandProperty = BindableProperty.Create(nameof(StopCommand), typeof(System.Windows.Input.ICommand), typeof(ProcessingOverlay));
    public static readonly BindableProperty ShowStopProperty = BindableProperty.Create(nameof(ShowStop), typeof(bool), typeof(ProcessingOverlay), false);
    public static readonly BindableProperty StopEnabledProperty = BindableProperty.Create(nameof(StopEnabled), typeof(bool), typeof(ProcessingOverlay), true);
    public bool IsBusy { get => (bool)GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public string ProgressText { get => (string)GetValue(ProgressTextProperty); set => SetValue(ProgressTextProperty, value); }
    public Color SpinnerColor { get => (Color)GetValue(SpinnerColorProperty); set => SetValue(SpinnerColorProperty, value); }
    public bool ShowProgress { get => (bool)GetValue(ShowProgressProperty); set => SetValue(ShowProgressProperty, value); }
    public System.Windows.Input.ICommand? StopCommand { get => (System.Windows.Input.ICommand?)GetValue(StopCommandProperty); set => SetValue(StopCommandProperty, value); }
    public bool ShowStop { get => (bool)GetValue(ShowStopProperty); set => SetValue(ShowStopProperty, value); }
    public bool StopEnabled { get => (bool)GetValue(StopEnabledProperty); set => SetValue(StopEnabledProperty, value); }
    public ProcessingOverlay() => InitializeComponent();
}
