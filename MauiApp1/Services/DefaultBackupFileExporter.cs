namespace MauiApp1.Services;
public sealed class DefaultBackupFileExporter : IBackupFileExporter
{
    public Task SaveAsync(string sourcePath, string fileName, CancellationToken cancellationToken = default)
        => throw new PlatformNotSupportedException("Saving backups is supported on Android.");
}