using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Services;
using System.Collections.ObjectModel;

namespace FinTracker.PageModels;

public partial class EtlDataPageModel(EtlPdfImportService importer, PaymentModeRepository paymentModes, CategoryRepository categories, ShopRepository shops, FinancialAccountContext accounts, ICriticalOperationState criticalOperations, ModalErrorHandler errors) : ObservableObject
{
    public ObservableCollection<PaymentMode> PaymentModes { get; } = [];
    [ObservableProperty] private PaymentMode? _selectedExpensePaymentMode;
    [ObservableProperty] private PaymentMode? _selectedIncomePaymentMode;
    [ObservableProperty] private bool _createMissingEtlSetup;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "Select a bank statement PDF to import debit transactions.";
    [ObservableProperty] private string _summaryText = string.Empty;

    public async Task LoadPaymentModesAsync()
    {
        var accountId = accounts.RequireId();
        var rows = await paymentModes.ListAsync();
        if (accounts.RequireId() != accountId) return;
        PaymentModes.Clear();
        foreach (var row in rows) PaymentModes.Add(row);
        SelectedExpensePaymentMode = rows.FirstOrDefault(x => x.Id == SelectedExpensePaymentMode?.Id);
        SelectedIncomePaymentMode = rows.FirstOrDefault(x => x.Id == SelectedIncomePaymentMode?.Id);
        StatusText = rows.Count == 0 ? "Create payment modes for this company before importing." : "Select expense and income payment modes, then import a PDF.";
    }

    [RelayCommand]
    private async Task Import()
    {
        if (IsBusy) return;
        if (SelectedExpensePaymentMode is null || SelectedIncomePaymentMode is null)
        { StatusText = "Select both expense and income payment modes before choosing the PDF."; return; }
        var accountId = accounts.RequireId();
        var expenseModeId = SelectedExpensePaymentMode.Id;
        var incomeModeId = SelectedIncomePaymentMode.Id;
        FileResult? file;
        try { file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Select bank statement PDF", FileTypes = FilePickerFileType.Pdf }); }
        catch (Exception ex) { errors.HandleError(ex); return; }
        if (file is null) return;
        IsBusy = true; SummaryText = string.Empty; StatusText = "Preparing import...";
        var acceptingProgress = true;
        var progress = new Progress<string>(value => { if (acceptingProgress && IsBusy) StatusText = value; });
        try
        {
            using var operation = criticalOperations.Begin();
            await Task.Yield();
            if (accounts.RequireId() != accountId) throw new InvalidOperationException("The active company changed. Reload ETL configuration.");
            if (CreateMissingEtlSetup)
            {
                StatusText = "Preparing ETL category and holding shop...";
                await Task.Run(() => PrepareEtlSetupAsync(accountId));
            }
            var result = await importer.ImportAsync(file, expenseModeId, incomeModeId, progress);
            acceptingProgress = false;
            SummaryText = $"Found: {result.TransactionsFound} | Debits: {result.DebitTransactions} | Credits: {result.CreditTransactions} | Expenses: {result.Imported} | Income: {result.IncomeImported} | Duplicates: {result.DuplicatesSkipped} | Errors: {result.InvalidOrErrors}";
            StatusText = "Import completed.";
            await FinTracker.Pages.Controls.ThemeDialog.DisplayAlertAsync("ETL DATA", SummaryText, "OK");
        }
        catch (OperationCanceledException) { StatusText = "Import cancelled."; }
        catch (Exception ex) { StatusText = "Import failed."; errors.HandleError(ex); }
        finally { acceptingProgress = false; IsBusy = false; }
    }

    private async Task PrepareEtlSetupAsync(int accountId)
    {
        var category = (await categories.ListAsync()).FirstOrDefault(x => x.Name.Equals("ETL_DT", StringComparison.OrdinalIgnoreCase));
        if (category is null)
        {
            category = new Category { Name = "ETL_DT", IconKey = "default_shop", Budgeted = 0 };
            await categories.InsertAsync(category);
        }
        if (accounts.RequireId() != accountId) throw new InvalidOperationException("The active company changed during ETL configuration.");
        if ((await shops.ListAsync()).All(x => !x.Name.Equals("ETL_DATA_VAULT", StringComparison.OrdinalIgnoreCase)))
            await shops.SaveItemAsync(new Shop { Name = "ETL_DATA_VAULT", CategoryId = category.Id });
    }
}
