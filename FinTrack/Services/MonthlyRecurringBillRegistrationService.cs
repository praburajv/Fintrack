using FinTracker.Data;
using FinTracker.Models;
using Microsoft.Data.Sqlite;
using System.Globalization;

namespace FinTracker.Services;

public sealed class MonthlyRecurringBillRegistrationService(RecurringBillRepository recurringBills, FinancialAccountContext accounts)
{
    public async Task<RecurringRegistrationResult> RegisterMonthlyRecurringBillsAsync(DateTime targetMonth)
    {
        targetMonth = new DateTime(targetMonth.Year, targetMonth.Month, 1);
        var templates = (await recurringBills.ListAsync()).Where(x => x.IsActive && x.StartMonth <= targetMonth && (x.EndMonth is null || x.EndMonth >= targetMonth)).ToList();
        if (templates.Count == 0) return new RecurringRegistrationResult([]);
        await using var connection = new SqliteConnection(Constants.DatabasePath); await connection.OpenAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        var period = RecurringBillRepository.MonthValue(targetMonth); var items = new List<RecurringRegistrationItem>();
        foreach (var bill in templates)
        {
            var command = connection.CreateCommand(); command.Transaction = transaction; command.CommandText = "INSERT OR IGNORE INTO Bill_transcation(Bill_date,Shop_Id,Payment_Mode_Id,Bill_value,Note,Recurring_Bill_Id,Recurring_Period,FinancialAccountId) VALUES(@date,@shop,@mode,@amount,@note,@recurring,@period,@account)";
            command.Parameters.AddWithValue("@account", accounts.RequireId());
            command.Parameters.AddWithValue("@date", targetMonth.ToString("O", CultureInfo.InvariantCulture)); command.Parameters.AddWithValue("@shop", bill.ShopId); command.Parameters.AddWithValue("@mode", bill.PaymentModeId); command.Parameters.AddWithValue("@amount", bill.Amount); command.Parameters.AddWithValue("@note", bill.Description); command.Parameters.AddWithValue("@recurring", bill.Id); command.Parameters.AddWithValue("@period", period);
            items.Add(new RecurringRegistrationItem(bill, await command.ExecuteNonQueryAsync() > 0));
        }
        await transaction.CommitAsync(); return new RecurringRegistrationResult(items);
    }
}
