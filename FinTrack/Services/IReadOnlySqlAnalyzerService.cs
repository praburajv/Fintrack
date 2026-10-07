namespace FinTracker.Services;

public sealed record SqlAnalyzerResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int RowsReturned,
    bool IsTruncated,
    long ExecutionTimeMs,
    string? ErrorMessage = null,
    bool IsCancelled = false);

public interface IReadOnlySqlAnalyzerService
{
    Task<SqlAnalyzerResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default);
    Task<string?> ValidateBuildAsync(string sql, CancellationToken cancellationToken = default);
}
