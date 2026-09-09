namespace MauiApp1.Pages.Controls;

public partial class ProcessingOverlay : ContentView
{
    public static readonly BindableProperty IsBusyProperty = BindableProperty.Create(nameof(IsBusy), typeof(bool), typeof(ProcessingOverlay), false);
    public static readonly BindableProperty MessageProperty = BindableProperty.Create(nameof(Message), typeof(string), typeof(ProcessingOverlay), "Processing...");
    public bool IsBusy { get => (bool)GetValue(IsBusyProperty); set => SetValue(IsBusyProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public ProcessingOverlay() => InitializeComponent();
}