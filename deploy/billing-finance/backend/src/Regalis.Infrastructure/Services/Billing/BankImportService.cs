using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// Bank statement import (build doc §8): the CSV or Excel exactly as the bank gives it, the
/// columns matched by the admin and remembered per account, a preview that saves nothing,
/// then the import. Duplicates are spotted on the bank's own reference, or date + amount +
/// the first 40 characters of the description; importing the same file twice adds nothing.
/// A row that cannot be read stops nothing. A batch can be undone while none of it is matched.
/// </summary>
public class BankImportService(RegalisDbContext db, ICurrentUser currentUser, IDateTimeProvider clock) : IBankImportService
{
    public static readonly string[] DateFormats = { "dd/MM/yyyy", "dd/MM/yy", "dd-MM-yyyy", "dd-MM-yy", "dd-MMM-yyyy", "dd-MMM-yy", "dd MMM yyyy", "dd.MM.yyyy", "yyyy-MM-dd", "MM/dd/yyyy" };

    private sealed record RawRow(int RowNumber, Dictionary<string, object?> Cells);

    private sealed record ParsedRow(int RowNumber, DateOnly? Date, string Description, decimal Out, decimal In, string? Reference, decimal? Balance, string? Error);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<BankColumnMappingDto?> GetMappingAsync(int bankAccountId, CancellationToken ct = default)
    {
        BankColumnMapping? row = await db.Set<BankColumnMapping>().AsNoTracking().FirstOrDefaultAsync(m => m.BankAccountId == bankAccountId, ct);
        return row == null ? null : JsonSerializer.Deserialize<BankColumnMappingDto>(row.MappingJson, Json);
    }

    // ── Reading the file ───────────────────────────────────────────────────────────

    private static (List<string> Headers, List<RawRow> Rows) Read(BankUploadInput file)
    {
        if (file.Length <= 0) throw new BusinessRuleException("Choose the statement file downloaded from the bank.");
        if (file.Length > 10L * 1024 * 1024) throw new BusinessRuleException("The statement file can be up to 10 MB.");
        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        using var ms = new MemoryStream();
        file.Content.CopyTo(ms);
        ms.Position = 0;
        List<List<object?>> grid = ext switch
        {
            ".xlsx" or ".xlsm" => ReadExcel(ms),
            ".csv" or ".txt" => ReadCsv(ms),
            _ => throw new BusinessRuleException("Upload the statement as CSV or Excel (.xlsx). An old .xls file can be saved as .xlsx from Excel first.")
        };
        int headerIndex = grid.FindIndex(r =>
            r.Count(c => !string.IsNullOrWhiteSpace(Convert.ToString(c, CultureInfo.InvariantCulture))) >= 3
            && r.Any(c => Convert.ToString(c, CultureInfo.InvariantCulture)?.Contains("date", StringComparison.OrdinalIgnoreCase) == true));
        if (headerIndex < 0) headerIndex = grid.FindIndex(r => r.Count(c => !string.IsNullOrWhiteSpace(Convert.ToString(c, CultureInfo.InvariantCulture))) >= 3);
        if (headerIndex < 0) throw new BusinessRuleException("No column headings were found in this file.");
        var headers = new List<string>();
        foreach (object? h in grid[headerIndex])
        {
            string name = (Convert.ToString(h, CultureInfo.InvariantCulture) ?? "").Trim();
            if (name.Length == 0) name = $"Column {headers.Count + 1}";
            while (headers.Contains(name)) name += " (2)";
            headers.Add(name);
        }
        var rows = new List<RawRow>();
        for (int i = headerIndex + 1; i < grid.Count; i++)
        {
            List<object?> r = grid[i];
            if (r.All(c => string.IsNullOrWhiteSpace(Convert.ToString(c, CultureInfo.InvariantCulture)))) continue;
            var cells = new Dictionary<string, object?>();
            for (int c = 0; c < headers.Count; c++) cells[headers[c]] = c < r.Count ? r[c] : null;
            rows.Add(new RawRow(i + 1, cells));
        }
        return (headers, rows);
    }

    private static List<List<object?>> ReadExcel(Stream s)
    {
        using var wb = new XLWorkbook(s);
        IXLWorksheet ws = wb.Worksheets.First();
        var grid = new List<List<object?>>();
        IXLRange? used = ws.RangeUsed();
        if (used == null) return grid;
        int lastCol = used.LastColumn().ColumnNumber();
        for (int r = 1; r <= used.LastRow().RowNumber(); r++)
        {
            var row = new List<object?>();
            for (int c = 1; c <= lastCol; c++)
            {
                IXLCell cell = ws.Cell(r, c);
                row.Add(cell.DataType switch
                {
                    XLDataType.DateTime => cell.GetDateTime(),
                    XLDataType.Number => cell.GetDouble(),
                    _ => cell.GetFormattedString()
                });
            }
            grid.Add(row);
        }
        return grid;
    }

    private static List<List<object?>> ReadCsv(Stream s)
    {
        string text = new StreamReader(s, Encoding.UTF8, true).ReadToEnd();
        string firstLines = string.Join("\n", text.Split('\n').Take(20));
        char sep = new[] { ',', ';', '\t', '|' }.OrderByDescending(c => firstLines.Count(x => x == c)).First();
        var grid = new List<List<object?>>();
        var row = new List<object?>();
        var cell = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else cell.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == sep) { row.Add(cell.ToString()); cell.Clear(); }
            else if (ch == '\n' || ch == '\r')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                row.Add(cell.ToString()); cell.Clear();
                grid.Add(row); row = new List<object?>();
            }
            else cell.Append(ch);
        }
        if (cell.Length > 0 || row.Count > 0) { row.Add(cell.ToString()); grid.Add(row); }
        return grid;
    }

    private static BankColumnMappingDto Guess(List<string> headers)
    {
        string? Find(params string[] words) => headers.FirstOrDefault(h => words.Any(w => h.Contains(w, StringComparison.OrdinalIgnoreCase)));
        string? date = headers.FirstOrDefault(h => h.Contains("value date", StringComparison.OrdinalIgnoreCase)) == null
            ? Find("txn date", "transaction date", "tran date", "date") : Find("txn date", "transaction date", "tran date", "date");
        return new BankColumnMappingDto(date, Find("narration", "description", "particulars", "remarks", "details"),
            Find("withdrawal", "debit", "dr amount", "money out", "paid out"), Find("deposit", "credit", "cr amount", "money in", "paid in"),
            null, Find("ref", "utr", "cheque", "chq"), Find("balance"), "dd/MM/yyyy");
    }

    private static string? Text(RawRow r, string? column)
    {
        if (column == null || !r.Cells.TryGetValue(column, out object? v) || v == null) return null;
        string s = v is double d ? d.ToString(CultureInfo.InvariantCulture) : Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        s = s.Trim();
        return s.Length == 0 ? null : s;
    }

    private static readonly Regex MoneyNoise = new(@"[₹,\s]|INR|Rs\.?", RegexOptions.IgnoreCase);

    private static bool TryMoney(RawRow r, string? column, out decimal value, out bool present)
    {
        value = 0m;
        present = false;
        if (column == null || !r.Cells.TryGetValue(column, out object? v) || v == null) return true;
        if (v is double d) { value = (decimal)d; present = value != 0m; return true; }
        string s = (Convert.ToString(v, CultureInfo.InvariantCulture) ?? "").Trim();
        if (s.Length == 0 || s == "-") return true;
        bool negative = s.StartsWith('-') || s.StartsWith('(') || s.EndsWith("Dr", StringComparison.OrdinalIgnoreCase);
        s = MoneyNoise.Replace(s, "").Replace("(", "").Replace(")", "");
        s = Regex.Replace(s, "(Cr|Dr)$", "", RegexOptions.IgnoreCase).TrimStart('-');
        if (!decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal parsed)) return false;
        value = negative ? -parsed : parsed;
        present = value != 0m;
        return true;
    }

    private static List<ParsedRow> Parse(List<RawRow> raw, BankColumnMappingDto m)
    {
        var result = new List<ParsedRow>();
        foreach (RawRow r in raw)
        {
            string description = (Text(r, m.DescriptionColumn) ?? "").Replace('\n', ' ');
            string? reference = Text(r, m.ReferenceColumn);
            if (reference != null && Regex.IsMatch(reference, "^0+$")) reference = null;
            DateOnly? date = null;
            object? rawDate = m.DateColumn != null && r.Cells.TryGetValue(m.DateColumn, out object? dv) ? dv : null;
            if (rawDate is DateTime dt) date = DateOnly.FromDateTime(dt);
            else
            {
                string? ds = Text(r, m.DateColumn);
                if (ds != null)
                {
                    string first = ds.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0];
                    if (DateTime.TryParseExact(ds, m.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out DateTime p1)
                        || DateTime.TryParseExact(first, m.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out p1))
                        date = DateOnly.FromDateTime(p1);
                    else
                    {
                        result.Add(new ParsedRow(r.RowNumber, null, description, 0m, 0m, reference, null,
                            $"Row {r.RowNumber} reads \"{ds}\", which is not a date in {m.DateFormat.ToUpperInvariant()}. Check the date format."));
                        continue;
                    }
                }
            }
            // A statement often ends with totals or a closing note: no date and no reference.
            if (date == null)
            {
                if (description.Length > 0 || reference != null)
                    result.Add(new ParsedRow(r.RowNumber, null, description, 0m, 0m, reference, null, $"Row {r.RowNumber} has no date."));
                continue;
            }
            decimal outAmt = 0m, inAmt = 0m;
            bool ok = true;
            if (m.AmountColumn != null)
            {
                ok = TryMoney(r, m.AmountColumn, out decimal signed, out _);
                if (signed < 0m) outAmt = -signed; else inAmt = signed;
            }
            else
            {
                ok &= TryMoney(r, m.OutColumn, out outAmt, out _);
                ok &= TryMoney(r, m.InColumn, out inAmt, out _);
                outAmt = Math.Abs(outAmt);
                inAmt = Math.Abs(inAmt);
            }
            TryMoney(r, m.BalanceColumn, out decimal bal, out bool hasBal);
            decimal? balance = hasBal || (m.BalanceColumn != null && Text(r, m.BalanceColumn) != null) ? bal : null;
            if (!ok) { result.Add(new ParsedRow(r.RowNumber, date, description, 0m, 0m, reference, balance, $"Row {r.RowNumber}: the amount cannot be read.")); continue; }
            if (outAmt == 0m && inAmt == 0m) { result.Add(new ParsedRow(r.RowNumber, date, description, 0m, 0m, reference, balance, $"No amount - row {r.RowNumber}")); continue; }
            if (outAmt != 0m && inAmt != 0m) { result.Add(new ParsedRow(r.RowNumber, date, description, outAmt, inAmt, reference, balance, $"Row {r.RowNumber} has money both in and out.")); continue; }
            if (description.Length == 0) description = reference ?? "(no description)";
            result.Add(new ParsedRow(r.RowNumber, date, description.Length > 500 ? description[..500] : description,
                FinanceRules.Round(outAmt), FinanceRules.Round(inAmt), reference?.Length > 120 ? reference[..120] : reference, balance, null));
        }
        return result;
    }

    private static string FallbackKey(DateOnly date, decimal amount, BankTransactionDirection dir, string description) =>
        $"{date:yyyyMMdd}|{amount:0.00}|{(byte)dir}|{(description.Length > 40 ? description[..40] : description).Trim().ToUpperInvariant()}";

    /// <summary>Classifies each row as new, already here, or an error. Nothing is saved.</summary>
    private async Task<List<BankImportRowDto>> ClassifyAsync(int bankAccountId, List<ParsedRow> rows, CancellationToken ct)
    {
        var dated = rows.Where(r => r.Error == null && r.Date != null).ToList();
        var refs = dated.Where(r => r.Reference != null).Select(r => r.Reference!).Distinct().ToList();
        var existingRefs = refs.Count == 0 ? new HashSet<string>() : (await db.BankTransactions.IgnoreQueryFilters()
            .Where(t => t.BankAccountId == bankAccountId && t.BankReferenceId != null && refs.Contains(t.BankReferenceId))
            .Select(t => t.BankReferenceId!).ToListAsync(ct)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        DateOnly? min = dated.Count == 0 ? null : dated.Min(r => r.Date);
        DateOnly? max = dated.Count == 0 ? null : dated.Max(r => r.Date);
        var existingKeys = new HashSet<string>();
        if (min != null)
        {
            var lines = await db.BankTransactions.Where(t => t.BankAccountId == bankAccountId && t.TransactionDate >= min && t.TransactionDate <= max)
                .Select(t => new { t.TransactionDate, t.Amount, t.Direction, t.Narration }).ToListAsync(ct);
            foreach (var l in lines) existingKeys.Add(FallbackKey(l.TransactionDate, l.Amount, l.Direction, l.Narration));
        }
        var locked = (await db.Set<PeriodLock>().Where(p => p.IsLocked).Select(p => p.Period).ToListAsync(ct)).ToHashSet();
        var seenRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenKeys = new HashSet<string>();
        var result = new List<BankImportRowDto>();
        foreach (ParsedRow r in rows)
        {
            if (r.Error != null)
            {
                result.Add(new BankImportRowDto(r.RowNumber, r.Date, r.Description, r.Out, r.In, r.Reference, r.Balance, "error", r.Error));
                continue;
            }
            DateOnly date = r.Date!.Value;
            var dir = r.In > 0m ? BankTransactionDirection.Credit : BankTransactionDirection.Debit;
            string key = FallbackKey(date, r.In > 0m ? r.In : r.Out, dir, r.Description);
            bool duplicate = r.Reference != null
                ? existingRefs.Contains(r.Reference) || !seenRefs.Add(r.Reference)
                : existingKeys.Contains(key) || !seenKeys.Add(key);
            if (duplicate)
                result.Add(new BankImportRowDto(r.RowNumber, date, r.Description, r.Out, r.In, r.Reference, r.Balance, "duplicate", "Already here - skipped"));
            else if (locked.Contains(FinanceRules.Period(date)))
                result.Add(new BankImportRowDto(r.RowNumber, date, r.Description, r.Out, r.In, r.Reference, r.Balance, "error", $"{FinanceRules.MonthName(FinanceRules.Period(date))} is locked - row {r.RowNumber} left out"));
            else
                result.Add(new BankImportRowDto(r.RowNumber, date, r.Description, r.Out, r.In, r.Reference, r.Balance, "new", null));
        }
        return result;
    }

    private static void Validate(BankColumnMappingDto m)
    {
        if (string.IsNullOrWhiteSpace(m.DateColumn) || string.IsNullOrWhiteSpace(m.DescriptionColumn)
            || (string.IsNullOrWhiteSpace(m.AmountColumn) && string.IsNullOrWhiteSpace(m.OutColumn) && string.IsNullOrWhiteSpace(m.InColumn)))
            throw new BusinessRuleException("Tell us which column holds the date, the description and the amount.");
        if (string.IsNullOrWhiteSpace(m.DateFormat)) throw new BusinessRuleException("Choose the date format.");
    }

    private async Task<BankAccount> AccountAsync(int bankAccountId, CancellationToken ct) =>
        await db.BankAccounts.FirstOrDefaultAsync(a => a.BankAccountId == bankAccountId, ct) ?? throw new BusinessRuleException("Choose the bank account.");

    public async Task<BankImportPreviewDto> ParseAsync(int bankAccountId, BankUploadInput file, BankColumnMappingDto? mapping, CancellationToken ct = default)
    {
        await AccountAsync(bankAccountId, ct);
        var (headers, raw) = Read(file);
        BankColumnMappingDto? saved = await GetMappingAsync(bankAccountId, ct);
        BankColumnMappingDto applied = mapping ?? (saved != null && saved.DateColumn != null && headers.Contains(saved.DateColumn) ? saved : Guess(headers));
        List<BankImportRowDto> rows;
        try
        {
            Validate(applied);
            rows = await ClassifyAsync(bankAccountId, Parse(raw, applied), ct);
        }
        catch (BusinessRuleException) when (mapping == null)
        {
            rows = new List<BankImportRowDto>();
        }
        var dated = rows.Where(r => r.Date != null).ToList();
        return new BankImportPreviewDto(bankAccountId, Path.GetFileName(file.FileName), headers, raw.Count,
            dated.Count == 0 ? null : dated.Min(r => r.Date), dated.Count == 0 ? null : dated.Max(r => r.Date), saved, applied, rows,
            rows.Count(r => r.Result == "new"), rows.Count(r => r.Result == "duplicate"), rows.Count(r => r.Result == "error"), DateFormats);
    }

    public async Task<BankImportResultDto> CommitAsync(int bankAccountId, BankUploadInput file, BankColumnMappingDto mapping, CancellationToken ct = default)
    {
        Validate(mapping);
        var (_, raw) = Read(file);
        List<ParsedRow> parsed = Parse(raw, mapping);
        return await FinanceRules.InTransactionAsync(db, async () =>
        {
            BankAccount account = await AccountAsync(bankAccountId, ct);
            List<BankImportRowDto> rows = await ClassifyAsync(bankAccountId, parsed, ct);
            var fresh = rows.Where(r => r.Result == "new").ToList();
            DateTime nowUtc = clock.UtcNow;
            int locationId = account.LocationId ?? await db.Locations.Where(l => l.IsDefault).Select(l => (int?)l.LocationId).FirstOrDefaultAsync(ct)
                ?? await db.Locations.Select(l => l.LocationId).FirstAsync(ct);
            string code = $"IMP-{nowUtc:yyyyMMdd-HHmmss}-{bankAccountId}-{Guid.NewGuid().ToString("N")[..6]}";
            DateOnly? first = rows.Where(r => r.Date != null).Select(r => r.Date).Min();
            decimal before = await BalanceAsync(account, first, ct);

            var batch = new BankImportBatch
            {
                BatchCode = code,
                BankAccountId = bankAccountId,
                FileName = Path.GetFileName(file.FileName),
                RowCount = rows.Count,
                ImportedCount = fresh.Count,
                SkippedCount = rows.Count(r => r.Result == "duplicate"),
                ErrorCount = rows.Count(r => r.Result == "error"),
                FirstDate = first,
                LastDate = rows.Where(r => r.Date != null).Select(r => r.Date).Max(),
                ColumnMappingJson = JsonSerializer.Serialize(mapping, Json),
                ImportedByUserId = currentUser.UserId,
                ImportedUtc = nowUtc
            };
            // The file's own balances: the last row in date order closes it; the first opens it.
            var withBalance = rows.Where(r => r.Date != null && r.Balance != null).ToList();
            decimal? fileClosing = null, fileOpening = null;
            if (withBalance.Count > 0)
            {
                bool descending = withBalance.Count > 1 && withBalance[0].Date > withBalance[^1].Date;
                var chronological = descending ? Enumerable.Reverse(withBalance).ToList() : withBalance;
                fileClosing = chronological[^1].Balance;
                var firstRow = chronological[0];
                fileOpening = firstRow.Balance - firstRow.In + firstRow.Out;
            }
            batch.FileClosingBalance = fileClosing;
            db.Set<BankImportBatch>().Add(batch);

            foreach (BankImportRowDto r in fresh)
            {
                db.BankTransactions.Add(new BankTransaction
                {
                    BankAccountId = bankAccountId,
                    TransactionDate = r.Date!.Value,
                    Direction = r.In > 0m ? BankTransactionDirection.Credit : BankTransactionDirection.Debit,
                    Amount = r.In > 0m ? r.In : r.Out,
                    Narration = r.Description,
                    ReferenceNumber = r.Reference?.Length > 80 ? r.Reference[..80] : r.Reference,
                    BankReferenceId = r.Reference,
                    ReconciliationStatus = ReconciliationStatus.Unreconciled,
                    LocationId = locationId,
                    EnteredByUserId = currentUser.UserId,
                    ImportBatchId = code,
                    Remarks = $"Imported from {batch.FileName}, row {r.RowNumber}"
                });
            }
            BankColumnMapping? saved = await db.Set<BankColumnMapping>().FirstOrDefaultAsync(m => m.BankAccountId == bankAccountId, ct);
            if (saved == null)
            {
                saved = new BankColumnMapping { BankAccountId = bankAccountId };
                db.Set<BankColumnMapping>().Add(saved);
            }
            saved.MappingJson = batch.ColumnMappingJson;
            saved.DateFormat = mapping.DateFormat;
            await db.SaveChangesAsync(ct);

            decimal after = await BalanceAsync(account, null, ct);
            bool agree = fileClosing == null || fileClosing == after;
            string balanceMessage = fileClosing == null
                ? "The file has no balance column, so the balance could not be checked."
                : fileOpening != null && fileOpening != before
                    ? $"The file starts at {FinanceRules.Money(fileOpening.Value)} but Regalis has {FinanceRules.Money(before)}. A statement may be missing - check before matching."
                    : agree
                        ? $"Closing balance in the file {FinanceRules.Money(fileClosing.Value)} · balance in Regalis {FinanceRules.Money(after)} · they agree."
                        : $"Closing balance in the file {FinanceRules.Money(fileClosing.Value)} · balance in Regalis {FinanceRules.Money(after)} · they do not agree. A statement may be missing - check before matching.";
            string message = fresh.Count == 0 && batch.SkippedCount > 0 && batch.ErrorCount == 0
                ? "Every line in this file is already here. Nothing was imported."
                : $"{fresh.Count} new line{(fresh.Count == 1 ? "" : "s")} imported · {batch.SkippedCount} already here · {batch.ErrorCount} could not be read.";
            return new BankImportResultDto(batch.BankImportBatchId, code, fresh.Count, batch.SkippedCount, batch.ErrorCount, message,
                fileOpening, before, fileClosing, after, agree && (fileOpening == null || fileOpening == before), balanceMessage);
        }, ct);
    }

    /// <summary>Opening + credits - debits, of lines dated before a date (or all lines).</summary>
    private async Task<decimal> BalanceAsync(BankAccount account, DateOnly? before, CancellationToken ct)
    {
        var q = db.BankTransactions.Where(t => t.BankAccountId == account.BankAccountId);
        if (before != null) q = q.Where(t => t.TransactionDate < before);
        decimal net = await q.SumAsync(t => (decimal?)(t.Direction == BankTransactionDirection.Credit ? t.Amount : -t.Amount), ct) ?? 0m;
        return account.OpeningBalance + net;
    }

    public async Task<BankImportBatchDto> UndoAsync(long batchId, CancellationToken ct = default)
    {
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            BankImportBatch batch = await db.Set<BankImportBatch>().FirstOrDefaultAsync(b => b.BankImportBatchId == batchId, ct)
                ?? throw new NotFoundException("Import", batchId);
            if (batch.IsUndone) throw new BusinessRuleException("This import has already been undone.");
            List<BankTransaction> lines = await db.BankTransactions.Where(t => t.ImportBatchId == batch.BatchCode).ToListAsync(ct);
            var ids = lines.Select(l => l.BankTransactionId).ToList();
            var allocated = await db.Set<BankMatchAllocation>().Where(m => ids.Contains(m.BankTransactionId)).Select(m => m.BankTransactionId).ToListAsync(ct);
            int matched = lines.Where(l => l.ReconciliationStatus != ReconciliationStatus.Unreconciled || l.MatchedPaymentId != null || l.MatchedExpenseId != null)
                .Select(l => l.BankTransactionId).Concat(allocated).Distinct().Count();
            if (matched > 0)
                throw new BusinessRuleException($"{matched} line{(matched == 1 ? " in this batch has" : "s in this batch have")} already been matched, so the batch cannot be undone. Remove them one at a time.");
            foreach (BankTransaction l in lines)
            {
                await FinanceRules.EnsurePeriodOpenAsync(db, l.TransactionDate, ct, "this undo");
                // The bank reference is unique per account; keep it in the remarks so the line can be imported again.
                if (l.BankReferenceId != null)
                {
                    l.Remarks = $"{l.Remarks} · undone (bank ref {l.BankReferenceId})";
                    if (l.Remarks.Length > 500) l.Remarks = l.Remarks[..500];
                    l.BankReferenceId = null;
                }
                db.BankTransactions.Remove(l);
            }
            batch.IsUndone = true;
            batch.UndoneByUserId = currentUser.UserId;
            batch.UndoneUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return (await GetBatchesAsync(null, ct)).First(b => b.BankImportBatchId == batchId);
    }

    public async Task<IReadOnlyList<BankImportBatchDto>> GetBatchesAsync(int? bankAccountId, CancellationToken ct = default)
    {
        var q = db.Set<BankImportBatch>().AsNoTracking();
        if (bankAccountId != null) q = q.Where(b => b.BankAccountId == bankAccountId);
        return await (from b in q
                      join a in db.BankAccounts on b.BankAccountId equals a.BankAccountId
                      join u in db.Users on b.ImportedByUserId equals u.UserId into uj
                      from u in uj.DefaultIfEmpty()
                      orderby b.BankImportBatchId descending
                      select new BankImportBatchDto(b.BankImportBatchId, b.BatchCode, b.BankAccountId, a.BankName + " · " + a.AccountCode, b.FileName,
                          b.RowCount, b.ImportedCount, b.SkippedCount, b.ErrorCount, b.FirstDate, b.LastDate, u != null ? u.FullName : null,
                          b.ImportedUtc, b.IsUndone,
                          db.BankTransactions.Count(t => t.ImportBatchId == b.BatchCode && t.ReconciliationStatus != ReconciliationStatus.Unreconciled)))
            .Take(100).ToListAsync(ct);
    }
}
