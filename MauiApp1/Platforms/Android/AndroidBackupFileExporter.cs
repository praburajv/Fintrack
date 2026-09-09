using Android.App;
using Android.Content;
using Android.Provider;
using MauiApp1.Services;

namespace MauiApp1.Platforms.Android;

public sealed class AndroidBackupFileExporter : IBackupFileExporter
{
    private static readonly object Sync = new();
    private static TaskCompletionSource<global::Android.Net.Uri?>? _pending;
    private static int _requestInFlight;

    public async Task SaveAsync(string sourcePath, string fileName, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Exchange(ref _requestInFlight, 1) != 0) return;
        try
        {
            var activity = Platform.CurrentActivity ?? throw new InvalidOperationException("No active Android activity.");
            var intent = new Intent(Intent.ActionCreateDocument);
            intent.AddCategory(Intent.CategoryOpenable);
            intent.SetType("application/octet-stream");
            intent.PutExtra(Intent.ExtraTitle, fileName);
            TaskCompletionSource<global::Android.Net.Uri?> tcs;
            lock (Sync) { tcs = new(TaskCreationOptions.RunContinuationsAsynchronously); _pending = tcs; }
            activity.StartActivityForResult(intent, 4127);
            var uri = await tcs.Task.WaitAsync(cancellationToken);
            if (uri is null) return;
            await using var source = File.OpenRead(sourcePath);
            await using var destination = activity.ContentResolver!.OpenOutputStream(uri);
            if (destination is null) throw new IOException("The selected destination could not be opened.");
            await source.CopyToAsync(destination, cancellationToken);
        }
        finally { lock (Sync) { _pending = null; } Interlocked.Exchange(ref _requestInFlight, 0); }
    }

    public static void Complete(int requestCode, global::Android.App.Result resultCode, Intent? data)
    {
        if (requestCode != 4127) return;
        lock (Sync) { _pending?.TrySetResult(resultCode == global::Android.App.Result.Ok ? data?.Data : null); }
    }
}