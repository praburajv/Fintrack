namespace FinTracker.Services;
public interface IBackupFileExporter { Task SaveAsync(string sourcePath, string fileName, CancellationToken cancellationToken = default); }