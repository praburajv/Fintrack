using System.Globalization;
using System.Text;
using MauiApp1.Models;

namespace MauiApp1.Services;

public sealed record SqlBuildResult(string? Sql, string? Error)
{
    public bool Succeeded => Sql is not null && Error is null;
}

public sealed class SqlQueryBuilderService
{
    public SqlBuildResult Build(
        SqlTableMetadata? table,
        IReadOnlyList<SqlColumnMetadata> selectedFields,
        IReadOnlyList<SqlQueryCondition> conditions,
        SqlQueryJoin? join,
        SqlColumnMetadata? sortField,
        string sortDirection,
        string limitText)
    {
        if (table is null) return new(null, "Select a table first.");
        if (join?.RightTable is not null && (join.LeftField is null || join.RightField is null))
            return new(null, "Complete the JOIN fields before generating SQL.");

        var sql = new StringBuilder("SELECT ");
        sql.Append(selectedFields.Count == 0 ? "*" : string.Join(", ", selectedFields.Select(Qualify)));
        sql.Append("\nFROM ").Append(Qualify(table.Name));

        if (join?.RightTable is not null)
        {
            var leftField = join.LeftField!;
            var rightField = join.RightField!;
            sql.Append('\n').Append(join.JoinType).Append(' ').Append(Qualify(join.RightTable.Name));
            sql.Append(" ON ").Append(Qualify(leftField.TableName, leftField.Name));
            sql.Append(" = ").Append(Qualify(rightField.TableName, rightField.Name));
        }

        var usableConditions = conditions.Where(condition => condition.Field is not null).ToList();
        if (usableConditions.Count > 0)
        {
            var fragments = new List<string>();
            for (var index = 0; index < usableConditions.Count; index++)
            {
                var condition = usableConditions[index];
                var fragment = BuildCondition(condition);
                if (fragment.Error is not null) return new(null, fragment.Error);
                fragments.Add((index > 0 ? $"{condition.Connector} " : string.Empty) + fragment.Sql);
            }
            sql.Append("\nWHERE ").Append(string.Join(' ', fragments));
        }

        if (sortField is not null)
        {
            if (sortDirection is not ("ASC" or "DESC")) return new(null, "Choose ASC or DESC for sorting.");
            sql.Append("\nORDER BY ").Append(Qualify(sortField)).Append(' ').Append(sortDirection);
        }

        if (!string.IsNullOrWhiteSpace(limitText))
        {
            if (!int.TryParse(limitText, NumberStyles.None, CultureInfo.InvariantCulture, out var limit) || limit < 1 || limit > 10000)
                return new(null, "Limit must be a whole number between 1 and 10000.");
            sql.Append("\nLIMIT ").Append(limit);
        }

        return new(sql.Append(';').ToString(), null);
    }

    private static (string? Sql, string? Error) BuildCondition(SqlQueryCondition condition)
    {
        var field = Qualify(condition.Field!);
        var op = condition.Operator.ToUpperInvariant();
        if (op is "IS NULL" or "IS NOT NULL") return ($"{field} {op}", null);
        if (string.IsNullOrWhiteSpace(condition.Value)) return (null, $"Enter a value for {field} {op}.");
        if (op is "BETWEEN" or "NOT BETWEEN")
        {
            if (string.IsNullOrWhiteSpace(condition.SecondValue)) return (null, $"Enter both values for {field} {op}.");
            return ($"{field} {op} {Literal(condition.Field!, condition.Value)} AND {Literal(condition.Field!, condition.SecondValue)}", null);
        }
        if (op is "IN" or "NOT IN")
        {
            var values = condition.Value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (values.Length == 0) return (null, $"Enter at least one value for {field} {op}.");
            return ($"{field} {op} ({string.Join(", ", values.Select(value => Literal(condition.Field!, value)))})", null);
        }
        var supported = new[] { "=", "!=", ">", "<", ">=", "<=", "LIKE", "NOT LIKE" };
        return supported.Contains(op, StringComparer.Ordinal) ? ($"{field} {op} {Literal(condition.Field!, condition.Value)}", null) : (null, $"Unsupported operator: {op}.");
    }

    private static string Literal(SqlColumnMetadata field, string value)
    {
        var type = field.DataType.ToUpperInvariant();
        if (type.Contains("INT") || type.Contains("REAL") || type.Contains("FLOA") || type.Contains("DOUB") || type.Contains("NUM") || type.Contains("DEC") || type.Contains("BOOL"))
        {
            if (decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return value;
        }
        return $"'{value.Replace("'", "''")}'";
    }

    private static string Qualify(SqlColumnMetadata column) => Qualify(column.TableName, column.Name);
    private static string Qualify(string table, string column) => $"{Quote(table)}.{Quote(column)}";
    private static string Qualify(string table) => Quote(table);
    private static string Quote(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
