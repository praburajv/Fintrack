using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MauiApp1.Data;
using MauiApp1.Models;
using MauiApp1.Services;
using CommunityToolkit.Mvvm.Messaging;

namespace MauiApp1.PageModels;

public partial class MonthlyExpenseDataPageModel(BillTransactionRepository bills, MonthlyFinancialDeletionService deletion, ModalErrorHandler errors) : ObservableObject
{
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    private readonly SemaphoreSlim _deleteGate = new(1, 1);
    private TaskCompletionSource<MonthlyDeletionChoice?>? _choiceCompletion;
    private bool _deletionReviewCancelled;
    [ObservableProperty] private int _year = DateTime.Today.Year;
    [ObservableProperty] private bool _isBusy = true;
    [ObservableProperty] private string _processingMessage = "Loading monthly expense counts...";
    [ObservableProperty, NotifyPropertyChangedFor(nameof(ProgressText)), NotifyPropertyChangedFor(nameof(ProgressFraction))] private double _progress;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsProcessing))] private bool _isAwaitingDeletionChoice;
    [ObservableProperty] private bool _isDeletingData;
    [ObservableProperty] private string _deletionChoiceTitle = string.Empty;
    [ObservableProperty] private string _deletionChoiceMessage = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(IsDeleteConfirmation))] private MonthlyDeletionChoice? _deleteConfirmationChoice;
    public ObservableCollection<ExpenseMonthMaintenanceItem> Months { get; } = [];
    public ObservableCollection<MonthlyDeletionChoice> DeletionChoices { get; } = [];
    public string YearText => Year.ToString();
    public string ProgressText => $"{Progress:0}%";
    public double ProgressFraction => Math.Clamp(Progress / 100d, 0, 1);
    public bool IsProcessing => IsBusy && !IsAwaitingDeletionChoice;
    public bool IsDeleteConfirmation => DeleteConfirmationChoice is not null;
    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(IsProcessing));

    [RelayCommand]
    private void ChooseDeletionOption(MonthlyDeletionChoice? choice)
    {
        if (IsAwaitingDeletionChoice && choice is not null && DeletionChoices.Any(item => ReferenceEquals(item, choice)))
            _choiceCompletion?.TrySetResult(choice);
    }

    [RelayCommand]
    private void CancelDeletionReview()
    {
        if (_deleteGate.CurrentCount == 0 && !IsDeletingData) _deletionReviewCancelled = true;
        _choiceCompletion?.TrySetResult(null);
    }

    private async Task<MonthlyDeletionChoice?> ChooseAsync(string title, string message, params MonthlyDeletionChoice[] choices)
    {
        if (_deletionReviewCancelled) return null;
        // The page owns the buttons, so Android action-sheet styling or a
        // processing overlay cannot obscure the options or final Delete button.
        var completion = new TaskCompletionSource<MonthlyDeletionChoice?>(TaskCreationOptions.RunContinuationsAsynchronously);
        _choiceCompletion = completion;
        DeletionChoiceTitle = title;
        DeletionChoiceMessage = message;
        DeletionChoices.Clear();
        foreach (var choice in choices) DeletionChoices.Add(choice);
        DeleteConfirmationChoice = choices.FirstOrDefault(choice => choice.IsDestructive);
        try
        {
            IsAwaitingDeletionChoice = true;
            var choice = await completion.Task;
            return _deletionReviewCancelled ? null : choice;
        }
        finally
        {
            IsAwaitingDeletionChoice = false;
            _choiceCompletion = null;
            DeletionChoices.Clear();
            DeleteConfirmationChoice = null;
        }
    }

    [RelayCommand] private async Task Appearing() => await LoadAsync();
    [RelayCommand] private async Task PreviousYear() { if (!IsBusy) { Year--; await LoadAsync(); } }
    [RelayCommand] private async Task NextYear() { if (!IsBusy) { Year++; await LoadAsync(); } }

    private async Task LoadAsync(bool keepBusyAfterLoad = false)
    {
        if (_deleteGate.CurrentCount == 0 && !keepBusyAfterLoad) return;
        if (!await _loadGate.WaitAsync(0)) return;
        IsBusy = true; ProcessingMessage = "Loading monthly expense counts..."; Progress = 0;
        try
        {
            await Task.Yield();
            var counts = await bills.GetMonthlyBillCountsAsync(Year);
            var incomeCounts = await deletion.GetIncomeCountsAsync(Year);
            Months.Clear();
            foreach (var month in Enumerable.Range(1, 12))
                Months.Add(new ExpenseMonthMaintenanceItem { Year = Year, MonthNumber = month, MonthName = new DateTime(2000, month, 1).ToString("MMMM"), BillCount = counts.GetValueOrDefault(month), ImportedIncomeCount = incomeCounts.GetValueOrDefault(month).Imported, UntrackedIncomeCount = incomeCounts.GetValueOrDefault(month).Untracked });
            Progress = 100;
            OnPropertyChanged(nameof(YearText));
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = keepBusyAfterLoad; ProcessingMessage = string.Empty; _loadGate.Release(); }
    }

    [RelayCommand]
    private async Task ViewMonth(ExpenseMonthMaintenanceItem? month)
    {
        if (month is null || IsBusy) return;
        IsBusy = true; ProcessingMessage = $"Opening {month.MonthName} {month.Year}...";
        try
        {
            await Task.Yield();
            // myexpense is a Shell flyout root, not a registered detail route.
            await Shell.Current.GoToAsync($"//myexpense?mode=FullYear&year={month.Year}&month={month.MonthNumber}");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsBusy = false; ProcessingMessage = string.Empty; }
    }

    [RelayCommand]
    private async Task DeleteMonth(ExpenseMonthMaintenanceItem? month)
    {
        if (month is null || !month.HasDeletableData || IsBusy || !await _deleteGate.WaitAsync(0)) return;
        _deletionReviewCancelled = false;
        IsBusy = true; Progress = 0; ProcessingMessage = "Preparing monthly deletion review...";
        try
        {
            await Task.Yield();
            var plan = await deletion.GetPlanAsync(month.Year, month.MonthNumber);
            if (plan.ImportIds.Length > 0)
            {
                var scope = await ChooseAsync($"Delete scope · {month.MonthName} {month.Year}",
                    $"Expense bills: {plan.Expenses.Count()}\nImported Income records: {plan.ImportedIncome.Count()}\n\nChoose what to review. Nothing is deleted until the final Delete confirmation.",
                    new("whole-month", "Whole month", "All expense bills and imported Income in this month. Untracked Income requires a separate review."),
                    new("one-import", "One ETL import", "Only expense and Income records linked to the import you select, within this month."));
                if (scope?.Key == "one-import")
                {
                    var importIds = plan.ImportIds;
                    var choices = importIds.Select((id, index) => new MonthlyDeletionChoice(id, $"Select import {index + 1}",
                        $"{id}\nExpense bills: {plan.Expenses.Count(row => row.ImportId == id)} · Income records: {plan.ImportedIncome.Count(row => row.ImportId == id)}")).ToArray();
                    var selected = await ChooseAsync("Choose an ETL import", "Other imports and months will be kept.", choices);
                    if (selected is null) return;
                    var importId = selected.Key;
                    plan = new MonthlyDeletionPlan(month.Year, month.MonthNumber, importId,
                        plan.Records.Where(row => row.ImportId == importId).ToArray());
                }
                else if (scope?.Key != "whole-month") return;
            }

            var reviewed = new List<int>();
            if (plan.UntrackedIncome.Any())
            {
                var review = await ChooseAsync("Protect untracked Income",
                    $"{plan.UntrackedIncome.Count()} Income records have no import history. Keep manual Income; only include records you know came from ETL.",
                    new("keep-untracked", "Keep untracked Income", "Keep every untracked/manual Income record. Imported Income remains in the deletion plan."),
                    new("review-untracked", "Review individually", "Approve older ETL Credits one record at a time; no untracked Income is included automatically."));
                if (review?.Key == "review-untracked")
                {
                    ProcessingMessage = "Reviewing untracked Income...";
                    foreach (var row in plan.UntrackedIncome)
                    {
                        var choice = await ChooseAsync($"Review Income #{row.Id}",
                            $"{row.Date:dd/MM/yyyy}\n{row.ShopName} · {EuroFormatter.Format(row.Amount)}\n{row.Note}\n\nInclude only if you confirm this was imported from ETL.",
                            new("keep-income", "Keep this Income", "This record will not be deleted."),
                            new("include-income", "Include this record", "I confirm this is an older ETL Credit to include in the final deletion review."));
                        if (choice?.Key == "include-income") reviewed.Add(row.Id);
                        else if (choice?.Key != "keep-income") return;
                    }
                }
                else if (review?.Key != "keep-untracked") return;
            }
            var expenseCount = plan.Expenses.Count();
            var incomeCount = plan.ImportedIncome.Count() + reviewed.Count;
            if (expenseCount + incomeCount == 0)
            {
                await Shell.Current.DisplayAlertAsync("Nothing selected for deletion", "No expense bills or imported/reviewed Income records were selected. Untracked Income was kept. Nothing was deleted.", "OK");
                return;
            }
            var protectedCount = plan.UntrackedIncome.Count() - reviewed.Count;
            var confirmed = await ChooseAsync("Confirm monthly deletion",
                $"{month.MonthName} {month.Year}\n\nExpense bills to delete: {expenseCount}\nIncome records to delete: {incomeCount}\nUntracked Income to keep: {protectedCount}\n\nScope: {(plan.ImportId is null ? "whole month" : "selected ETL import only")}.\nOther months/imports outside this scope will be kept.\nThis is permanent. A failure rolls everything back.",
                new MonthlyDeletionChoice("delete", "Delete", "Permanently delete only the records listed above.", IsDestructive: true));
            if (confirmed?.Key != "delete") return;
            ProcessingMessage = $"Deleting {month.MonthName} {month.Year} data...";
            Progress = 0;
            IsDeletingData = true;
            await Task.Yield();
            var result = await deletion.DeleteAsync(plan, reviewed,
                new Progress<double>(value => { if (IsDeletingData) Progress = value; }));
            Progress = 100;
            IsDeletingData = false;
            var changedMonth = new DateTime(month.Year, month.MonthNumber, 1);
            WeakReferenceMessenger.Default.Send(new BillChangedMessage(new BillChanged(changedMonth, changedMonth, true)));
            await LoadAsync(keepBusyAfterLoad: true);
            await Shell.Current.DisplayAlertAsync("Monthly Data Removed",
                $"Deleted {result.ExpenseCount} expenses and {result.IncomeCount} Income records.\n{result.ProtectedIncomeCount} untracked Income records were kept. Other months/imports were not removed.", "OK");
        }
        catch (Exception ex) { errors.HandleError(ex); }
        finally { IsDeletingData = false; IsBusy = false; ProcessingMessage = string.Empty; _deleteGate.Release(); }
    }
}
