using CommunityToolkit.Mvvm.ComponentModel;

namespace FinTracker.Models;

public sealed partial class SqlQueryCondition : ObservableObject
{
    [ObservableProperty] private SqlColumnMetadata? _field;
    [ObservableProperty] private string _operator = "=";
    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private string _secondValue = string.Empty;
    [ObservableProperty] private string _connector = "AND";

    public string Summary => Operator switch
    {
        "IS NULL" or "IS NOT NULL" => $"{Field?.DisplayName ?? "Field"} {Operator}",
        "BETWEEN" or "NOT BETWEEN" => $"{Field?.DisplayName ?? "Field"} {Operator} {Value} AND {SecondValue}",
        "IN" or "NOT IN" => $"{Field?.DisplayName ?? "Field"} {Operator} ({Value})",
        _ => $"{Field?.DisplayName ?? "Field"} {Operator} {Value}"
    };

    partial void OnFieldChanged(SqlColumnMetadata? value) => OnPropertyChanged(nameof(Summary));
    partial void OnOperatorChanged(string value) => OnPropertyChanged(nameof(Summary));
    partial void OnValueChanged(string value) => OnPropertyChanged(nameof(Summary));
    partial void OnSecondValueChanged(string value) => OnPropertyChanged(nameof(Summary));
}

public sealed partial class SqlQueryJoin : ObservableObject
{
    [ObservableProperty] private string _joinType = "INNER JOIN";
    [ObservableProperty] private SqlTableMetadata? _rightTable;
    [ObservableProperty] private SqlColumnMetadata? _leftField;
    [ObservableProperty] private SqlColumnMetadata? _rightField;

    public string Summary => RightTable is null ? "None" : $"{JoinType} {RightTable.Name}";
    partial void OnJoinTypeChanged(string value) => OnPropertyChanged(nameof(Summary));
    partial void OnRightTableChanged(SqlTableMetadata? value) => OnPropertyChanged(nameof(Summary));
}
