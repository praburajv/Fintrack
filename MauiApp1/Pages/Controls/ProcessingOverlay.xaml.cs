namespace MauiApp1.Pages.Controls;

public partial class ProcessingOverlay : ContentView
{
    public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(nameof(IsBusy), typeof(bool), typeof(ProcessingOverlay), false);
    public static readonly BindableProperty MessageProperty = BindableProperty.Create(nameof(Message), typeof(string), typeof(ProcessingOverlay), "Processing...");
    public static readonly BindableProperty ProgressProperty = BindableProperty.Create(nameof(Progress), typeof(double), typeof(ProcessingOverlay), 0d);
    public static readonly BindableProperty ProgressTextProperty = BindableProperty.Create(nameof(ProgressText), typeof(string), typeof(ProcessingOverlay), "0%");
    public bool IsBusy { get => (bool)GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public string ProgressText { get => (string)GetValue(ProgressTextProperty); set => SetValue(ProgressTextProperty, value); }
    public ProcessingOverlay() => InitializeComponent();
}
