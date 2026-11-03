using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// The compliance calendar, the period lock and the reminder log (build doc §13). Dates come
/// from MST_ComplianceObligation rows, not code. Amounts are pulled live from TDS and GST.
/// Frequency: MonthlyNext (for month M, due on DueDay of M+1), Monthly (due in M itself),
/// Quarterly (DueMonths lists the due month of Q1..Q4), Yearly (DueMonths = the month).
/// </summary>
public class ComplianceService(
    RegalisDbContext db,
    IGstService gst,
    ISettingService settings,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IComplianceService
{
    private static readonly string[] StatusLabels = { "", "Not started", "Not done", "Ready to file", "Filed" };

    private sealed record Occurrence(ComplianceObligation Obligation, string Period, string PeriodLabel, DateOnly Due, DateOnly PeriodFrom, DateOnly PeriodTo);

    private static DateOnly Clamp(int y, int m, int day) => new(y, m, Math.Min(day, DateTime.DaysInMonth(y, m)));

    private static IEnumerable<Occurrence> Occurrences(ComplianceObligation o, DateOnly from, DateOnly to)
    {
        for (DateOnly m = new DateOnly(from.Year, from.Month, 1).AddMonths(-15); m <= to; m = m.AddMonths(1))
        {
            switch (o.Frequency)
            {
                case "MonthlyNext":
                {
                    DateOnly next = m.AddMonths(1);
                    yield return new Occurrence(o, FinanceRules.Period(m), m.ToString("MMM yyyy", CultureInfo.InvariantCulture), Clamp(next.Year, next.Month, o.DueDay), m, m.AddMonths(1).AddDays(-1));
                    break;
                }
                case "Monthly":
                    yield return new Occurrence(o, FinanceRules.Period(m), m.ToString("MMM yyyy", CultureInfo.InvariantCulture), Clamp(m.Year, m.Month, o.DueDay), m, m.AddMonths(1).AddDays(-1));
                    break;
                case "Quarterly":
                {
                    // Quarter q of the FY starting in April: Apr-Jun, Jul-Sep, Oct-Dec, Jan-Mar.
                    if (m.Month is not (4 or 7 or 10 or 1)) break;
                    var months = (o.DueMonths ?? "7,10,1,5").Split(',').Select(s => int.Parse(s.Trim(), CultureInfo.InvariantCulture)).ToArray();
                    byte q = FinanceRules.Quarter(m);
                    string fy = FinanceRules.FinancialYear(m);
                    int dueMonth = months[q - 1];
                    DateOnly qEnd = m.AddMonths(3).AddDays(-1);
                    int dueYear = dueMonth > qEnd.Month ? qEnd.Year : qEnd.Year + 1;
                    yield return new Occurrence(o, $"{fy[..4]}-Q{q}", $"Q{q} {fy}", Clamp(dueYear, dueMonth, o.DueDay), m, qEnd);
                    break;
                }
                case "Yearly":
                    if (m.Month != 4) break;
                    int month = int.TryParse(o.DueMonths, out int dm) ? dm : 7;
                    int year = m.Year + 1;
                    yield return new Occurrence(o, m.Year.ToString(CultureInfo.InvariantCulture), $"FY {FinanceRules.FinancialYear(m)}", Clamp(year, month, o.DueDay), m, m.AddYears(1).AddDays(-1));
                    break;
            }
        }
    }

    public async Task<IReadOnlyList<ComplianceItemDto>> GetCalendarAsync(string? fromPeriod, int months, CancellationToken ct = default)
    {
        DateOnly today = FinanceRules.Today(clock);
        DateOnly start = DateOnly.TryParseExact((fromPeriod ?? "") + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateOnly f)
            ? f : new DateOnly(today.Year, today.Month, 1);
        months = Math.Clamp(months, 1, 12);
        DateOnly end = start.AddMonths(months).AddDays(-1);
        DateOnly lookBack = start.AddDays(-90);

        var obligations = await db.Set<ComplianceObligation>().AsNoTracking().Where(o => o.IsActive).OrderBy(o => o.DisplayOrder).ToListAsync(ct);
        var occurrences = obligations.SelectMany(o => Occurrences(o, start, end)).Where(x => x.Due >= lookBack && x.Due <= end).ToList();
        var periods = occurrences.Select(x => x.Period).Distinct().ToList();
        var statuses = await (from s in db.Set<ComplianceStatus>().AsNoTracking()
                              where periods.Contains(s.Period)
                              join u in db.Users on s.CompletedByUserId equals (long?)u.UserId into uj
                              from u in uj.DefaultIfEmpty()
                              select new { s, By = u != null ? u.FullName : null }).ToListAsync(ct);
        var returns = await db.Set<GstReturn>().AsNoTracking().Where(r => periods.Contains(r.Period)).ToListAsync(ct);
        var unpaidTds = await db.Set<TdsEntry>().AsNoTracking().Where(t => t.Direction == TdsDirection.Payable)
            .Select(t => new { t.DeductedOn, t.TdsAmount, t.TdsChallanId }).ToListAsync(ct);

        var items = new List<ComplianceItemDto>();
        foreach (Occurrence x in occurrences)
        {
            var saved = statuses.FirstOrDefault(s => s.s.ComplianceObligationId == x.Obligation.ComplianceObligationId && s.s.Period == x.Period);
            byte status = saved != null ? (byte)saved.s.Status : (byte)ComplianceState.NotStarted;
            decimal? amount = null;
            string? note = null, link = null;
            string? reference = saved?.s.Reference;
            string src = x.Obligation.AmountSource ?? "";
            if (src == "TdsUnpaid")
            {
                var month = unpaidTds.Where(t => t.DeductedOn >= x.PeriodFrom && t.DeductedOn <= x.PeriodTo).ToList();
                amount = month.Where(t => t.TdsChallanId == null).Sum(t => t.TdsAmount);
                note = "TDS payable with no challan";
                link = "/admin/billing/tds";
                if (saved == null && month.Count > 0 && amount == 0m) status = (byte)ComplianceState.Filed;
                if (saved == null && month.Count == 0)
                {
                    // Nothing was deducted that month, so there is no challan to pay.
                    status = (byte)ComplianceState.Filed;
                    note = "No TDS deducted this month";
                }
            }
            else if (src == "GstCash")
            {
                amount = (await gst.GetSummaryAsync(x.Period, ct)).CashToPay;
                note = "GST cash to pay";
                link = "/admin/billing/gst";
            }
            else if (src.StartsWith("Setting:", StringComparison.Ordinal))
            {
                amount = await settings.GetDecimalAsync(src[8..], 0m, ct);
                note = "Fixed amount from settings";
            }
            if (x.Obligation.Code is "GSTR1" or "GSTR3B")
            {
                link = "/admin/billing/gst";
                GstReturn? r = returns.FirstOrDefault(r => r.Period == x.Period && r.ReturnType == x.Obligation.Code);
                if (r != null && saved == null)
                {
                    status = r.Status switch { GstReturnStatus.Filed => (byte)ComplianceState.Filed, GstReturnStatus.Ready => (byte)ComplianceState.ReadyToFile, _ => status };
                    reference = r.Arn;
                }
            }
            if (x.Obligation.Code == "TDS_26Q") link = "/admin/billing/tds";
            if (status != (byte)ComplianceState.Filed && x.Due < today) status = (byte)ComplianceState.NotDone;
            // An occurrence from before the window is shown only while it is still undone.
            if (x.Due < start && status == (byte)ComplianceState.Filed) continue;
            items.Add(new ComplianceItemDto($"o{x.Obligation.ComplianceObligationId}-{x.Period}", x.Obligation.ComplianceObligationId, x.Obligation.Name, x.Period,
                x.PeriodLabel, x.Due, x.Due.DayNumber - today.DayNumber, amount, note, status, StatusLabels[status], x.Obligation.OwnerRole,
                saved?.By, saved?.s.CompletedOn, reference, link, true));
        }

        // Purchase bills falling due in the window.
        var bills = await (from b in db.Set<PurchaseBill>().AsNoTracking()
                           where (b.Status == PurchaseBillStatus.Approved || b.Status == PurchaseBillStatus.PartPaid) && b.DueDate >= lookBack && b.DueDate <= end
                           join s in db.Suppliers on b.SupplierId equals s.SupplierId
                           select new
                           {
                               b.PurchaseBillId, b.SupplierBillNo, s.SupplierName, b.DueDate, b.BillTotal, b.BillDate,
                               Paid = PurchaseBillService.LiveAllocations(db).Where(a => a.PurchaseBillId == b.PurchaseBillId).Sum(a => (decimal?)a.Amount) ?? 0m
                           }).ToListAsync(ct);
        foreach (var b in bills.Where(b => b.BillTotal - b.Paid > 0m))
        {
            byte status = b.DueDate < today ? (byte)ComplianceState.NotDone : (byte)ComplianceState.NotStarted;
            items.Add(new ComplianceItemDto($"bill-{b.PurchaseBillId}", null, $"Pay bill {b.SupplierBillNo} · {b.SupplierName}", FinanceRules.Period(b.DueDate),
                b.BillDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture), b.DueDate, b.DueDate.DayNumber - today.DayNumber, b.BillTotal - b.Paid, "Balance on the bill",
                status, StatusLabels[status], "Finance", null, null, null, $"/admin/billing/purchase-bills/{b.PurchaseBillId}", false));
        }
        return items.OrderBy(i => i.DueDate).ThenBy(i => i.Name).ToList();
    }

    public async Task<ComplianceItemDto> SetStatusAsync(ComplianceStatusRequest request, CancellationToken ct = default)
    {
        if (request.Status is < 1 or > 4 || request.Status == (byte)ComplianceState.NotDone) throw new BusinessRuleException("Choose Not started, Ready to file or Filed.");
        ComplianceObligation o = await db.Set<ComplianceObligation>().FirstOrDefaultAsync(x => x.ComplianceObligationId == request.ObligationId, ct)
            ?? throw new NotFoundException("Obligation", request.ObligationId);
        if (string.IsNullOrWhiteSpace(request.Period) || request.Period.Length > 7) throw new BusinessRuleException("The period is missing.");
        if (request.Status == (byte)ComplianceState.Filed && string.IsNullOrWhiteSpace(request.Reference))
            throw new BusinessRuleException("Enter the reference - the ARN, challan or acknowledgement number.");
        ComplianceStatus? row = await db.Set<ComplianceStatus>().FirstOrDefaultAsync(s => s.ComplianceObligationId == o.ComplianceObligationId && s.Period == request.Period, ct);
        if (row == null)
        {
            row = new ComplianceStatus { ComplianceObligationId = o.ComplianceObligationId, Period = request.Period };
            db.Set<ComplianceStatus>().Add(row);
        }
        row.Status = (ComplianceState)request.Status;
        row.Reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        row.CompletedOn = request.Status == (byte)ComplianceState.Filed ? request.CompletedOn ?? FinanceRules.Today(clock) : null;
        row.CompletedByUserId = request.Status == (byte)ComplianceState.Filed ? currentUser.UserId : null;
        await db.SaveChangesAsync(ct);
        DateOnly today = FinanceRules.Today(clock);
        var all = await GetCalendarAsync(FinanceRules.Period(today.AddMonths(-14)), 12, ct);
        return all.FirstOrDefault(i => i.ObligationId == o.ComplianceObligationId && i.Period == request.Period)
               ?? new ComplianceItemDto($"o{o.ComplianceObligationId}-{request.Period}", o.ComplianceObligationId, o.Name, request.Period, request.Period, today, 0,
                   null, null, request.Status, StatusLabels[request.Status], o.OwnerRole, currentUser.FullName, row.CompletedOn, row.Reference, null, true);
    }

    public async Task<IReadOnlyList<ComplianceObligationDto>> GetObligationsAsync(CancellationToken ct = default)
    {
        return await db.Set<ComplianceObligation>().AsNoTracking().OrderBy(o => o.DisplayOrder)
            .Select(o => new ComplianceObligationDto(o.ComplianceObligationId, o.Code, o.Name, o.Frequency, o.DueDay, o.DueMonths, o.AmountSource, o.OwnerRole, o.DisplayOrder, o.IsActive))
            .ToListAsync(ct);
    }

    public async Task<ComplianceObligationDto> SaveObligationAsync(int? id, SaveComplianceObligationRequest request, CancellationToken ct = default)
    {
        string code = request.Code?.Trim().ToUpperInvariant() ?? "";
        if (code.Length == 0 || string.IsNullOrWhiteSpace(request.Name)) throw new BusinessRuleException("Enter a code and a name.");
        if (request.Frequency is not ("Monthly" or "MonthlyNext" or "Quarterly" or "Yearly")) throw new BusinessRuleException("Frequency is Monthly, MonthlyNext, Quarterly or Yearly.");
        if (request.DueDay is < 1 or > 31) throw new BusinessRuleException("The due day is between 1 and 31.");
        if (await db.Set<ComplianceObligation>().AnyAsync(o => o.Code == code && o.ComplianceObligationId != (id ?? 0), ct))
            throw new ConflictException($"An obligation with code {code} already exists.", "DUPLICATE_OBLIGATION");
        ComplianceObligation o;
        if (id == null)
        {
            o = new ComplianceObligation();
            db.Set<ComplianceObligation>().Add(o);
        }
        else
        {
            o = await db.Set<ComplianceObligation>().FirstOrDefaultAsync(x => x.ComplianceObligationId == id, ct) ?? throw new NotFoundException("Obligation", id.Value);
        }
        o.Code = code;
        o.Name = request.Name.Trim();
        o.Frequency = request.Frequency;
        o.DueDay = request.DueDay;
        o.DueMonths = string.IsNullOrWhiteSpace(request.DueMonths) ? null : request.DueMonths.Trim();
        o.AmountSource = string.IsNullOrWhiteSpace(request.AmountSource) ? null : request.AmountSource.Trim();
        o.OwnerRole = string.IsNullOrWhiteSpace(request.OwnerRole) ? null : request.OwnerRole.Trim();
        o.DisplayOrder = request.DisplayOrder;
        o.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);
        return new ComplianceObligationDto(o.ComplianceObligationId, o.Code, o.Name, o.Frequency, o.DueDay, o.DueMonths, o.AmountSource, o.OwnerRole, o.DisplayOrder, o.IsActive);
    }

    // ── Period lock ──────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<PeriodLockDto>> GetPeriodLocksAsync(CancellationToken ct = default)
    {
        DateOnly today = FinanceRules.Today(clock);
        DateOnly thisMonth = new(today.Year, today.Month, 1);
        var rows = await (from p in db.Set<PeriodLock>().AsNoTracking()
                          join l in db.Users on p.LockedByUserId equals (long?)l.UserId into lj
                          from l in lj.DefaultIfEmpty()
                          join u in db.Users on p.UnlockedByUserId equals (long?)u.UserId into uj
                          from u in uj.DefaultIfEmpty()
                          select new { p, LockedBy = l != null ? l.FullName : null, UnlockedBy = u != null ? u.FullName : null }).ToListAsync(ct);
        var periods = Enumerable.Range(0, 18).Select(i => FinanceRules.Period(thisMonth.AddMonths(-i))).Union(rows.Select(r => r.p.Period)).Distinct()
            .OrderByDescending(p => p).ToList();
        return periods.Select(p =>
        {
            var r = rows.FirstOrDefault(x => x.p.Period == p);
            bool ended = string.CompareOrdinal(p, FinanceRules.Period(thisMonth)) < 0;
            return new PeriodLockDto(p, FinanceRules.MonthName(p), r?.p.IsLocked ?? false, r?.LockedBy, r?.p.LockedUtc, r?.UnlockedBy, r?.p.UnlockedUtc,
                r?.p.UnlockReason, ended && !(r?.p.IsLocked ?? false));
        }).ToList();
    }

    private static void CheckPeriod(string period)
    {
        if (!DateOnly.TryParseExact(period + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            throw new BusinessRuleException("Choose a month.");
    }

    public async Task<PeriodLockDto> LockAsync(string period, CancellationToken ct = default)
    {
        CheckPeriod(period);
        DateOnly today = FinanceRules.Today(clock);
        if (string.CompareOrdinal(period, FinanceRules.Period(today)) >= 0) throw new BusinessRuleException("Only a month that has ended can be locked.");
        PeriodLock? row = await db.Set<PeriodLock>().FirstOrDefaultAsync(p => p.Period == period, ct);
        if (row?.IsLocked == true) throw new BusinessRuleException($"{FinanceRules.MonthName(period)} is already locked.");
        if (row == null)
        {
            row = new PeriodLock { Period = period };
            db.Set<PeriodLock>().Add(row);
        }
        row.IsLocked = true;
        row.LockedByUserId = currentUser.UserId;
        row.LockedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return (await GetPeriodLocksAsync(ct)).First(p => p.Period == period);
    }

    public async Task<PeriodLockDto> UnlockAsync(string period, UnlockPeriodRequest request, CancellationToken ct = default)
    {
        CheckPeriod(period);
        string reason = request.Reason?.Trim() ?? "";
        if (reason.Length < 5) throw new BusinessRuleException("Say why this month is being unlocked. It is written to the audit log.");
        PeriodLock row = await db.Set<PeriodLock>().FirstOrDefaultAsync(p => p.Period == period && p.IsLocked, ct)
            ?? throw new BusinessRuleException($"{FinanceRules.MonthName(period)} is not locked.");
        row.IsLocked = false;
        row.UnlockedByUserId = currentUser.UserId;
        row.UnlockedUtc = clock.UtcNow;
        row.UnlockReason = reason.Length > 300 ? reason[..300] : reason;
        await db.SaveChangesAsync(ct);
        return (await GetPeriodLocksAsync(ct)).First(p => p.Period == period);
    }

    public async Task<IReadOnlyList<ReminderLogDto>> GetReminderLogAsync(string? documentType, long? documentId, CancellationToken ct = default)
    {
        var q = db.Set<ReminderLog>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(documentType)) q = q.Where(r => r.DocumentType == documentType);
        if (documentId != null) q = q.Where(r => r.DocumentId == documentId);
        return await q.OrderByDescending(r => r.ReminderLogId).Take(200)
            .Select(r => new ReminderLogDto(r.ReminderLogId, r.ReminderType, r.DocumentType, r.DocumentId, r.DocumentNumber, r.Recipient, r.Channel, r.SentUtc, r.Delivered, r.Error))
            .ToListAsync(ct);
    }
}
