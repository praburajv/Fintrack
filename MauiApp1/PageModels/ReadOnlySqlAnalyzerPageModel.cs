using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Services;

namespace MauiApp1.PageModels;

public partial class ReadOnlySqlAnalyzerPageModel(IReadOnlySqlAnalyzerService analyzer, ISqlSchemaMetadataService schema, SqlQueryBuilderService builder) : ObservableObject
{
    public const string TablesSql = "SELECT name, type FROM sqlite_master WHERE type IN ('table', 'view') ORDER BY type, name;";
    public const string ShopSchemaSql = "PRAGMA table_info(Shop);";
    public const string BillSchemaSql = "PRAGMA table_info(Bill_transcation);";
    public const string CountsSql = "SELECT (SELECT COUNT(*) FROM Bill_transcation) AS Bills, (SELECT COUNT(*) FROM Shop) AS Shops, (SELECT COUNT(*) FROM Payment_mode) AS PaymentModes, (SELECT COUNT(*) FROM UserAccount) AS Users;";

    [ObservableProperty] private string _sqlText = "SELECT * FROM Shop LIMIT 20;";
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private string _error = string.Empty;
    [ObservableProperty] private List<string> _columns = [];
    [ObservableProperty] private List<IReadOnlyList<string>> _rows = [];
    [ObservableProperty] private int _rowsReturned;
    [ObservableProperty] private bool _isTruncated;
    [ObservableProperty] private long _executionTimeMs;
    [ObservableProperty] private string _selectedSqlValue = string.Empty;
    [ObservableProperty] private bool _isStopping;

    private CancellationTokenSource? _executionCts;

    public string DatabaseFilename => Data.Constants.DatabaseFilename;
    public string DatabasePath => Path.Combine(FileSystem.AppDataDirectory, Data.Constants.DatabaseFilename);
    public bool HasResults => Columns.Count > 0;
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string ResultSummary => HasError ? string.Empty : RowsReturned == 0 && !HasResults ? string.Empty : $"Rows returned: {RowsReturned}    Execution time: {ExecutionTimeMs} ms";
    public string TruncationText => IsTruncated ? $"Rows displayed: {ReadOnlySqlAnalyzerService.MaxDisplayedRows}\nResults truncated." : string.Empty;
    public bool HasSelectedSqlValue => !string.IsNullOrWhiteSpace(SelectedSqlValue);

    [RelayCommand]
    private async Task ExecuteQuery()
    {
        if (IsBusy) return;
        if (IsBusy) return;
        _executionCts?.Dispose(); _executionCts = new CancellationTokenSource();
        IsBusy = true; IsStopping = false; Error = string.Empty; Status = "Executing query... Please wait.";
        try
        {
            var result = await analyzer.ExecuteAsync(SqlText, _executionCts.Token);
            if (result.IsCancelled) { Status = "Query stopped. Previous results retained."; return; }
            if (result.ErrorMessage is null)
            {
                Columns = result.Columns.ToList(); Rows = result.Rows.ToList(); RowsReturned = result.RowsReturned; SelectedSqlValue = string.Empty;
                IsTruncated = result.IsTruncated; ExecutionTimeMs = result.ExecutionTimeMs; Status = "Query completed.";
            }
            else { Error = result.ErrorMessage; Status = "SQL Error"; }
            NotifyResultProperties();
        }
        catch (OperationCanceledException) { Status = "Query stopped. Previous results retained."; }
        catch (Exception ex)
        {
            Columns = []; Rows = []; RowsReturned = 0; IsTruncated = false; Error = ex.Message; Status = "SQL Error"; NotifyResultProperties();
        }
        finally { _executionCts?.Dispose(); _executionCts = null; IsStopping = false; IsBusy = false; }
    }

    [RelayCommand]
    private void StopQuery()
    {
        if (!IsBusy || IsStopping) return;
        IsStopping = true; Status = "Stopping query..."; _executionCts?.Cancel();
    }

    [RelayCommand]
    private void Clear()
    {
        SqlText = string.Empty; Columns = []; Rows = []; RowsReturned = 0; IsTruncated = false; ExecutionTimeMs = 0; SelectedSqlValue = string.Empty; Status = string.Empty; Error = string.Empty; NotifyResultProperties();
    }

    [RelayCommand]
    private void SelectSqlValue(string value)
    {
        SelectedSqlValue = value;
        Status = $"Selected value: {value}";
        NotifyResultProperties();
    }

    [RelayCommand]
    private void CopySelectedValueToQuery()
    {
        if (!HasSelectedSqlValue) return;
        SqlText = SelectedSqlValue;
        Status = "Selected value copied to the SQL Query editor.";
        OnPropertyChanged(nameof(Status));
    }

    [RelayCommand] private void TablesQuery() => SqlText = TablesSql;
    [RelayCommand] private void ShopSchemaQuery() => SqlText = ShopSchemaSql;
    [RelayCommand] private void BillSchemaQuery() => SqlText = BillSchemaSql;
    [RelayCommand] private void CountsQuery() => SqlText = CountsSql;

    private void NotifyResultProperties()
    {
        OnPropertyChanged(nameof(HasResults)); OnPropertyChanged(nameof(HasError)); OnPropertyChanged(nameof(ResultSummary)); OnPropertyChanged(nameof(TruncationText)); OnPropertyChanged(nameof(HasSelectedSqlValue));
    }
}
