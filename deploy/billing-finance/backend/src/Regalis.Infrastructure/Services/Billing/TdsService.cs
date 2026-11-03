using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// TDS (build doc §10): payable is what Regalis kept back from suppliers (written when a
/// vendor payment is posted), receivable is what customers kept back (written by bank
/// matching). Challans cover many deductions; a deduction sits on one challan only and a
/// challan cannot cover more than it paid. Rates keep an effective-date history. Nothing
/// is filed from here - the page produces the figures and the Excel for the accountant.
/// </summary>
public class TdsService(RegalisDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, IOptions<StorageOptions> storage) : ITdsService
{
    private static readonly Regex Bsr = new("^[0-9]{7}$");

    /// <summary>TDS deducted in a month is paid by the 7th of the next (30 April for March).</summary>
    public static DateOnly ChallanDueFor(DateOnly deductedOn) =>
        deductedOn.Month == 3 ? new DateOnly(deductedOn.Year, 4, 30) : new DateOnly(deductedOn.Year, deductedOn.Month, 1).AddMonths(1).AddDays(6);

    /// <summary>26Q: Q1 by 31 Jul, Q2 by 31 Oct, Q3 by 31 Jan, Q4 by 31 May.</summary>
    public static DateOnly ReturnDue(string fy, byte quarter)
    {
        int y = int.Parse(fy[..4]);
        return quarter switch { 1 => new DateOnly(y, 7, 31), 2 => new DateOnly(y, 10, 31), 3 => new DateOnly(y + 1, 1, 31), _ => new DateOnly(y + 1, 5, 31) };
    }

    private (string Fy, byte Q) Current(string? fy, byte? q)
    {
        DateOnly today = FinanceRules.Today(clock);
        return (string.IsNullOrWhiteSpace(fy) ? FinanceRules.FinancialYear(today) : fy, q ?? FinanceRules.Quarter(today));
    }

    public async Task<TdsSummaryDto> GetSummaryAsync(string? financialYear, byte? quarter, CancellationToken ct = default)
    {
        var (fy, q) = Current(financialYear, quarter);
        DateOnly today = FinanceRules.Today(clock);
        var payable = await db.Set<TdsEntry>().AsNoTracking().Where(t => t.Direction == TdsDirection.Payable).ToListAsync(ct);
        var thisQuarter = payable.Where(t => t.FinancialYear == fy && t.Quarter == q).ToList();
        var unpaid = payable.Where(t => t.TdsChallanId == null).ToList();
        var receivable = await db.Set<TdsEntry>().AsNoTracking().Where(t => t.Direction == TdsDirection.Receivable && t.FinancialYear == fy).ToListAsync(ct);
        var missing = receivable.Where(t => t.CertificateNo == null).ToList();
        // The next 26Q not yet past: last quarter's while it is still open, else this quarter's.
        int y = int.Parse(fy[..4]);
        (string Fy, byte Q) prev = q == 1 ? ($"{y - 1}-{y % 100:D2}", (byte)4) : (fy, (byte)(q - 1));
        (string nextFy, byte nextQ) = ReturnDue(prev.Fy, prev.Q) >= today ? prev : (fy, q);
        DateOnly due = ReturnDue(nextFy, nextQ);
        return new TdsSummaryDto(fy, q, thisQuarter.Sum(t => t.TdsAmount), thisQuarter.Select(t => t.SourceId).Distinct().Count(),
            unpaid.Sum(t => t.TdsAmount), unpaid.Count, unpaid.Count == 0 ? null : unpaid.Min(t => ChallanDueFor(t.DeductedOn)),
            receivable.Sum(t => t.TdsAmount), receivable.Select(t => t.PartyId).Distinct().Count(), missing.Count, missing.Sum(t => t.TdsAmount),
            $"26Q · Q{nextQ} {nextFy}", due);
    }

    private IQueryable<TdsEntryDto> Project(IQueryable<TdsEntry> q, DateOnly today) =>
        from t in q
        join c in db.Set<TdsChallan>() on t.TdsChallanId equals (long?)c.TdsChallanId into cj
        from c in cj.DefaultIfEmpty()
        select new TdsEntryDto(t.TdsEntryId, (byte)t.Direction, t.DeductedOn, t.PartyId, t.PartyName, t.PartyTaxId, t.Section, t.BaseAmount, t.Rate,
            t.TdsAmount, t.Quarter, t.FinancialYear, t.DocumentNumber, t.SourceType, t.SourceId, t.TdsChallanId, c != null ? c.ChallanNo : null,
            t.CertificateNo, t.CertificateReceivedOn, t.CertificatePath != null, false);

    public async Task<IReadOnlyList<TdsEntryDto>> GetEntriesAsync(byte direction, string? financialYear, byte? quarter, string? section, bool? open, CancellationToken ct = default)
    {
        DateOnly today = FinanceRules.Today(clock);
        IQueryable<TdsEntry> q = db.Set<TdsEntry>().AsNoTracking().Where(t => (byte)t.Direction == direction);
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(t => t.FinancialYear == financialYear);
        if (quarter != null) q = q.Where(t => t.Quarter == quarter);
        if (!string.IsNullOrWhiteSpace(section)) q = q.Where(t => t.Section == section);
        if (open == true) q = direction == (byte)TdsDirection.Payable ? q.Where(t => t.TdsChallanId == null) : q.Where(t => t.CertificateNo == null);
        var rows = await Project(q.OrderBy(t => t.DeductedOn).ThenBy(t => t.TdsEntryId), today).ToListAsync(ct);
        // A certificate still missing a month after its quarter closed is money that cannot be claimed.
        return rows.Select(r => r with
        {
            CertificateOverdue = r.Direction == (byte)TdsDirection.Receivable && r.CertificateNo == null
                                 && FinanceRules.QuarterRange(r.FinancialYear, r.Quarter).To.AddMonths(1) < today
        }).ToList();
    }

    public async Task<IReadOnlyList<TdsChallanDto>> GetChallansAsync(string? financialYear, CancellationToken ct = default)
    {
        var q = db.Set<TdsChallan>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(financialYear)) q = q.Where(c => c.FinancialYear == financialYear);
        return await q.OrderByDescending(c => c.PaidOn).Select(c => new TdsChallanDto(c.TdsChallanId, c.BsrCode, c.ChallanNo, c.PaidOn, c.Amount, c.Quarter,
            c.FinancialYear, db.Set<TdsEntry>().Count(t => t.TdsChallanId == c.TdsChallanId),
            db.Set<TdsEntry>().Where(t => t.TdsChallanId == c.TdsChallanId).Sum(t => (decimal?)t.TdsAmount) ?? 0m)).ToListAsync(ct);
    }

    public async Task<TdsChallanDto> RecordChallanAsync(RecordChallanRequest request, CancellationToken ct = default)
    {
        string bsr = request.BsrCode?.Trim() ?? "";
        string no = request.ChallanNo?.Trim() ?? "";
        if (!Bsr.IsMatch(bsr)) throw new BusinessRuleException("The BSR code is the bank's seven-digit code printed on the challan.");
        if (no.Length == 0 || no.Length > 10) throw new BusinessRuleException("Enter the challan serial number.");
        decimal amount = FinanceRules.Round(request.Amount);
        if (amount <= 0m) throw new BusinessRuleException("Enter the amount paid on the challan.");
        if (request.TdsEntryIds == null || request.TdsEntryIds.Count == 0) throw new BusinessRuleException("Tick the deductions this challan covers.");
        FinanceRules.EnsureNotFuture(request.PaidOn, clock, "The challan date cannot be in the future.");
        long id = await FinanceRules.InTransactionAsync(db, async () =>
        {
            await FinanceRules.EnsurePeriodOpenAsync(db, request.PaidOn, ct, "this challan");
            var entries = await db.Set<TdsEntry>().Where(t => request.TdsEntryIds.Contains(t.TdsEntryId)).ToListAsync(ct);
            if (entries.Count != request.TdsEntryIds.Distinct().Count()) throw new BusinessRuleException("One of the ticked deductions no longer exists.");
            if (entries.Any(t => t.Direction != TdsDirection.Payable)) throw new BusinessRuleException("A challan covers TDS Regalis deducted, not TDS deducted by customers.");
            var taken = entries.Where(t => t.TdsChallanId != null).ToList();
            if (taken.Count > 0)
            {
                string other = await db.Set<TdsChallan>().Where(c => c.TdsChallanId == taken[0].TdsChallanId).Select(c => c.ChallanNo).FirstAsync(ct);
                throw new BusinessRuleException($"{(taken.Count == 1 ? "One" : taken.Count.ToString())} of these deductions {(taken.Count == 1 ? "is" : "are")} already on challan {other}.");
            }
            decimal covered = entries.Sum(t => t.TdsAmount);
            if (covered > amount)
                throw new BusinessRuleException($"This challan is {FinanceRules.Money(amount)} but the deductions ticked come to {FinanceRules.Money(covered)}.");
            if (await db.Set<TdsChallan>().AnyAsync(c => c.BsrCode == bsr && c.ChallanNo == no && c.PaidOn == request.PaidOn, ct))
                throw new ConflictException($"Challan {no} of {request.PaidOn:dd MMM yyyy} is already recorded.", "DUPLICATE_CHALLAN");
            DateOnly first = entries.Min(t => t.DeductedOn);
            var challan = new TdsChallan { BsrCode = bsr, ChallanNo = no, PaidOn = request.PaidOn, Amount = amount, Quarter = FinanceRules.Quarter(first), FinancialYear = FinanceRules.FinancialYear(first) };
            db.Set<TdsChallan>().Add(challan);
            await db.SaveChangesAsync(ct);
            foreach (TdsEntry t in entries) t.TdsChallanId = challan.TdsChallanId;
            await db.SaveChangesAsync(ct);
            return challan.TdsChallanId;
        }, ct);
        return (await GetChallansAsync(null, ct)).First(c => c.TdsChallanId == id);
    }

    public async Task<TdsEntryDto> SaveCertificateAsync(long entryId, TdsCertificateRequest request, AttachmentUploadInput? file, CancellationToken ct = default)
    {
        string no = request.CertificateNo?.Trim() ?? "";
        if (no.Length == 0 || no.Length > 40) throw new BusinessRuleException("Enter the Form 16A certificate number.");
        FinanceRules.EnsureNotFuture(request.ReceivedOn, clock, "The certificate date cannot be in the future.");
        TdsEntry entry = await db.Set<TdsEntry>().FirstOrDefaultAsync(t => t.TdsEntryId == entryId, ct) ?? throw new NotFoundException("TDS entry", entryId);
        if (file != null)
        {
            var (path, _) = await FinanceRules.SaveUploadAsync(storage.Value.UploadRoot, "tds-certificates", entryId, file.FileName, file.Content, file.Length, ct);
            entry.CertificatePath = path;
            entry.CertificateFileName = Path.GetFileName(file.FileName);
        }
        entry.CertificateNo = no;
        entry.CertificateReceivedOn = request.ReceivedOn;
        await db.SaveChangesAsync(ct);
        return (await GetEntriesAsync((byte)entry.Direction, null, null, null, null, ct)).First(e => e.TdsEntryId == entryId);
    }

    public async Task<AttachmentFileDto> GetCertificateAsync(long entryId, CancellationToken ct = default)
    {
        TdsEntry entry = await db.Set<TdsEntry>().AsNoTracking().FirstOrDefaultAsync(t => t.TdsEntryId == entryId, ct) ?? throw new NotFoundException("TDS entry", entryId);
        if (entry.CertificatePath == null || !File.Exists(entry.CertificatePath)) throw new NotFoundException("Certificate file", entryId);
        string type = entry.CertificatePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) ? "application/pdf"
            : entry.CertificatePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
        return new AttachmentFileDto(await File.ReadAllBytesAsync(entry.CertificatePath, ct), type, entry.CertificateFileName ?? Path.GetFileName(entry.CertificatePath));
    }

    public async Task<IReadOnlyList<TdsSectionRowDto>> GetSectionsAsync(CancellationToken ct = default)
    {
        return await db.Set<TdsSection>().AsNoTracking().OrderBy(s => s.Section).ThenByDescending(s => s.EffectiveFrom)
            .Select(s => new TdsSectionRowDto(s.TdsSectionId, s.Section, s.Description, s.Rate, s.IndividualRate, s.NoPanRate, s.EffectiveFrom, s.EffectiveTo, s.IsActive))
            .ToListAsync(ct);
    }

    /// <summary>A new rate is a new row from its effective date; the row it replaces is closed
    /// the day before, so earlier deductions keep the rate they were made at.</summary>
    public async Task<TdsSectionRowDto> SaveRateAsync(SaveTdsRateRequest request, CancellationToken ct = default)
    {
        string section = request.Section?.Trim().ToUpperInvariant() ?? "";
        if (section.Length == 0 || section.Length > 10) throw new BusinessRuleException("Enter the section, for example 194C.");
        if (request.Rate < 0m || request.Rate > 100m || request.NoPanRate < 0m || request.NoPanRate > 100m) throw new BusinessRuleException("Rates are percentages between 0 and 100.");
        if (string.IsNullOrWhiteSpace(request.Description)) throw new BusinessRuleException("Say what the section applies to.");
        int id = await FinanceRules.InTransactionAsync(db, async () =>
        {
            var current = await db.Set<TdsSection>().Where(s => s.Section == section && (s.EffectiveTo == null || s.EffectiveTo >= request.EffectiveFrom)).ToListAsync(ct);
            if (current.Any(s => s.EffectiveFrom >= request.EffectiveFrom))
                throw new BusinessRuleException($"{section} already has a rate from {current.Max(s => s.EffectiveFrom):dd MMM yyyy}. A new rate must start after it.");
            foreach (TdsSection s in current) s.EffectiveTo = request.EffectiveFrom.AddDays(-1);
            var row = new TdsSection
            {
                Section = section, Description = request.Description.Trim(), Rate = request.Rate, IndividualRate = request.IndividualRate,
                NoPanRate = request.NoPanRate, EffectiveFrom = request.EffectiveFrom, IsActive = true
            };
            db.Set<TdsSection>().Add(row);
            await db.SaveChangesAsync(ct);
            return row.TdsSectionId;
        }, ct);
        return (await GetSectionsAsync(ct)).First(s => s.TdsSectionId == id);
    }

    public async Task<byte[]> ExportAsync(byte direction, string? financialYear, byte? quarter, CancellationToken ct = default)
    {
        var rows = await GetEntriesAsync(direction, financialYear, quarter, null, null, ct);
        bool payable = direction == (byte)TdsDirection.Payable;
        using var wb = new XLWorkbook();
        IXLWorksheet ws = wb.AddWorksheet(payable ? "TDS payable (26Q)" : "TDS receivable");
        string[] head = payable
            ? new[] { "Date paid", "Supplier", "PAN", "Section", "Base amount", "Rate %", "TDS", "Quarter", "FY", "Bill", "Challan", "Certificate issued" }
            : new[] { "Date", "Customer", "TAN", "Section", "Base amount", "Rate %", "TDS", "Quarter", "FY", "Invoice", "Certificate", "Received on" };
        for (int c = 0; c < head.Length; c++) ws.Cell(1, c + 1).Value = head[c];
        int r = 2;
        foreach (TdsEntryDto e in rows)
        {
            ws.Cell(r, 1).Value = e.DeductedOn.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 2).Value = e.PartyName;
            ws.Cell(r, 3).Value = e.PartyTaxId ?? "";
            ws.Cell(r, 4).Value = e.Section;
            ws.Cell(r, 5).Value = e.BaseAmount;
            ws.Cell(r, 6).Value = e.Rate;
            ws.Cell(r, 7).Value = e.TdsAmount;
            ws.Cell(r, 8).Value = $"Q{e.Quarter}";
            ws.Cell(r, 9).Value = e.FinancialYear;
            ws.Cell(r, 10).Value = e.DocumentNumber ?? "";
            ws.Cell(r, 11).Value = payable ? e.ChallanNo ?? "Not paid" : e.CertificateNo ?? "Missing";
            ws.Cell(r, 12).Value = payable ? e.CertificateNo ?? "" : e.CertificateReceivedOn?.ToString("dd-MMM-yyyy") ?? "";
            r++;
        }
        ExcelStyle.Finish(ws, head.Length, r - 1, new[] { 1 }, new[] { 5, 7 });
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
