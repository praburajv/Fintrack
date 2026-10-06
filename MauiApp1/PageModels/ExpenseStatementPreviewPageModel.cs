using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Models;
using MauiApp1.Services;
using MauiApp1.Utilities;

namespace MauiApp1.PageModels;

public partial class ExpenseStatementPreviewPageModel(
    IExpenseStatementDocumentService documents,
    ModalErrorHandler errors) : ObservableObject, IQueryAttributable
{
    private readonly SemaphoreSlim _pdfGate = new(1, 1);
    private readonly SemaphoreSlim _printGate = new(1, 1);
    private readonly SemaphoreSlim _shareGate = new(1, 1);
    private string? _pdfPath;

    [ObservableProperty] private ExpenseStatementSnapshot? _statement;
    [ObservableProperty] private bool _isGeneratingPdf;
    [ObservableProperty] private bool _isOpeningPdf;
    [ObservableProperty] private bool _isSharing;
    [ObservableProperty] private bool _isShopVisitBusy;
    [ObservableProperty] private bool _shopVisitSortAscending;
    [ObservableProperty] private string _processingMessage = "Preparing expense statement...";

    public bool HasStatement => Statement is not null;
    public bool IsOverlayBusy => IsGeneratingPdf || IsOpeningPdf || IsSharing || IsShopVisitBusy;
    partial void OnIsShopVisitBusyChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnShopVisitSortAscendingChanged(bool value)
    {
        if (Statement is not null) Statement = Statement.WithShopVisits(ShopVisitOrdering.Order(Statement.ShopVisits, value));
        _pdfPath = null;
    }

    partial void OnStatementChanged(ExpenseStatementSnapshot? value) => OnPropertyChanged(nameof(HasStatement));
    partial void OnIsGeneratingPdfChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsOpeningPdfChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsSharingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("statement", out var value) && value is ExpenseStatementSnapshot statement)
        {
            ShopVisitSortAscending = query.TryGetValue("shopVisitSortAscending", out var ascending) && ascending is true;
            Statement = statement.WithShopVisits(ShopVisitOrdering.Order(statement.ShopVisits, ShopVisitSortAscending));
            _pdfPath = null;
        }
    }

    [RelayCommand]
    private async Task Back()
    {
        if (!IsOverlayBusy)
            await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task OpenPdf()
    {
        if (Statement is null || IsOverlayBusy || !await _printGate.WaitAsync(0)) return;
        try
        {
            IsOpeningPdf = true;
            ProcessingMessage = "Preparing statement for printing...";
            await Task.Yield();
            var path = await EnsurePdfAsync();
            if (!await Launcher.Default.OpenAsync(new OpenFileRequest("Expense Statement", new ReadOnlyFile(path))))
                throw new InvalidOperationException("No application is available to open the generated PDF for viewing or printing.");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsOpeningPdf = false; _printGate.Release(); }
    }

    [RelayCommand]
    private async Task Share()
    {
        if (Statement is null || IsOverlayBusy || !await _shareGate.WaitAsync(0)) return;
        try
        {
            IsSharing = true;
            ProcessingMessage = "Preparing statement for sharing...";
            await Task.Yield();
            var path = await EnsurePdfAsync();
            await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(
                new ShareFileRequest
                {
                    Title = "Share Expense Statement",
                    File = new ShareFile(path, "application/pdf")
                });
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsSharing = false; _shareGate.Release(); }
    }

    private async Task<string> EnsurePdfAsync()
    {
        if (!string.IsNullOrWhiteSpace(_pdfPath) && File.Exists(_pdfPath))
            return _pdfPath;

        await _pdfGate.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(_pdfPath) && File.Exists(_pdfPath))
                return _pdfPath;

            if (Statement is null)
                throw new InvalidOperationException("The expense statement is not available.");

            IsGeneratingPdf = true;
            _pdfPath = await documents.CreatePdfAsync(Statement);
            return _pdfPath;
        }
        finally
        {
            IsGeneratingPdf = false;
            _pdfGate.Release();
        }
    }
}
