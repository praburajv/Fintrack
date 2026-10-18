using System.Globalization;
using System.Text;
using FinTracker.Models;

namespace FinTracker.Services;

public interface IHistoryDocumentService
{
    Task<string> CreateHistoryPdfAsync(HistoryReport report, CancellationToken token = default);
}

public sealed partial class ExpenseStatementDocumentService : IHistoryDocumentService
{
    public Task<string> CreateHistoryPdfAsync(HistoryReport report, CancellationToken token = default) => Task.Run(() =>
    {
        var pages = new List<string>();
        var content = new StringBuilder();
        var y = 0d;
        void Start()
        {
            content = new StringBuilder();
            AddText(content, LeftMargin, PageHeight - 42, 15, report.Title + " - Search History", true);
            AddText(content, LeftMargin, PageHeight - 62, 9, Fit(report.Company, 36));
            AddRightAlignedText(content, PageWidth - LeftMargin, PageHeight - 62, 9, $"Page {pages.Count + 1} | {report.CurrencyCode}");
            y = PageHeight - 84;
        }
        void Line(string text, bool bold = false, int indent = 0)
        {
            // Wrap long names/notes instead of truncating the report's evidence.
            var remaining = text.Replace('\r', ' ').Replace('\n', ' ');
            do
            {
                token.ThrowIfCancellationRequested();
                if (y < BottomMargin + 20) { pages.Add(content.ToString()); Start(); }
                // Even Helvetica's widest glyph (@/W) must fit inside the margins.
                var capacity = (int)((PageWidth - 2 * LeftMargin - indent) / (9 * 1.05));
                var length = Math.Min(remaining.Length, Math.Max(1, capacity));
                if (length < remaining.Length)
                {
                    var space = remaining.LastIndexOf(' ', length - 1, length);
                    if (space > length / 2) length = space;
                }
                AddText(content, LeftMargin + indent, y, 9, remaining[..length], bold);
                y -= 15;
                remaining = remaining[length..].TrimStart();
            } while (remaining.Length > 0);
        }
        string Money(decimal amount) => $"{amount.ToString("N2", CultureInfo.InvariantCulture)} {report.CurrencyCode}";
        void Group(HistoryGroup group, int level)
        {
            Line($"{group.Title} | {group.Count} bills | Total: {Money(group.Total)}", true, level * 10);
            foreach (var child in group.Children) Group(child, level + 1);
            foreach (var bill in group.Bills)
            {
                Line($"{bill.Date:dd/MM/yyyy} | {bill.ShopName} | {Money(bill.Amount)}", false, (level + 1) * 10);
                Line($"Payment: {bill.PaymentMode}" + (bill.Reference is null ? "" : $" | Reference: {bill.Reference}"), false, (level + 1) * 10);
                if (!string.IsNullOrWhiteSpace(bill.Note)) Line($"Note: {bill.Note}", false, (level + 1) * 10);
                y -= 4;
            }
            y -= 5;
        }
        Start();
        Line("Company: " + report.Company);
        Line($"{report.Mode}: {report.Selection}", true);
        Line("Selected years: " + string.Join(", ", report.Years));
        Line("Order: " + report.OrderText);
        Line($"Bills: {report.Count} | Overall total: {Money(report.Total)}", true);
        y -= 8;
        foreach (var group in report.Groups) Group(group, 0);
        pages.Add(content.ToString());
        var path = Path.Combine(FileSystem.CacheDirectory, report.FileName);
        try { WritePdf(path, pages, token); return path; }
        catch { if (File.Exists(path)) File.Delete(path); throw; }
    }, token);
}
