namespace MauiApp1.Services;

public sealed record SqlAnalyzerResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    int RowsReturned,
    bool IsTruncated,
    long ExecutionTimeMs,
    string? ErrorMessage = null);

public interface IReadOnlySqlAnalyzerService
{
    Task<SqlAnalyzerResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default);
}
