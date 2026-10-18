using FinTracker.Models;
using FinTracker.Pages.Controls;
using FinTracker.Services;

namespace FinTracker.Pages;

public sealed class HistoryPreviewPage : ContentPage
{
    private readonly HistoryReport _report;
    private readonly IHistoryDocumentService _documents;
    private readonly FinancialAccountContext _accounts;
    private readonly ModalErrorHandler _errors;
    private readonly ProcessingOverlay _overlay = new() { ShowProgress = false };
    private string? _pdf;

    public HistoryPreviewPage(HistoryReport report, IHistoryDocumentService documents, FinancialAccountContext accounts, ModalErrorHandler errors)
    {
        _report = report; _documents = documents; _accounts = accounts; _errors = errors;
        Title = report.Title + " preview";
        this.SetDynamicResource(BackgroundColorProperty, "Background");
        var open = new Button { Text = "View / Print PDF" };
        var share = new Button { Text = "Share PDF" };
        open.Clicked += async (_, _) => await ExportAsync(false);
        share.Clicked += async (_, _) => await ExportAsync(true);
        var body = new VerticalStackLayout { Padding = 16, Spacing = 10, Children =
        {
            new Label { Text = report.Title, FontSize = 24, FontAttributes = FontAttributes.Bold },
            new Label { Text = $"{report.Company}\n{report.Mode}: {report.Selection}\nYears: {string.Join(", ", report.Years)}\nOrder: {report.OrderText}\n{report.Count} bills · Total: {report.TotalText}" },
            open, share, new Label { Text = "The PDF includes every filtered bill, including collapsed groups. Print using your PDF viewer." }
        } };
        foreach (var group in report.Groups) body.Add(HistoryViews.Group(group, report.Type));
        Content = new Grid { Children = { new ScrollView { Content = body }, _overlay } };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_accounts.Current?.Id != _report.AccountId) await Navigation.PopAsync();
    }

    private async Task ExportAsync(bool share)
    {
        if (_overlay.IsBusy || _accounts.Current?.Id != _report.AccountId) return;
        _overlay.IsBusy = true; _overlay.Message = "Preparing history PDF...";
        try
        {
            _pdf ??= await _documents.CreateHistoryPdfAsync(_report);
            if (_accounts.Current?.Id != _report.AccountId) return;
            if (share)
                await Microsoft.Maui.ApplicationModel.DataTransfer.Share.Default.RequestAsync(new ShareFileRequest
                { Title = _report.Title, File = new ShareFile(_pdf, "application/pdf") });
            else if (!await Launcher.Default.OpenAsync(new OpenFileRequest(_report.Title, new ReadOnlyFile(_pdf))))
                throw new InvalidOperationException("No application is available to view or print this PDF.");
        }
        catch (Exception ex) { _errors.HandleError(ex); }
        finally { _overlay.IsBusy = false; }
    }
}
