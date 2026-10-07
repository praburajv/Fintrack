using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using FinTracker.Data;

namespace FinTracker.Services;

public sealed class ReadOnlySqlAnalyzerService : IReadOnlySqlAnalyzerService
{
    public const int MaxDisplayedRows = 500;

    private static readonly HashSet<string> BlockedKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        "INSERT", "UPDATE", "DELETE", "REPLACE", "CREATE", "ALTER", "DROP", "VACUUM", "REINDEX", "ANALYZE",
        "ATTACH", "DETACH", "BEGIN", "COMMIT", "ROLLBACK", "SAVEPOINT", "RELEASE"
    };

    private static readonly Regex AllowedPragma = new(
        @"^PRAGMA\s+(?:table_info|table_xinfo|foreign_key_list|index_list)\s*\(\s*[A-Za-z_][A-Za-z0-9_]*\s*\)|^PRAGMA\s+index_info\s*\(\s*[A-Za-z_][A-Za-z0-9_]*\s*\)|^PRAGMA\s+(?:database_list|compile_options|integrity_check|quick_check)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<SqlAnalyzerResult> ExecuteAsync(string sql, CancellationToken cancellationToken = default)
    {
        var validation = Validate(sql);
        if (validation is not null)
            return Failed(validation);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(FileSystem.AppDataDirectory, Constants.DatabaseFilename),
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql.Trim();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var columns = Enumerable.Range(0, reader.FieldCount)
                .Select(reader.GetName)
                .ToList();
            var rows = new List<IReadOnlyList<string>>();
            var rowsReturned = 0;
            var truncated = false;

            while (await reader.ReadAsync(cancellationToken))
            {
                rowsReturned++;
                if (rows.Count < MaxDisplayedRows)
                    rows.Add(Enumerable.Range(0, reader.FieldCount)
                        .Select(index => FormatValue(reader.GetValue(index), columns[index]))
                        .ToList());
                else
                    truncated = true;
            }

            stopwatch.Stop();
            return new SqlAnalyzerResult(columns, rows, rowsReturned, truncated, stopwatch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return new SqlAnalyzerResult([], [], 0, false, stopwatch.ElapsedMilliseconds, "Query stopped.", true);
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException or IOException)
        {
            stopwatch.Stop();
            return new SqlAnalyzerResult([], [], 0, false, stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public async Task<string?> ValidateBuildAsync(string sql, CancellationToken cancellationToken = default)
    {
        var validation = Validate(sql);
        if (validation is not null) return validation;

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = Path.Combine(FileSystem.AppDataDirectory, Constants.DatabaseFilename),
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            }.ToString();
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = sql.Trim();
            command.Prepare();
            return null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return "SQL validation was cancelled.";
        }
        catch (Exception ex) when (ex is SqliteException or InvalidOperationException or IOException)
        {
            return $"SQL build check failed: {ex.Message}";
        }
    }

    private static string? Validate(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return "Enter a read-only SQL query.";

        if (!TryRemoveComments(sql, out var withoutComments, out var commentError))
            return commentError;

        var statement = withoutComments.Trim();
        if (statement.EndsWith(';'))
            statement = statement[..^1].TrimEnd();
        if (statement.Length == 0 || ContainsStatementSeparator(statement))
            return "Only one SQL statement is permitted.";

        var firstToken = Regex.Match(statement, @"^([A-Za-z_][A-Za-z0-9_]*)", RegexOptions.CultureInvariant).Groups[1].Value;
        if (!firstToken.Equals("SELECT", StringComparison.OrdinalIgnoreCase)
            && !firstToken.Equals("WITH", StringComparison.OrdinalIgnoreCase)
            && !firstToken.Equals("PRAGMA", StringComparison.OrdinalIgnoreCase))
            return "This statement is not permitted because it could modify the database.";

        var tokens = Regex.Matches(statement, @"\b[A-Za-z_][A-Za-z0-9_]*\b", RegexOptions.CultureInvariant)
            .Select(match => match.Value);
        if (tokens.Any(BlockedKeywords.Contains))
            return "This statement is not permitted because it could modify the database.";

        if (firstToken.Equals("PRAGMA", StringComparison.OrdinalIgnoreCase)
            && !AllowedPragma.IsMatch(statement))
            return "This PRAGMA is not permitted by the read-only analyzer.";

        return null;
    }

    private static bool ContainsStatementSeparator(string sql)
    {
        var quote = '\0';
        foreach (var character in sql)
        {
            if (quote != '\0')
            {
                if (character == quote) quote = '\0';
                continue;
            }
            if (character is '\'' or '"') { quote = character; continue; }
            if (character == ';') return true;
        }
        return false;
    }

    private static bool TryRemoveComments(string sql, out string result, out string? error)
    {
        var output = new StringBuilder(sql.Length);
        var quote = '\0';
        for (var index = 0; index < sql.Length; index++)
        {
            var character = sql[index];
            if (quote != '\0')
            {
                output.Append(character);
                if (character == quote)
                {
                    if (index + 1 < sql.Length && sql[index + 1] == quote) output.Append(sql[++index]);
                    else quote = '\0';
                }
                continue;
            }
            if (character is '\'' or '"') { quote = character; output.Append(character); continue; }
            if (character == '-' && index + 1 < sql.Length && sql[index + 1] == '-')
            {
                index += 2;
                while (index < sql.Length && sql[index] is not '\r' and not '\n') index++;
                index--;
                output.Append(' ');
                continue;
            }
            if (character == '/' && index + 1 < sql.Length && sql[index + 1] == '*')
            {
                index += 2;
                var closed = false;
                while (index + 1 < sql.Length)
                {
                    if (sql[index] == '*' && sql[index + 1] == '/') { index++; closed = true; break; }
                    index++;
                }
                if (!closed) { result = string.Empty; error = "The SQL comment is not closed."; return false; }
                output.Append(' ');
                continue;
            }
            output.Append(character);
        }
        if (quote != '\0') { result = string.Empty; error = "The SQL string is not closed."; return false; }
        result = output.ToString(); error = null; return true;
    }

    private static string FormatValue(object value, string columnName)
    {
        if (columnName.Equals("PasswordHash", StringComparison.OrdinalIgnoreCase)
            || columnName.Equals("PasswordSalt", StringComparison.OrdinalIgnoreCase))
            return "[protected]";
        if (value is DBNull) return "NULL";
        if (value is byte[] bytes) return $"[BLOB: {bytes.Length} bytes]";
        return Convert.ToString(value, CultureInfo.CurrentCulture) ?? "NULL";
    }

    private static SqlAnalyzerResult Failed(string message) => new([], [], 0, false, 0, message);
}
