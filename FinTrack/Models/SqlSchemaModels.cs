namespace FinTracker.Models;

public sealed record SqlTableMetadata(string Name, bool IsView = false)
{
    public override string ToString() => Name;
}

public sealed record SqlColumnMetadata(string TableName, string Name, string DataType, bool IsNullable, bool IsPrimaryKey)
{
    public string DisplayName => $"{TableName}.{Name}";
    public override string ToString() => DisplayName;
}

public sealed record SqlForeignKeyMetadata(string TableName, string ColumnName, string ReferencedTableName, string ReferencedColumnName);
