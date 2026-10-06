using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Services;

namespace MauiApp1.PageModels;

public partial class EtlDataPageModel(EtlPdfImportService importer, ModalErrorHandler errors) : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Select a bank statement PDF to import debit transactions.";
    [ObservableProperty] private string _summaryText = string.Empty;

    [RelayCommand]
    private async Task Import()
    {
        if (IsBusy) return;
        FileResult? file;
        try { file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select bank statement PDF", FileTypes = FilePickerFileType.Pdf }); }
        catch (Exception ex) { errors.HandleError(ex); return; }
        if (file is null) return;
        IsBusy = true; SummaryText = string.Empty; StatusText = "Preparing import...";
        try { await Task.Yield(); var result = await importer.ImportAsync(file, new Progress<string>(value => StatusText = value)); SummaryText = $"Found: {result.TransactionsFound} | Debits: {result.DebitTransactions} | Credits: {result.CreditTransactions} | Expenses: {result.Imported} | Income: {result.IncomeImported} | Duplicates: {result.DuplicatesSkipped} | Errors: {result.InvalidOrErrors}"; StatusText = "Import completed."; await Shell.Current.DisplayAlertAsync("ETL DATA", SummaryText, "OK"); }
        catch (OperationCanceledException) { StatusText = "Import cancelled."; }
        catch (Exception ex) { StatusText = "Import failed."; errors.HandleError(ex); }
        finally { IsBusy = false; }
    }
}
