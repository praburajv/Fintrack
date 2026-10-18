using System.Collections.Concurrent;

public sealed class FileResult(string path)
{
    public string FileName => Path.GetFileName(path);
    public Func<Task<Stream>>? Reader { get; set; }
    public Task<Stream> OpenReadAsync() => Reader?.Invoke() ?? Task.FromResult<Stream>(File.OpenRead(path));
}
public sealed class PickOptions { public string PickerTitle { get; set; } = ""; public object? FileTypes { get; set; } }
public static class FilePickerFileType { public static object Pdf { get; } = new(); }
public sealed class FilePicker
{
    public static FilePicker Default { get; } = new();
    public FileResult? Next { get; set; }
    public Exception? Failure { get; set; }
    public Task<FileResult?> PickAsync(PickOptions options)
    {
        if (Failure is not null) { var error = Failure; Failure = null; throw error; }
        return Task.FromResult(Next);
    }
}
namespace FinTracker.Pages.Controls
{
    public static class ThemeDialog
    {
        public static Task DisplayAlertAsync(string title, string message, string button) => Task.CompletedTask;
    }
}

/// <summary>Runs async UI code on a dedicated synchronization context, without a MAUI runtime.</summary>
public sealed class UiPump : SynchronizationContext
{
    private readonly BlockingCollection<Action> _queue = new();
    public override void Post(SendOrPostCallback callback, object? state) => _queue.Add(() => callback(state));

    public static Task RunAsync(Func<Task> body)
    {
        var finished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var pump = new UiPump(); SetSynchronizationContext(pump);
            pump.Post(async _ =>
            {
                try { await body(); finished.SetResult(); }
                catch (Exception ex) { finished.SetException(ex); }
                finally { pump._queue.CompleteAdding(); }
            }, null);
            foreach (var callback in pump._queue.GetConsumingEnumerable()) callback();
        }) { IsBackground = true };
        thread.Start(); return finished.Task;
    }
}

public sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
{
    public void Report(T value) => report(value);
}
