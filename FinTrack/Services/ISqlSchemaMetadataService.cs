using FinTracker.Models;

namespace FinTracker.Services;

public interface ISqlSchemaMetadataService
{
    Task<IReadOnlyList<SqlTableMetadata>> GetTablesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetTableNamesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SqlColumnMetadata>> GetColumnsAsync(string tableName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SqlForeignKeyMetadata>> GetForeignKeysAsync(string tableName, CancellationToken cancellationToken = default);
    void Invalidate();
}
