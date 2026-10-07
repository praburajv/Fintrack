using System.Collections.ObjectModel;
using FinTracker.Models;

namespace FinTracker.PageModels;

public partial class ReadOnlySqlAnalyzerPageModel
{
    private readonly Task _schemaLoadTask = Task.CompletedTask;

    public ObservableCollection<SqlTableMetadata> Tables { get; } = [];
    public ObservableCollection<SqlColumnMetadata> AvailableColumns { get; } = [];
    public ObservableCollection<SqlColumnMetadata> QueryFields { get; } = [];
    public ObservableCollection<SqlColumnMetadata> SelectedFields { get; } = [];
    public ObservableCollection<SqlQueryCondition> Conditions { get; } = [];
    public ObservableCollection<SqlTableMetadata> JoinTables { get; } = [];
    public ObservableCollection<SqlColumnMetadata> JoinLeftFields { get; } = [];
    public ObservableCollection<SqlColumnMetadata> JoinRightFields { get; } = [];
    public ObservableCollection<SqlColumnMetadata> SortFields { get; } = [];

    public IReadOnlyList<string> Operators { get; } = ["=", "!=", ">", "<", ">=", "<=", "BETWEEN", "NOT BETWEEN", "IN", "NOT IN", "LIKE", "NOT LIKE", "IS NULL", "IS NOT NULL"];
    public IReadOnlyList<string> Connectors { get; } = ["AND", "OR"];
    public IReadOnlyList<string> JoinTypes { get; } = ["INNER JOIN", "LEFT JOIN"];
    public IReadOnlyList<string> SortDirections { get; } = ["ASC", "DESC"];

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool _isBuilderExpanded;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool _isSchemaBusy;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private SqlTableMetadata? _selectedTable;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private SqlColumnMetadata? _selectedField;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private SqlColumnMetadata? _selectedConditionField;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _conditionOperator = "=";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _conditionValue = string.Empty;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _conditionSecondValue = string.Empty;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _conditionConnector = "AND";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private SqlTableMetadata? _selectedJoinTable;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private SqlColumnMetadata? _selectedJoinLeftField;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private SqlColumnMetadata? _selectedJoinRightField;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _joinType = "INNER JOIN";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private SqlColumnMetadata? _selectedSortField;
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _sortDirection = "DESC";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _limitText = "100";
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private string _builderError = string.Empty;

    public bool IsValueVisible => ConditionOperator is not ("IS NULL" or "IS NOT NULL");
    public bool IsSecondValueVisible => ConditionOperator is "BETWEEN" or "NOT BETWEEN";
    public bool HasJoin => SelectedJoinTable is not null;
    public bool HasSelectedFields => SelectedFields.Count > 0;
    public bool HasBuilderError => !string.IsNullOrWhiteSpace(BuilderError);
    public string BuilderHeaderText => IsBuilderExpanded ? "▼ Query Builder" : "▶ Query Builder";

    partial void OnIsBuilderExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(BuilderHeaderText));
        if (value && Tables.Count == 0) _ = LoadTablesAsync();
    }

    partial void OnSelectedTableChanged(SqlTableMetadata? value) => _ = LoadTableColumnsAsync(value);
    partial void OnSelectedJoinTableChanged(SqlTableMetadata? value) => _ = LoadJoinColumnsAsync(value);
    partial void OnConditionOperatorChanged(string value) { OnPropertyChanged(nameof(IsValueVisible)); OnPropertyChanged(nameof(IsSecondValueVisible)); }
    partial void OnBuilderErrorChanged(string value) => OnPropertyChanged(nameof(HasBuilderError));
    partial void OnSelectedJoinLeftFieldChanged(SqlColumnMetadata? value) => OnPropertyChanged(nameof(JoinSummary));
    partial void OnSelectedJoinRightFieldChanged(SqlColumnMetadata? value) => OnPropertyChanged(nameof(JoinSummary));
    partial void OnJoinTypeChanged(string value) => OnPropertyChanged(nameof(JoinSummary));

    public string JoinSummary => SelectedJoinTable is null ? "None" : $"{JoinType} {SelectedJoinTable.Name}";

    private async Task LoadTablesAsync()
    {
        IsSchemaBusy = true;
        try
        {
            Tables.Clear();
            foreach (var table in await schema.GetTablesAsync()) Tables.Add(table);
            SelectedTable ??= Tables.FirstOrDefault();
        }
        catch (Exception ex) { BuilderError = $"Unable to load tables: {ex.Message}"; }
        finally { IsSchemaBusy = false; }
    }

    private async Task LoadTableColumnsAsync(SqlTableMetadata? table)
    {
        AvailableColumns.Clear(); QueryFields.Clear(); SelectedFields.Clear(); Conditions.Clear(); SortFields.Clear(); SelectedField = null; SelectedConditionField = null; SelectedSortField = null;
        if (table is null) return;
        IsSchemaBusy = true;
        try
        {
            foreach (var column in await schema.GetColumnsAsync(table.Name)) AvailableColumns.Add(column);
            foreach (var column in AvailableColumns) QueryFields.Add(column);
            JoinTables.Clear(); foreach (var item in Tables.Where(item => !item.Name.Equals(table.Name, StringComparison.OrdinalIgnoreCase))) JoinTables.Add(item);
            await LoadJoinColumnsAsync(SelectedJoinTable);
        }
        catch (Exception ex) { BuilderError = $"Unable to load fields: {ex.Message}"; }
        finally { IsSchemaBusy = false; }
    }

    private async Task LoadJoinColumnsAsync(SqlTableMetadata? table)
    {
        JoinLeftFields.Clear(); JoinRightFields.Clear(); SelectedJoinLeftField = null; SelectedJoinRightField = null;
        QueryFields.Clear(); foreach (var column in AvailableColumns) QueryFields.Add(column);
        if (SelectedTable is null || table is null) { OnPropertyChanged(nameof(HasJoin)); return; }
        try
        {
            foreach (var column in AvailableColumns) JoinLeftFields.Add(column);
            foreach (var column in await schema.GetColumnsAsync(table.Name)) JoinRightFields.Add(column);
            foreach (var column in JoinRightFields) if (!QueryFields.Contains(column)) QueryFields.Add(column);
            var key = (await schema.GetForeignKeysAsync(SelectedTable.Name)).FirstOrDefault(item => item.ReferencedTableName.Equals(table.Name, StringComparison.OrdinalIgnoreCase));
            if (key is not null)
            {
                SelectedJoinLeftField = JoinLeftFields.FirstOrDefault(item => item.Name.Equals(key.ColumnName, StringComparison.OrdinalIgnoreCase));
                SelectedJoinRightField = JoinRightFields.FirstOrDefault(item => item.Name.Equals(key.ReferencedColumnName, StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex) { BuilderError = $"Unable to load join fields: {ex.Message}"; }
        OnPropertyChanged(nameof(HasJoin)); OnPropertyChanged(nameof(JoinSummary));
    }

    [CommunityToolkit.Mvvm.Input.RelayCommand] private void ToggleBuilder() => IsBuilderExpanded = !IsBuilderExpanded;
    [CommunityToolkit.Mvvm.Input.RelayCommand] private void AddField() { if (SelectedField is not null && !SelectedFields.Contains(SelectedField)) { SelectedFields.Add(SelectedField); OnPropertyChanged(nameof(HasSelectedFields)); } }
    [CommunityToolkit.Mvvm.Input.RelayCommand] private void RemoveField(SqlColumnMetadata field) { SelectedFields.Remove(field); OnPropertyChanged(nameof(HasSelectedFields)); }
    [CommunityToolkit.Mvvm.Input.RelayCommand] private void AddCondition()
    {
        if (SelectedConditionField is null) { BuilderError = "Select a filter field first."; return; }
        Conditions.Add(new SqlQueryCondition { Field = SelectedConditionField, Operator = ConditionOperator, Value = ConditionValue.Trim(), SecondValue = ConditionSecondValue.Trim(), Connector = Conditions.Count == 0 ? "AND" : ConditionConnector });
        ConditionValue = string.Empty; ConditionSecondValue = string.Empty; BuilderError = string.Empty;
    }
    [CommunityToolkit.Mvvm.Input.RelayCommand] private void RemoveCondition(SqlQueryCondition condition) => Conditions.Remove(condition);
    [CommunityToolkit.Mvvm.Input.RelayCommand] private void ClearBuilder()
    {
        SelectedFields.Clear(); Conditions.Clear(); SelectedJoinTable = null; SelectedSortField = null; LimitText = "100"; BuilderError = string.Empty; OnPropertyChanged(nameof(HasSelectedFields));
    }
    [CommunityToolkit.Mvvm.Input.RelayCommand] private async Task GenerateSqlAsync()
    {
        BuilderError = string.Empty;
        var join = SelectedJoinTable is null ? null : new SqlQueryJoin { JoinType = JoinType, RightTable = SelectedJoinTable, LeftField = SelectedJoinLeftField, RightField = SelectedJoinRightField };
        var built = builder.Build(SelectedTable, SelectedFields, Conditions, join, SelectedSortField, SortDirection, LimitText);
        if (!built.Succeeded) { BuilderError = built.Error ?? "Unable to generate SQL."; return; }
        var checkError = await analyzer.ValidateBuildAsync(built.Sql!);
        if (checkError is not null) { BuilderError = checkError; Status = "SQL build check failed."; return; }
        SqlText = built.Sql!; Status = "SQL generated and build-checked.";
    }
}
