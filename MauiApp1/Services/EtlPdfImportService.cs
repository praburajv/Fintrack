using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MauiApp1.Data;
using MauiApp1.Models;
using Microsoft.Extensions.Logging;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace MauiApp1.Services;

public sealed class EtlPdfImportService(
    BillTransactionRepository bills,
    IncomeTransactionRepository income,
    ShopRepository shops,
    CategoryRepository categories,
    PaymentModeRepository paymentModes,
    EtlTextMapRepository textMaps,
    EtlTextMappingService mappingService,
    ILogger<EtlPdfImportService> logger)
{
    // The bank statement uses MM.DD (for example, 2.27 means February 27).
    private static readonly Regex DatePattern = new(@"^(?<month>\d{1,2})[./-](?<day>\d{1,2})(?:[./-](?<year>20\d{2}))?$", RegexOptions.Compiled);
    private static readonly Regex YearPattern = new(@"\b(?<year>20\d{2})\b", RegexOptions.Compiled);
    private static readonly Regex MoneyPattern = new(@"^-?\d{1,3}(?:[.]\d{3})*(?:[,]\d{2})$|^-?\d+(?:[.,]\d{2})$", RegexOptions.Compiled);
    private static readonly string[] NoiseWords = ["DATA", "MOVIMENTO", "DESCRITIVO", "DEBITO", "CREDITO", "SALDO", "PAGINA", "PAG", "EXTRATO", "CONTA"];

    public async Task<EtlImportResult> ImportAsync(FileResult file, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (file is null || !Path.GetExtension(file.FileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Please select a PDF bank statement.");

        var shop = (await shops.ListAsync()).FirstOrDefault(x => x.Name.Equals("ETL_DATA_VAULT", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The shop ETL_DATA_VAULT was not found. Create it before importing.");
        var paymentModesList = await paymentModes.ListAsync();
        var mode = paymentModesList.FirstOrDefault(x => x.Name.Equals("Debit Card", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The payment mode Debit Card was not found. Create it before importing.");
        var incomeModeId = await paymentModes.EnsureIncomeModeAsync();
        var etlCategory = (await categories.ListAsync()).FirstOrDefault(x => x.Name.Equals("ETL_DT", StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The category ETL_DT was not found. Create it before importing.");
        if (shop.CategoryId != etlCategory.Id)
        {
            shop.CategoryId = etlCategory.Id;
            await shops.SaveItemAsync(shop);
        }
        var mappings = await textMaps.ListAsync();
        var availableShops = (await shops.ListAsync()).ToDictionary(x => x.Id);

        await using var stream = await file.OpenReadAsync();
        using var document = PdfDocument.Open(stream);
        var lines = GetTransactionLines(document);
        if (lines.Count == 0)
            throw new InvalidDataException("The PDF does not contain an EXTRATO DE to SALDO FINAL transaction section.");

        var year = FindYear(lines, document);
        var rows = ExtractRows(lines, year, progress, cancellationToken);
        var duplicateCounts = (await bills.ListAsync()).GroupBy(Key).ToDictionary(g => g.Key, g => g.Count());
        var incomeDuplicateCounts = (await income.ListAsync(DateTime.MinValue, DateTime.MaxValue)).GroupBy(item => Key(item)).ToDictionary(g => g.Key, g => g.Count());
        // Provenance is written with each new row; duplicate/manual rows are never relabelled.
        var importId = $"{file.FileName} — {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC — {Guid.NewGuid():N}";
        var imported = 0;
        var incomeImported = 0;
        var duplicates = 0;
        var errors = rows.Count(x => x.IgnoredReason is not null);

        foreach (var transaction in rows.Where(x => x.IgnoredReason is null).Select(x => x.Transaction!))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = Key(transaction);
            var duplicatesForType = transaction.IsIncome ? incomeDuplicateCounts : duplicateCounts;
            if (duplicatesForType.TryGetValue(key, out var count) && count > 0)
            {
                duplicatesForType[key] = count - 1;
                duplicates++;
                logger.LogDebug("ETL transaction ignored: duplicate source transaction on {Date} for {Amount}.", transaction.BillDate, transaction.IsIncome ? transaction.CreditAmount : transaction.DebitAmount);
                continue;
            }

            try
            {
                if (transaction.IsIncome)
                {
                    await income.SaveAsync(new IncomeTransaction { TransactionDate = transaction.BillDate, ShopId = shop.Id, PaymentModeId = incomeModeId, Amount = transaction.CreditAmount, Note = transaction.Description }, importId);
                    incomeImported++;
                }
                else
                {
                    var mappedShopId = mappingService.FindMappedShopId(transaction.Description, mappings, availableShops.Keys.ToHashSet()) ?? shop.Id;
                    await bills.SaveItemAsync(new BillTransaction { BillDate = transaction.BillDate, ShopId = mappedShopId, PaymentModeId = mode.Id, BillValue = transaction.DebitAmount, Note = transaction.Description }, importId);
                    imported++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                logger.LogError(ex, "ETL transaction could not be saved for {Date}.", transaction.BillDate);
            }
        }

        return new EtlImportResult(rows.Count, rows.Count(x => x.Transaction?.DebitAmount > 0), rows.Count(x => x.Transaction?.CreditAmount > 0), imported, incomeImported, duplicates, errors);
    }


    private List<PdfLine> GetTransactionLines(PdfDocument document)
    {
        var lines = document.GetPages().SelectMany(page => GroupRows(page.GetWords().ToList()).Select(words => new PdfLine(page.Number, words))).OrderBy(line => line.Page).ThenByDescending(line => line.Words[0].BoundingBox.Top).ToList();
        var result = new List<PdfLine>();
        var started = false;
        var ended = false;
        foreach (var line in lines)
        {
            if (!started)
            {
                if (!HasMarker(line.Words, "EXTRATO", "DE")) continue;
                started = true;
                logger.LogDebug("ETL PDF boundary found: EXTRATO DE on page {Page}.", line.Page);
                continue;
            }
            if (HasMarker(line.Words, "SALDO", "FINAL"))
            {
                logger.LogDebug("ETL PDF boundary reached: SALDO FINAL on page {Page}.", line.Page);
                ended = true;
                break;
            }
            result.Add(line);
        }
        if (!started) logger.LogWarning("ETL PDF boundary start marker EXTRATO DE was not found.");
        else if (!ended)
        {
            logger.LogWarning("ETL PDF boundary end marker SALDO FINAL was not found; no transactions will be imported.");
            return [];
        }
        return result;
    }

    private static int FindYear(IEnumerable<PdfLine> lines, PdfDocument document)
    {
        foreach (var page in document.GetPages())
            foreach (var word in page.GetWords())
                if (int.TryParse(YearPattern.Match(word.Text).Groups["year"].Value, out var year)) return year;
        foreach (var word in lines.SelectMany(x => x.Words))
            if (int.TryParse(YearPattern.Match(word.Text).Groups["year"].Value, out var year)) return year;
        throw new InvalidDataException("The transaction section does not contain a statement year.");
    }

    private List<ParsedRow> ExtractRows(IReadOnlyList<PdfLine> lines, int year, IProgress<string>? progress, CancellationToken token)
    {
        var result = new List<ParsedRow>();
        PendingTransaction? pending = null;
        string? deferredDescription = null;
        double deferredDescriptionY = 0;
        ColumnLayout? currentLayout = null;
        foreach (var line in lines)
        {
            token.ThrowIfCancellationRequested();
            if (TryGetLayout(line.Words, out var foundLayout)) currentLayout = foundLayout;
            if (IsNoiseRow(line.Words)) continue;
            if (TryParseTransaction(line.Words, year, currentLayout, out var transaction))
            {
                if (deferredDescription is not null)
                {
                    if (string.IsNullOrWhiteSpace(transaction.Description))
                        transaction.Description = deferredDescription;
                    else if (pending is not null)
                        pending.Description = JoinDescription(pending.Description, [deferredDescription]);
                    deferredDescription = null;
                }
                FlushPending(result, ref pending);
                pending = transaction;
                logger.LogDebug("ETL transaction candidate detected on page {Page}.", line.Page);
                continue;
            }
            if (!ContainsDate(line.Words) && !ContainsMoney(line.Words) && !IsNoiseRow(line.Words))
            {
                var words = pending is not null
                    ? DescriptionWords(line.Words, pending.DescriptionStart, pending.DescriptionEnd)
                    : line.Words.Where(word => word.BoundingBox.Left > 110 && word.BoundingBox.Right < 354).Select(word => word.Text).ToList();
                if (words.Count > 0)
                {
                    var fragment = string.Join(" ", words);
                    if (pending is not null && string.IsNullOrWhiteSpace(pending.Description))
                        pending.Description = JoinDescription(pending.Description, [fragment]);
                    else
                        deferredDescription = JoinDescription(deferredDescription ?? string.Empty, [fragment]);
                    deferredDescriptionY = line.Words[0].BoundingBox.Top;
                    continue;
                }
            }
            if (pending is not null && (ContainsDate(line.Words) || ContainsMoney(line.Words))) FlushPending(result, ref pending);
        }
        if (pending is not null && deferredDescription is not null) pending.Description = JoinDescription(pending.Description, [deferredDescription]);
        FlushPending(result, ref pending);
        progress?.Report("PDF transaction extraction completed.");
        return result;
    }

    private void FlushPending(List<ParsedRow> result, ref PendingTransaction? pending)
    {
        if (pending is null) return;
        var description = NormalizeDescription(pending.Description);
        if (string.IsNullOrWhiteSpace(description))
        {
            logger.LogDebug("ETL transaction ignored: invalid transaction structure (missing DESCRITIVO).");
            result.Add(ParsedRow.Ignored("Invalid transaction structure"));
        }
        else
        {
            pending.Description = description;
            var transaction = pending.ToTransaction();
            // Repeated date/amount/description values are legitimate separate
            // bank rows. Fragment continuation lines cannot become candidates
            // because they have no DATA LANC./DATA VALOR pair, so row identity
            // is supplied by the reconstructed PDF row itself rather than a
            // value-based de-duplication key.
            logger.LogDebug("ETL transaction accepted: {Date}, debit {Debit}, credit {Credit}, description length {DescriptionLength}.", transaction.BillDate, transaction.DebitAmount, transaction.CreditAmount, description.Length);
            result.Add(ParsedRow.Accepted(transaction));
        }
        pending = null;
    }

    private static bool TryParseTransaction(IReadOnlyList<Word> words, int year, ColumnLayout? layout, out PendingTransaction transaction)
    {
        transaction = null!;
        var dates = words.Where(IsDateColumnWord).ToList();
        if (dates.Count < 2 || layout is null || !TryFindAmounts(words, layout.Value, out var debitAmount, out var creditAmount)) return false;
        // DATA LANC. is the first date column and is the bill date. DATA VALOR
        // is the second date column and is used only to locate DESCRITIVO.
        var dateMatch = DatePattern.Match(dates[0].Text.Trim());
        if (!int.TryParse(dateMatch.Groups["day"].Value, out var day) || !int.TryParse(dateMatch.Groups["month"].Value, out var month)) return false;
        if (!int.TryParse(dateMatch.Groups["year"].Value, out var parsedYear)) parsedYear = year;
        DateTime date;
        try { date = new DateTime(parsedYear, month, day); } catch { return false; }
        var descriptionEnd = Math.Min(layout.Value.DebitX, layout.Value.CreditX);
        var descriptionStart = dates[1].BoundingBox.Right - 1;
        var description = string.Join(" ", words.Where(word => word.BoundingBox.Left > descriptionStart && word.BoundingBox.Right < descriptionEnd - 1).Select(word => word.Text));
        transaction = new PendingTransaction(date, debitAmount, creditAmount, description, descriptionStart, descriptionEnd, words[0].BoundingBox.Top);
        return true;
    }

    private static bool TryFindAmounts(IReadOnlyList<Word> words, ColumnLayout layout, out decimal debitAmount, out decimal creditAmount)
    {
        debitAmount = 0; creditAmount = 0;
        // Amounts are right-aligned. Each financial column has finite bounds:
        // a blank Credit must stay blank, never borrow the balance to its right.
        debitAmount = ReadColumnAmount(words, layout.Debit);
        creditAmount = ReadColumnAmount(words, layout.Credit);
        return debitAmount > 0 || creditAmount > 0;
    }

    private static decimal ReadColumnAmount(IReadOnlyList<Word> words, AmountColumn column)
    {
        var columnWords = words.Where(word => !IsDateColumnWord(word)
            && word.BoundingBox.Right >= column.Start && word.BoundingBox.Right < column.End)
            .OrderBy(word => word.BoundingBox.Left).ToList();
        var amountWord = columnWords.Where(word => MoneyPattern.IsMatch(word.Text.Trim()))
            .OrderBy(word => Math.Abs(word.BoundingBox.Right - column.Right)).FirstOrDefault();
        if (amountWord is null) return 0;

        var text = amountWord.Text.Trim();
        // PDF extraction may split a space-grouped amount ("2 197.45") into
        // separate words. Join only adjacent numeric groups in this same column.
        var next = amountWord;
        for (var index = columnWords.IndexOf(amountWord) - 1; index >= 0; index--)
        {
            var previous = columnWords[index];
            if (!Regex.IsMatch(previous.Text.Trim(), @"^-?\d{1,3}$")
                || next.BoundingBox.Left - previous.BoundingBox.Right > Math.Max(6, amountWord.BoundingBox.Height * 1.5)) break;
            text = previous.Text.Trim() + text;
            next = previous;
        }
        return TryMoney(text, out var amount) ? amount : 0;
    }

    private static bool TryGetLayout(IReadOnlyList<Word> words, out ColumnLayout layout)
    {
        var debit = words.FirstOrDefault(word => NormalizeToken(word.Text).StartsWith("DEBIT", StringComparison.Ordinal));
        var credit = words.FirstOrDefault(word => NormalizeToken(word.Text).StartsWith("CREDIT", StringComparison.Ordinal));
        if (debit is null || credit is null) { layout = default; return false; }
        var balance = words.FirstOrDefault(word => NormalizeToken(word.Text) == "SALDO");
        // SALDO is used only as a geometric boundary, never read as an amount.
        var headers = new[] { debit, credit, balance }.OfType<Word>().OrderBy(word => word.BoundingBox.Right).ToArray();
        var spacing = Math.Abs(debit.BoundingBox.Right - credit.BoundingBox.Right);
        if (spacing < 1) { layout = default; return false; }
        AmountColumn GetColumn(Word header)
        {
            var index = Array.IndexOf(headers, header);
            var right = header.BoundingBox.Right;
            var start = index > 0 ? (headers[index - 1].BoundingBox.Right + right) / 2 : right - spacing / 2;
            var end = index < headers.Length - 1 ? (right + headers[index + 1].BoundingBox.Right) / 2 : right + spacing / 2;
            return new AmountColumn(start, end, right);
        }
        layout = new ColumnLayout(debit.BoundingBox.Left, credit.BoundingBox.Left, GetColumn(debit), GetColumn(credit));
        return true;
    }

    private static bool IsContinuation(IReadOnlyList<Word> words, PendingTransaction pending) => !ContainsDate(words) && !ContainsMoney(words) && !IsNoiseRow(words) && DescriptionWords(words, pending.DescriptionStart, pending.DescriptionEnd).Count > 0;
    private static List<string> DescriptionWords(IReadOnlyList<Word> words, double descriptionStart, double descriptionEnd) => words.Where(word => word.BoundingBox.Left > descriptionStart && word.BoundingBox.Right < descriptionEnd - 1).Select(word => word.Text).ToList();
    private static bool IsNoiseRow(IReadOnlyList<Word> words)
    {
        if (words.Count == 0) return true;
        var tokens = words.Select(word => NormalizeToken(word.Text));
        return !ContainsDate(words) && !ContainsMoney(words) && tokens.Any(word => NoiseWords.Contains(word, StringComparer.Ordinal));
    }
    private static bool ContainsDate(IReadOnlyList<Word> words) => words.Any(IsDateColumnWord);
    private static bool IsDateColumnWord(Word word) => word.BoundingBox.Left < 110 && IsDateToken(word.Text);
    private static bool IsDateToken(string text)
    {
        var match = DatePattern.Match(text.Trim());
        if (!match.Success || !int.TryParse(match.Groups["month"].Value, out var month) || !int.TryParse(match.Groups["day"].Value, out var day)) return false;
        return month is >= 1 and <= 12 && day is >= 1 and <= 31;
    }
    private static bool ContainsMoney(IReadOnlyList<Word> words) => words.Any(word => MoneyPattern.IsMatch(word.Text.Trim()));

    private static bool HasMarker(IReadOnlyList<Word> words, string first, string second)
    {
        var tokens = words.Select(word => NormalizeToken(word.Text)).ToList();
        for (var i = 0; i < tokens.Count - 1; i++) if (tokens[i] == first && tokens[i + 1] == second) return true;
        return false;
    }

    private static IEnumerable<List<Word>> GroupRows(List<Word> words)
    {
        var rows = new List<(double Y, List<Word> Words)>();
        foreach (var word in words.OrderByDescending(x => x.BoundingBox.Top).ThenBy(x => x.BoundingBox.Left))
        {
            var row = rows.LastOrDefault();
            // Some bank statement words in the DESCRITIVO column sit a few
            // points above the dates/amounts on the same visual row. Keep
            // those fragments together; the 10+ point gap between actual
            // transaction rows still separates adjacent transactions.
            if (row.Words is null || Math.Abs(row.Y - word.BoundingBox.Top) > 8) rows.Add((word.BoundingBox.Top, [word])); else row.Words.Add(word);
        }
        return rows.Select(x => x.Words.OrderBy(y => y.BoundingBox.Left).ToList());
    }

    private static string NormalizeToken(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormD); var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized) if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetter(character)) builder.Append(char.ToUpperInvariant(character));
        return builder.ToString();
    }

    private static string NormalizeDescription(string value) => Regex.Replace(value, @"\s+", " ").Trim();
    private static string JoinDescription(string current, IEnumerable<string> continuation) => NormalizeDescription($"{current} {string.Join(" ", continuation)}");
    private static bool TryMoney(string value, out decimal amount) { var normalized = value.Contains(',') ? value.Replace(".", string.Empty).Replace(',', '.') : value; return decimal.TryParse(normalized, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out amount); }
    private static string Key(EtlBankTransaction x) => BuildKey(x.BillDate, x.IsIncome ? x.CreditAmount : x.DebitAmount, x.Description);
    private static string Key(BillTransaction x) => BuildKey(x.BillDate, x.BillValue, x.Note);
    private static string Key(IncomeTransaction x) => BuildKey(x.TransactionDate, x.Amount, x.Note);
    // SQLite can return 600 where the PDF supplied 600.00; monetary scale must
    // not change duplicate identity. Counts still preserve repeated bank rows.
    private static string BuildKey(DateTime date, decimal amount, string description) => $"{date:O}|{amount.ToString("F2", CultureInfo.InvariantCulture)}|{NormalizeDescription(description).ToUpperInvariant()}";

    private readonly record struct AmountColumn(double Start, double End, double Right);
    private readonly record struct ColumnLayout(double DebitX, double CreditX, AmountColumn Debit, AmountColumn Credit);
    private sealed record PdfLine(int Page, IReadOnlyList<Word> Words);
    private sealed class PendingTransaction(DateTime billDate, decimal debitAmount, decimal creditAmount, string description, double descriptionStart, double descriptionEnd, double sourceY)
    {
        public DateTime BillDate { get; } = billDate;
        public decimal DebitAmount { get; } = debitAmount;
        public decimal CreditAmount { get; } = creditAmount;
        public string Description { get; set; } = description;
        public double DescriptionStart { get; } = descriptionStart;
        public double DescriptionEnd { get; } = descriptionEnd;
        public double SourceY { get; } = sourceY;
        public EtlBankTransaction ToTransaction() => new() { BillDate = BillDate, DebitAmount = decimal.Round(DebitAmount, 2), CreditAmount = decimal.Round(CreditAmount, 2), Description = Description.Length > 1000 ? Description[..1000] : Description };
    }

    private sealed record ParsedRow(EtlBankTransaction? Transaction, string? IgnoredReason)
    {
        public static ParsedRow Accepted(EtlBankTransaction transaction) => new(transaction, null);
        public static ParsedRow Ignored(string reason) => new(null, reason);
    }
}
