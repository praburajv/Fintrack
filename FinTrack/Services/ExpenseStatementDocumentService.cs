using System.Globalization;
using System.Text;
using FinTracker.Models;

namespace FinTracker.Services;

public sealed partial class ExpenseStatementDocumentService : IExpenseStatementDocumentService
{
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double LeftMargin = 36;
    private const double BottomMargin = 42;
    private const double RowHeight = 17;

    public Task<string> CreatePdfAsync(ExpenseStatementSnapshot statement, CancellationToken cancellationToken = default)
        => Task.Run(() => CreatePdf(statement, cancellationToken), cancellationToken);

    public Task<string> CreateBudgetPdfAsync(BudgetReportSnapshot report, CancellationToken cancellationToken = default)
        => Task.Run(() => CreateBudgetPdf(report, cancellationToken), cancellationToken);

    private static string CreatePdf(ExpenseStatementSnapshot statement, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(statement);
        var pages = BuildPages(statement, cancellationToken);
        CleanupOldStatements();
        var baseName = Path.GetFileNameWithoutExtension(statement.FileName);
        var path = Path.Combine(FileSystem.CacheDirectory, $"{baseName}-{DateTime.Now:yyyyMMdd-HHmmssfff}.pdf");
        WritePdf(path, pages, cancellationToken);
        return path;
    }

    private static string CreateBudgetPdf(BudgetReportSnapshot report, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(report);
        var pages = BuildBudgetPages(report, cancellationToken);
        CleanupOldStatements();
        var baseName = Path.GetFileNameWithoutExtension(report.FileName);
        var path = Path.Combine(FileSystem.CacheDirectory, $"{baseName}-{DateTime.Now:yyyyMMdd-HHmmssfff}.pdf");
        WritePdf(path, pages, cancellationToken);
        return path;
    }

    private static void CleanupOldStatements()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            foreach (var path in Directory.EnumerateFiles(FileSystem.CacheDirectory, "Expense-Statement-*.pdf"))
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                    File.Delete(path);
            foreach (var path in Directory.EnumerateFiles(FileSystem.CacheDirectory, "Budget-Report-*.pdf"))
                if (File.GetLastWriteTimeUtc(path) < cutoff)
                    File.Delete(path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Expense statement cache cleanup skipped: {ex.GetType().Name}");
        }
    }

    private static List<string> BuildBudgetPages(BudgetReportSnapshot report, CancellationToken cancellationToken)
    {
        var pages = new List<string>();
        var content = new StringBuilder();
        var y = StartBudgetPage(content, report, 1);

        AddText(content, LeftMargin, y, 13, "Budget Summary", bold: true);
        y -= 25;
        AddBudgetSummaryRow(content, ref y, "Monthly Income", report.MonthlyIncomeText, report.CurrencyCode);
        AddBudgetSummaryRow(content, ref y, "Monthly Budget", report.MonthlyBudgetText, report.CurrencyCode);
        AddBudgetSummaryRow(content, ref y, "Actual Expenses", report.ActualExpenseText, report.CurrencyCode);
        AddBudgetSummaryRow(content, ref y, "Remaining Budget", report.RemainingBudgetText, report.CurrencyCode);
        AddBudgetSummaryRow(content, ref y, "Budget Used", report.BudgetUsedPercentText, report.CurrencyCode, currency: false);
        AddBudgetSummaryRow(content, ref y, "Budget Health", report.BudgetHealthText, report.CurrencyCode, currency: false);
        y -= 12;

        AddText(content, LeftMargin, y, 13, "Category Breakdown", bold: true);
        y -= 23;
        AddBudgetCategoryHeader(content, y);
        y -= RowHeight;

        foreach (var category in report.Categories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (y < BottomMargin + 34)
            {
                pages.Add(content.ToString());
                content = new StringBuilder();
                y = StartBudgetPage(content, report, pages.Count + 1);
                AddText(content, LeftMargin, y, 13, "Category Breakdown (continued)", bold: true);
                y -= 23;
                AddBudgetCategoryHeader(content, y);
                y -= RowHeight;
            }

            AddText(content, 38, y, 8.5, Fit(category.CategoryName, 25));
            AddRightAlignedText(content, 280, y, 8.5, PdfSafeCurrency(category.BudgetedText, report.CurrencyCode));
            AddRightAlignedText(content, 390, y, 8.5, PdfSafeCurrency(category.ActualExpenseText, report.CurrencyCode));
            AddRightAlignedText(content, 500, y, 8.5, PdfSafeCurrency(category.RemainingText, report.CurrencyCode));
            AddText(content, 510, y, 8.5, Fit(category.StatusText, 14));
            AddLine(content, LeftMargin, y - 4, PageWidth - LeftMargin, y - 4, 0.25);
            y -= RowHeight;
        }

        pages.Add(content.ToString());
        return pages;
    }

    private static double StartBudgetPage(StringBuilder content, BudgetReportSnapshot report, int pageNumber)
    {
        AddText(content, LeftMargin, PageHeight - 45, 16, "Expense Tracker - Budget Report", bold: true);
        AddText(content, LeftMargin, PageHeight - 67, 12, $"Reporting Period: {report.PeriodTitle}", bold: true);
        AddRightAlignedText(content, PageWidth - LeftMargin, PageHeight - 45, 9, $"Page {pageNumber}");
        AddRightAlignedText(content, PageWidth - LeftMargin, PageHeight - 67, 9, $"Currency: {report.CurrencyCode}");
        AddLine(content, LeftMargin, PageHeight - 77, PageWidth - LeftMargin, PageHeight - 77, 0.8);
        return PageHeight - 104;
    }

    private static void AddBudgetSummaryRow(StringBuilder content, ref double y, string label, string value, string currencyCode, bool currency = true)
    {
        AddText(content, LeftMargin + 10, y, 10, label);
        AddRightAlignedText(content, 350, y, 10, currency ? PdfSafeCurrency(value, currencyCode) : value, bold: true);
        y -= 19;
    }

    private static void AddBudgetCategoryHeader(StringBuilder content, double y)
    {
        AddText(content, 38, y, 8.5, "Category", bold: true);
        AddRightAlignedText(content, 280, y, 8.5, "Budgeted", bold: true);
        AddRightAlignedText(content, 390, y, 8.5, "Actual", bold: true);
        AddRightAlignedText(content, 500, y, 8.5, "Remaining", bold: true);
        AddText(content, 510, y, 8.5, "Status", bold: true);
        AddLine(content, LeftMargin, y - 4, PageWidth - LeftMargin, y - 4, 0.8);
    }

    private static List<string> BuildPages(ExpenseStatementSnapshot statement, CancellationToken cancellationToken)
    {
        var pages = new List<string>();
        var content = new StringBuilder();
        var y = StartPage(content, statement, 1);

        foreach (var category in statement.Categories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (y < BottomMargin + 55)
            {
                pages.Add(content.ToString());
                content = new StringBuilder();
                y = StartPage(content, statement, pages.Count + 1);
            }

            AddText(content, LeftMargin, y, 12, category.CategoryName, bold: true);
            y -= 19;
            AddTableHeader(content, y);
            y -= RowHeight;

            foreach (var row in category.Rows)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (y < BottomMargin + 32)
                {
                    pages.Add(content.ToString());
                    content = new StringBuilder();
                    y = StartPage(content, statement, pages.Count + 1);
                    AddText(content, LeftMargin, y, 12, category.CategoryName + " (continued)", bold: true);
                    y -= 19;
                    AddTableHeader(content, y);
                    y -= RowHeight;
                }

                AddText(content, 38, y, 9, row.SerialNumber.ToString(CultureInfo.InvariantCulture));
                AddText(content, 68, y, 9, row.DateText);
                AddText(content, 137, y, 9, Fit(row.ShopName, 28));
                AddText(content, 312, y, 9, Fit(row.PaymentModeName, 18));
                AddRightAlignedText(content, 559, y, 9, PdfSafeCurrency(row.ValueText, statement.CurrencyCode));
                AddLine(content, LeftMargin, y - 4, PageWidth - LeftMargin, y - 4, 0.25);
                y -= RowHeight;
            }

            AddText(content, 312, y, 9, "Category Total", bold: true);
            AddRightAlignedText(content, 559, y, 9, PdfSafeCurrency(category.CategoryTotalText, statement.CurrencyCode), bold: true);
            y -= 24;
        }

        if (y < BottomMargin + 34)
        {
            pages.Add(content.ToString());
            content = new StringBuilder();
            y = StartPage(content, statement, pages.Count + 1);
        }

        AddLine(content, 300, y + 7, PageWidth - LeftMargin, y + 7, 1);
        AddText(content, 312, y - 7, 11, "Statement Total", bold: true);
        AddRightAlignedText(content, 559, y - 7, 11, PdfSafeCurrency(statement.OverallTotalText, statement.CurrencyCode), bold: true);
        y -= 38;
        if (statement.ShopVisits.Count > 0)
        {
            AddText(content, LeftMargin, y, 13, "Shop Visit", bold: true);
            y -= 22;
            foreach (var shop in statement.ShopVisits)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (y < BottomMargin + 30)
                {
                    pages.Add(content.ToString());
                    content = new StringBuilder();
                    y = StartPage(content, statement, pages.Count + 1);
                    AddText(content, LeftMargin, y, 13, "Shop Visit (continued)", bold: true);
                    y -= 22;
                }
                AddText(content, LeftMargin, y, 9, Fit(shop.ShopName, 22));
                AddText(content, 170, y, 9, new string('|', Math.Min(shop.VisitCount, 30)));
                AddText(content, 380, y, 9, $"{shop.VisitCount} visits");
                AddRightAlignedText(content, 559, y, 9, PdfSafeCurrency(FinTracker.Utilities.EuroFormatter.Format(shop.TotalValue), statement.CurrencyCode));
                y -= RowHeight;
            }
        }
        pages.Add(content.ToString());
        return pages;
    }

    private static double StartPage(StringBuilder content, ExpenseStatementSnapshot statement, int pageNumber)
    {
        AddText(content, LeftMargin, PageHeight - 45, 16, statement.PeriodTitle, bold: true);
        AddText(content, LeftMargin, PageHeight - 67, 14, statement.StatementTitle, bold: true);
        AddRightAlignedText(content, PageWidth - LeftMargin, PageHeight - 45, 9, $"Page {pageNumber}");
        AddRightAlignedText(content, PageWidth - LeftMargin, PageHeight - 67, 9, $"Currency: {statement.CurrencyCode}");
        AddLine(content, LeftMargin, PageHeight - 77, PageWidth - LeftMargin, PageHeight - 77, 0.8);
        return PageHeight - 100;
    }

    private static void AddTableHeader(StringBuilder content, double y)
    {
        AddText(content, 38, y, 9, "S.No", bold: true);
        AddText(content, 68, y, 9, "Date", bold: true);
        AddText(content, 137, y, 9, "Shop Name", bold: true);
        AddText(content, 312, y, 9, "Payment Mode", bold: true);
        AddRightAlignedText(content, 559, y, 9, "Value", bold: true);
        AddLine(content, LeftMargin, y - 4, PageWidth - LeftMargin, y - 4, 0.8);
    }

    private static void AddText(StringBuilder content, double x, double y, double size, string text, bool bold = false)
        => content.AppendFormat(CultureInfo.InvariantCulture,
            "BT /{0} {1:0.##} Tf 1 0 0 1 {2:0.##} {3:0.##} Tm ({4}) Tj ET\n",
            bold ? "F2" : "F1", size, x, y, EscapePdfText(text));

    private static void AddRightAlignedText(StringBuilder content, double right, double y, double size, string text, bool bold = false)
    {
        var estimatedWidth = text.Length * size * 0.52;
        AddText(content, Math.Max(LeftMargin, right - estimatedWidth), y, size, text, bold);
    }

    private static void AddLine(StringBuilder content, double x1, double y1, double x2, double y2, double width)
        => content.AppendFormat(CultureInfo.InvariantCulture, "{0:0.##} w {1:0.##} {2:0.##} m {3:0.##} {4:0.##} l S\n", width, x1, y1, x2, y2);

    private static string Fit(string? value, int maximumCharacters)
    {
        var text = string.IsNullOrWhiteSpace(value) ? "-" : value.Trim();
        return text.Length <= maximumCharacters ? text : text[..Math.Max(1, maximumCharacters - 3)] + "...";
    }

    private static string PdfSafeCurrency(string value, string currencyCode)
        => value.Replace("₹", "INR ", StringComparison.Ordinal)
            .Replace("€", "\u20ac", StringComparison.Ordinal)
            .Replace("¥", "\u00a5", StringComparison.Ordinal)
            .Replace("£", "\u00a3", StringComparison.Ordinal)
            .Replace("Rs", currencyCode == "LKR" ? "LKR" : "Rs", StringComparison.Ordinal);

    private static string EscapePdfText(string? value)
    {
        var result = new StringBuilder();
        foreach (var character in (value ?? string.Empty).Replace("→", "to", StringComparison.Ordinal).Replace('—', '-').Replace('–', '-'))
        {
            switch (character)
            {
                case '(' or ')' or '\\': result.Append('\\').Append(character); break;
                case '\u20ac': result.Append("\\200"); break;
                default:
                    if (character is >= ' ' and <= '~') result.Append(character);
                    else if (character is >= '\u00a0' and <= '\u00ff') result.Append('\\').Append(Convert.ToString(character, 8)!.PadLeft(3, '0'));
                    else result.Append('?');
                    break;
            }
        }
        return result.ToString();
    }

    private static void WritePdf(string path, IReadOnlyList<string> pageContents, CancellationToken cancellationToken)
    {
        var objects = new SortedDictionary<int, byte[]>();
        var pageIds = Enumerable.Range(0, pageContents.Count).Select(index => 5 + index * 2).ToArray();
        objects[1] = Ascii("<< /Type /Catalog /Pages 2 0 R >>");
        objects[2] = Ascii($"<< /Type /Pages /Count {pageIds.Length} /Kids [{string.Join(' ', pageIds.Select(id => $"{id} 0 R"))}] >>");
        objects[3] = Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");
        objects[4] = Ascii("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>");

        for (var index = 0; index < pageContents.Count; index++)
        {
            var pageId = pageIds[index];
            var contentId = pageId + 1;
            var stream = Ascii(pageContents[index]);
            objects[pageId] = Ascii($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {PageWidth} {PageHeight}] /Resources << /Font << /F1 3 0 R /F2 4 0 R >> >> /Contents {contentId} 0 R >>");
            objects[contentId] = Combine(Ascii($"<< /Length {stream.Length} >>\nstream\n"), stream, Ascii("\nendstream"));
        }

        using var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        Write(output, Ascii("%PDF-1.4\n% Expense statement\n"));
        var offsets = new long[objects.Keys.Max() + 1];
        foreach (var item in objects)
        {
            cancellationToken.ThrowIfCancellationRequested();
            offsets[item.Key] = output.Position;
            Write(output, Ascii($"{item.Key} 0 obj\n"));
            Write(output, item.Value);
            Write(output, Ascii("\nendobj\n"));
        }

        var xref = output.Position;
        Write(output, Ascii($"xref\n0 {offsets.Length}\n0000000000 65535 f \n"));
        for (var id = 1; id < offsets.Length; id++)
            Write(output, Ascii($"{offsets[id]:0000000000} 00000 n \n"));
        Write(output, Ascii($"trailer\n<< /Size {offsets.Length} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF"));
    }

    private static byte[] Ascii(string value) => Encoding.ASCII.GetBytes(value);
    private static byte[] Combine(params byte[][] arrays)
    {
        var result = new byte[arrays.Sum(array => array.Length)];
        var offset = 0;
        foreach (var array in arrays) { Buffer.BlockCopy(array, 0, result, offset, array.Length); offset += array.Length; }
        return result;
    }
    private static void Write(Stream stream, byte[] value) => stream.Write(value, 0, value.Length);
}
