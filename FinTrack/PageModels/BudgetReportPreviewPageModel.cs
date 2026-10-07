using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Models;
using FinTracker.Services;

namespace FinTracker.PageModels;

public partial class BudgetReportPreviewPageModel(
    IExpenseStatementDocumentService documents,
    ModalErrorHandler errors) : ObservableObject, IQueryAttributable
{
    private readonly SemaphoreSlim _pdfGate = new(1, 1);
    private readonly SemaphoreSlim _openGate = new(1, 1);
    private readonly SemaphoreSlim _shareGate = new(1, 1);
    private string? _pdfPath;

    [ObservableProperty] private BudgetReportSnapshot? _report;
    [ObservableProperty] private bool _isGeneratingPdf;
    [ObservableProperty] private bool _isOpeningPdf;
    [ObservableProperty] private bool _isSharing;
    [ObservableProperty] private string _processingMessage = "Preparing budget report...";

    public bool IsOverlayBusy => IsGeneratingPdf || IsOpeningPdf || IsSharing;
    partial void OnIsGeneratingPdfChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsOpeningPdfChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));
    partial void OnIsSharingChanged(bool value) => OnPropertyChanged(nameof(IsOverlayBusy));

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("report", out var value) && value is BudgetReportSnapshot report)
        {
            Report = report;
            _pdfPath = null;
        }
    }

    [RelayCommand]
    private async Task Back()
    {
        if (!IsOverlayBusy) await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task OpenPdf()
    {
        if (Report is null || IsOverlayBusy || !await _openGate.WaitAsync(0)) return;
        try
        {
            IsOpeningPdf = true;
            ProcessingMessage = "Preparing budget report for printing...";
            await Task.Yield();
            var path = await EnsurePdfAsync();
            if (!await Launcher.Default.OpenAsync(new OpenFileRequest("Budget Report", new ReadOnlyFile(path))))
                throw new InvalidOperationException("No application is available to open the generated PDF for viewing or printing.");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsOpeningPdf = false; _openGate.Release(); }
    }

    [RelayCommand]
    private async Task Share()
    {
        if (Report is null || IsOverlayBusy || !await _shareGate.WaitAsync(0)) return;
        try
        {
            IsSharing = true;
            ProcessingMessage = "Preparing budget report for sharing...";
            await Task.Yield();
            var path = await EnsurePdfAsync();
            await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(
                new ShareFileRequest
                {
                    Title = "Share Budget Report",
                    File = new ShareFile(path, "application/pdf")
                });
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsSharing = false; _shareGate.Release(); }
    }

    private async Task<string> EnsurePdfAsync()
    {
        if (!string.IsNullOrWhiteSpace(_pdfPath) && File.Exists(_pdfPath)) return _pdfPath;
        await _pdfGate.WaitAsync();
        try
        {
            if (!string.IsNullOrWhiteSpace(_pdfPath) && File.Exists(_pdfPath)) return _pdfPath;
            if (Report is null) throw new InvalidOperationException("The budget report is not available.");
            IsGeneratingPdf = true;
            _pdfPath = await documents.CreateBudgetPdfAsync(Report);
            return _pdfPath;
        }
        finally
        {
            IsGeneratingPdf = false;
            _pdfGate.Release();
        }
    }
}
