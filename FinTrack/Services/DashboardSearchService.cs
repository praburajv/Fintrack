using System.Globalization;
using FinTracker.Data;
using FinTracker.Models;
using FinTracker.Utilities;
using Microsoft.Data.Sqlite;

namespace FinTracker.Services;

public sealed class DashboardSearchService(BillTransactionRepository bills, IncomeTransactionRepository income, FinancialAccountContext accounts)
{
    public const int PageSize = 20;

    public async Task<IReadOnlyList<DashboardSearchResult>> SearchAsync(string text, int offset, CancellationToken token)
    {
        var prefix = text.Trim();
        if (prefix.Length == 0) return [];
        var account = accounts.RequireId();
        await bills.EnsureInitializedAsync();
        await income.EnsureInitializedAsync();
        token.ThrowIfCancellationRequested();
        if (accounts.Current?.Id != account) return [];
        await using var connection = new SqliteConnection(Constants.DatabasePath);
        await connection.OpenAsync(token);
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT Id,ShopId,ShopName,Reference,EntryDate,Amount,Kind,IconKey FROM (
 SELECT s.Id AS Id,s.Id AS ShopId,s.Name AS ShopName,NULL AS Reference,NULL AS EntryDate,0 AS Amount,0 AS Kind,COALESCE(c.IconKey,'default_shop') AS IconKey,0 AS SortKind
 FROM Shop s LEFT JOIN Category c ON c.Id=s.Category_Id AND c.FinancialAccountId=s.FinancialAccountId
 WHERE s.FinancialAccountId=@account AND s.Name LIKE @prefix ESCAPE '\' COLLATE NOCASE
 UNION ALL
 SELECT b.Id,b.Shop_Id,s.Name,b.bill_reference_no,b.Bill_date,b.Bill_value,0,COALESCE(c.IconKey,'default_shop'),1
 FROM Bill_transcation b JOIN Shop s ON s.Id=b.Shop_Id AND s.FinancialAccountId=b.FinancialAccountId
 LEFT JOIN Category c ON c.Id=s.Category_Id AND c.FinancialAccountId=b.FinancialAccountId
 WHERE b.FinancialAccountId=@account AND b.bill_reference_no LIKE @reference ESCAPE '\' COLLATE NOCASE
 UNION ALL
 SELECT i.Id,i.Shop_Id,s.Name,i.bill_reference_no,i.Transaction_date,i.Amount,1,COALESCE(c.IconKey,'default_shop'),1
 FROM income_transaction i JOIN Shop s ON s.Id=i.Shop_Id AND s.FinancialAccountId=i.FinancialAccountId
 LEFT JOIN Category c ON c.Id=s.Category_Id AND c.FinancialAccountId=i.FinancialAccountId
 WHERE i.FinancialAccountId=@account AND i.bill_reference_no LIKE @reference ESCAPE '\' COLLATE NOCASE
) ORDER BY SortKind,ShopName COLLATE NOCASE,Reference COLLATE NOCASE,EntryDate DESC,Kind,Id
LIMIT @limit OFFSET @offset";
        command.Parameters.AddWithValue("@account", account);
        command.Parameters.AddWithValue("@prefix", Escape(prefix) + "%");
        // Reference syntax must not restrict valid shop names (spaces, punctuation, Unicode).
        command.Parameters.AddWithValue("@reference", TransactionInput.IsReference(prefix) ? Escape(prefix) + "%" : "");
        command.Parameters.AddWithValue("@limit", PageSize + 1);
        command.Parameters.AddWithValue("@offset", Math.Max(0, offset));
        var results = new List<DashboardSearchResult>();
        await using var reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token)) results.Add(new DashboardSearchResult
        {
            Id = reader.GetInt32(0), ShopId = reader.GetInt32(1), ShopName = reader.GetString(2),
            BillReferenceNo = reader.IsDBNull(3) ? null : reader.GetString(3),
            Date = reader.IsDBNull(4) ? default : DateTime.Parse(reader.GetString(4), CultureInfo.InvariantCulture),
            Amount = Convert.ToDecimal(reader.GetValue(5), CultureInfo.InvariantCulture),
            TransactionType = reader.GetInt32(6) == 1 ? FinancialTransactionType.Income : FinancialTransactionType.Expense,
            IconFileName = CategoryIconCatalog.FileNameFor(reader.GetString(7))
        });
        return results;
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
