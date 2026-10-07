namespace FinTracker.Models;

public sealed record EtlImportResult(int TransactionsFound, int DebitTransactions, int CreditTransactions, int Imported, int IncomeImported, int DuplicatesSkipped, int InvalidOrErrors);
