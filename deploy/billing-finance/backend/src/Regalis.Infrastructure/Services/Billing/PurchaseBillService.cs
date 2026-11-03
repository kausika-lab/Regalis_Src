using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// Purchase bills (build doc §4-5): the supplier's own invoice, recorded when it arrives,
/// paid or not. Duplicate check on supplier + bill number (also a filtered unique index).
/// GST split by place of supply, and the screen is told why. Paid and balance always come
/// from vendor-payment allocations. A bill above the approval limit cannot be paid until
/// someone other than the person who entered it approves it (BILL.APPROVE).
/// </summary>
public class PurchaseBillService(
    RegalisDbContext db,
    INumberSequenceService sequences,
    ISettingService settings,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IOptions<StorageOptions> storage) : IPurchaseBillService
{
    public const string BillSequence = "PurchaseBill";

    private static readonly PurchaseBillStatus[] Unpaid =
    {
        PurchaseBillStatus.AwaitingApproval, PurchaseBillStatus.Approved, PurchaseBillStatus.PartPaid, PurchaseBillStatus.OnHold
    };

    private static readonly PurchaseBillStatus[] Payable = { PurchaseBillStatus.Approved, PurchaseBillStatus.PartPaid };

    private static readonly PurchaseBillStatus[] OnBooks =
    {
        PurchaseBillStatus.Approved, PurchaseBillStatus.PartPaid, PurchaseBillStatus.Paid, PurchaseBillStatus.OnHold
    };

    /// <summary>Built with an object initializer, not a constructor, so EF can still sort and
    /// group on it after the projection.</summary>
    private sealed class Row
    {
        public PurchaseBill Bill { get; init; } = null!;
        public string SupplierName { get; init; } = "";
        public string? PoNumber { get; init; }
        public decimal Paid { get; init; }
        public int Attachments { get; init; }
    }

    /// <summary>Allocations of payments that were not reversed.</summary>
    internal static IQueryable<VendorPaymentAllocation> LiveAllocations(RegalisDbContext db) =>
        db.Set<VendorPaymentAllocation>().Where(a => db.Set<VendorPayment>().Any(v => v.VendorPaymentId == a.VendorPaymentId && !v.IsReversed));

    private IQueryable<Row> Rows(IQueryable<PurchaseBill> bills) =>
        from b in bills
        join s in db.Suppliers on b.SupplierId equals s.SupplierId
        join po in db.PurchaseOrders on b.PurchaseOrderId equals (long?)po.PurchaseOrderId into poJoin
        from po in poJoin.DefaultIfEmpty()
        select new Row
        {
            Bill = b,
            SupplierName = s.SupplierName,
            PoNumber = po != null ? po.PoNumber : null,
            Paid = LiveAllocations(db).Where(a => a.PurchaseBillId == b.PurchaseBillId).Sum(a => (decimal?)a.Amount) ?? 0m,
            Attachments = b.Attachments.Count
        };

    private IQueryable<PurchaseBill> Filter(PurchaseBillListRequest r, DateOnly today)
    {
        IQueryable<PurchaseBill> q = db.Set<PurchaseBill>().AsNoTracking();
        if (r.SupplierId != null) q = q.Where(b => b.SupplierId == r.SupplierId);
        if (r.Status != null) q = q.Where(b => (byte)b.Status == r.Status);
        if (r.PurchaseOrderId != null) q = q.Where(b => b.PurchaseOrderId == r.PurchaseOrderId);
        if (!string.IsNullOrWhiteSpace(r.FinancialYear) && r.FinancialYear.Length >= 4 && int.TryParse(r.FinancialYear[..4], out int y))
        {
            var from = new DateOnly(y, 4, 1);
            var to = new DateOnly(y + 1, 3, 31);
            q = q.Where(b => b.BillDate >= from && b.BillDate <= to);
        }
        switch (r.Quick)
        {
            case "unpaid":
                q = q.Where(b => Unpaid.Contains(b.Status));
                break;
            case "due-week":
            {
                DateOnly week = today.AddDays(7);
                q = q.Where(b => Payable.Contains(b.Status) && b.DueDate >= today && b.DueDate <= week);
                break;
            }
            case "overdue":
                q = q.Where(b => Payable.Contains(b.Status) && b.DueDate < today);
                break;
            case "awaiting":
                q = q.Where(b => b.Status == PurchaseBillStatus.AwaitingApproval);
                break;
        }
        if (!string.IsNullOrWhiteSpace(r.Search))
        {
            string s = r.Search.Trim();
            q = q.Where(b => b.SupplierBillNo.Contains(s) || b.BillNumber.Contains(s)
                             || db.Suppliers.Any(x => x.SupplierId == b.SupplierId && x.SupplierName.Contains(s))
                             || db.PurchaseOrders.Any(p => p.PurchaseOrderId == b.PurchaseOrderId && p.PoNumber.Contains(s)));
        }
        return q;
    }

    public async Task<PurchaseBillListResultDto> GetBillsAsync(PurchaseBillListRequest request, CancellationToken ct = default)
    {
        DateOnly today = FinanceRules.Today(clock);
        IQueryable<PurchaseBill> filtered = Filter(request, today);
        IQueryable<Row> rows = Rows(filtered);
        int total = await filtered.CountAsync(ct);
        // Totals over the whole filter: two plain sums (SQL Server cannot aggregate the per-row subquery).
        decimal filterTotal = await filtered.SumAsync(b => (decimal?)b.BillTotal, ct) ?? 0m;
        decimal filterPaid = await LiveAllocations(db).Where(a => filtered.Select(b => b.PurchaseBillId).Contains(a.PurchaseBillId))
            .SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;
        List<Row> page = await rows.OrderByDescending(x => x.Bill.BillDate).ThenByDescending(x => x.Bill.PurchaseBillId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        bool canCreate = currentUser.HasPermission("BILL.CREATE");
        bool canPay = currentUser.HasPermission("BILL.PAY");
        var items = page.Select(x =>
        {
            decimal balance = x.Bill.BillTotal - x.Paid;
            bool overdue = Payable.Contains(x.Bill.Status) && balance > 0m && x.Bill.DueDate < today;
            return new PurchaseBillListItemDto(x.Bill.PurchaseBillId, x.Bill.BillNumber, x.Bill.SupplierId, x.SupplierName,
                x.Bill.SupplierBillNo, x.Bill.BillDate, x.Bill.DueDate, x.Bill.PurchaseOrderId, x.PoNumber, x.Bill.BillTotal,
                x.Bill.TdsAmount, x.Paid, balance, (byte)x.Bill.Status, overdue, overdue ? today.DayNumber - x.Bill.DueDate.DayNumber : 0,
                x.Attachments, canCreate && IsEditable(x.Bill.Status), canPay && Payable.Contains(x.Bill.Status) && balance > 0m);
        }).ToList();
        return new PurchaseBillListResultDto(PagedResult<PurchaseBillListItemDto>.Create(items, request.Page, request.PageSize, total),
            filterTotal, filterPaid, filterTotal - filterPaid, total);
    }

    public async Task<PurchaseBillSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        DateOnly today = FinanceRules.Today(clock);
        DateOnly week = today.AddDays(7);
        var open = await Rows(db.Set<PurchaseBill>().AsNoTracking().Where(b => Unpaid.Contains(b.Status)))
            .Select(x => new { x.Bill.Status, x.Bill.DueDate, x.Bill.BillTotal, x.Paid }).ToListAsync(ct);
        var unpaid = open.Select(x => new { x.Status, x.DueDate, Balance = x.BillTotal - x.Paid }).Where(x => x.Balance > 0m).ToList();
        var dueWeek = unpaid.Where(x => Payable.Contains(x.Status) && x.DueDate >= today && x.DueDate <= week).ToList();
        var overdue = unpaid.Where(x => Payable.Contains(x.Status) && x.DueDate < today).ToList();
        var awaiting = unpaid.Where(x => x.Status == PurchaseBillStatus.AwaitingApproval).ToList();
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        decimal inputGst = await db.Set<PurchaseBill>().AsNoTracking()
            .Where(b => OnBooks.Contains(b.Status) && b.BillDate >= monthStart && b.BillDate <= today && b.SupplierGstin != null)
            .SumAsync(b => (decimal?)(b.CgstAmount + b.SgstAmount + b.IgstAmount), ct) ?? 0m;
        return new PurchaseBillSummaryDto(unpaid.Sum(x => x.Balance), unpaid.Count, dueWeek.Sum(x => x.Balance), dueWeek.Count,
            overdue.Sum(x => x.Balance), overdue.Count, overdue.Count == 0 ? 0 : overdue.Max(x => today.DayNumber - x.DueDate.DayNumber),
            awaiting.Count, awaiting.Sum(x => x.Balance), inputGst);
    }

    private static bool IsEditable(PurchaseBillStatus s) =>
        s is PurchaseBillStatus.Draft or PurchaseBillStatus.AwaitingApproval or PurchaseBillStatus.Rejected;

    public async Task<PurchaseBillDto> GetBillAsync(long id, CancellationToken ct = default)
    {
        PurchaseBill bill = await db.Set<PurchaseBill>().AsNoTracking()
            .Include(b => b.Lines).Include(b => b.Attachments).Include(b => b.Events)
            .FirstOrDefaultAsync(b => b.PurchaseBillId == id, ct) ?? throw new NotFoundException("Purchase bill", id);
        Supplier supplier = await db.Suppliers.IgnoreQueryFilters().AsNoTracking().FirstAsync(s => s.SupplierId == bill.SupplierId, ct);
        SupplierTaxProfile? profile = await db.Set<SupplierTaxProfile>().AsNoTracking().FirstOrDefaultAsync(p => p.SupplierId == bill.SupplierId, ct);
        string? poNumber = bill.PurchaseOrderId == null ? null
            : await db.PurchaseOrders.IgnoreQueryFilters().Where(p => p.PurchaseOrderId == bill.PurchaseOrderId).Select(p => p.PoNumber).FirstOrDefaultAsync(ct);
        string? locationName = await db.Locations.IgnoreQueryFilters().Where(l => l.LocationId == bill.LocationId).Select(l => l.LocationName).FirstOrDefaultAsync(ct);

        var payments = await (from a in db.Set<VendorPaymentAllocation>().AsNoTracking()
                              join v in db.Set<VendorPayment>() on a.VendorPaymentId equals v.VendorPaymentId
                              where a.PurchaseBillId == id
                              orderby v.PaidOn, v.VendorPaymentId
                              select new { v, a.Amount, a.TdsAmount }).ToListAsync(ct);
        var reconciledIds = await ReconciledVendorPaymentIdsAsync(db, payments.Select(p => p.v.VendorPaymentId).ToList(), ct);
        var paymentRows = payments.Select(p => new BillPaymentRowDto(p.v.VendorPaymentId, p.v.PaymentNumber, p.v.PaidOn,
            VendorPaymentService.ModeName(p.v.Mode), p.v.Reference, p.Amount - p.TdsAmount, p.TdsAmount,
            reconciledIds.Contains(p.v.VendorPaymentId), p.v.IsReversed)).ToList();
        var live = payments.Where(p => !p.v.IsReversed).ToList();
        decimal paid = live.Sum(p => p.Amount);
        decimal tdsDeducted = live.Sum(p => p.TdsAmount);

        var userIds = bill.Events.Where(e => e.UserId != null).Select(e => e.UserId!.Value)
            .Append(bill.EnteredByUserId).Concat(bill.ApprovedByUserId != null ? new[] { bill.ApprovedByUserId.Value } : Array.Empty<long>())
            .Concat(bill.Attachments.Select(a => a.UploadedByUserId)).Distinct().ToList();
        var names = await db.Users.IgnoreQueryFilters().Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);

        DateOnly today = FinanceRules.Today(clock);
        decimal balance = bill.BillTotal - paid;
        bool overdue = Payable.Contains(bill.Status) && balance > 0m && bill.DueDate < today;
        decimal limit = await settings.GetDecimalAsync("Bill.ApprovalLimit", 25000m, ct);
        string? posName = GstStates.All.FirstOrDefault(s => s.Code == bill.PlaceOfSupplyCode)?.Name;
        string reason = bill.IsInterState
            ? $"GST is IGST because the supplier's state ({StateName(bill.SupplierStateCode)}) differs from the location's ({StateName(bill.PlaceOfSupplyCode)})."
            : bill.SupplierStateCode == null
                ? "GST is CGST+SGST: the supplier's state is not known, so the location's state is assumed. Add the supplier's state or GSTIN to be sure."
                : $"GST is CGST+SGST because the supplier's state matches the location's ({StateName(bill.PlaceOfSupplyCode)}). A supplier in another state gives IGST.";

        bool mine = bill.EnteredByUserId == currentUser.UserId;
        return new PurchaseBillDto(bill.PurchaseBillId, bill.BillNumber, bill.SupplierId, supplier.SupplierName, bill.SupplierGstin ?? supplier.Gstin,
            profile?.Pan, bill.SupplierBillNo, bill.BillDate, bill.DueDate, bill.PaymentTermsDays, bill.PurchaseOrderId, poNumber,
            bill.LocationId, locationName, bill.SupplierStateCode, bill.PlaceOfSupplyCode, posName, bill.IsInterState, reason,
            bill.TaxableValue, bill.CgstAmount, bill.SgstAmount, bill.IgstAmount, bill.BillTotal, bill.TdsSection, bill.TdsRate, bill.TdsAmount,
            bill.BillTotal - bill.TdsAmount, paid, tdsDeducted, balance,
            Math.Max(0m, bill.BillTotal - Math.Max(bill.TdsAmount, tdsDeducted) - (paid - tdsDeducted)),
            (byte)bill.Status, overdue, overdue ? today.DayNumber - bill.DueDate.DayNumber : 0,
            names.GetValueOrDefault(bill.EnteredByUserId), bill.EnteredByUserId, bill.CreatedDate, bill.SubmittedUtc,
            bill.ApprovedByUserId != null ? names.GetValueOrDefault(bill.ApprovedByUserId.Value) : null, bill.ApprovedUtc,
            bill.ApprovalComment, bill.RejectReason, bill.HoldReason, bill.CancelReason, bill.Notes, limit,
            bill.Lines.OrderBy(l => l.LineNumber).Select(l => new PurchaseBillLineDto(l.LineNumber, l.Description, l.HsnSac, l.Quantity,
                l.Rate, l.TaxableValue, l.GstRate, l.CgstAmount, l.SgstAmount, l.IgstAmount)).ToList(),
            paymentRows,
            bill.Attachments.OrderBy(a => a.UploadedUtc).Select(a => new PurchaseBillAttachmentDto(a.PurchaseBillAttachmentId, a.FileName,
                a.ContentType, a.SizeBytes, names.GetValueOrDefault(a.UploadedByUserId), a.UploadedUtc)).ToList(),
            bill.Events.OrderBy(e => e.EventUtc).ThenBy(e => e.PurchaseBillEventId).Select(e => new PurchaseBillEventDto(e.EventType, e.Comment,
                e.UserId != null ? names.GetValueOrDefault(e.UserId.Value) : null, e.EventUtc)).ToList(),
            CanEdit: currentUser.HasPermission("BILL.CREATE") && IsEditable(bill.Status),
            CanSubmit: currentUser.HasPermission("BILL.CREATE") && bill.Status is PurchaseBillStatus.Draft or PurchaseBillStatus.Rejected,
            CanApprove: currentUser.HasPermission("BILL.APPROVE") && bill.Status == PurchaseBillStatus.AwaitingApproval && !mine,
            CanPay: currentUser.HasPermission("BILL.PAY") && Payable.Contains(bill.Status) && balance > 0m,
            CanHold: currentUser.HasPermission("BILL.APPROVE") && bill.Status is PurchaseBillStatus.Approved or PurchaseBillStatus.PartPaid or PurchaseBillStatus.OnHold,
            CanCancel: currentUser.HasPermission("BILL.CREATE") && paid == 0m && bill.Status is not (PurchaseBillStatus.Cancelled or PurchaseBillStatus.Paid));
    }

    private static string StateName(string? code) => GstStates.All.FirstOrDefault(s => s.Code == code)?.Name ?? (code ?? "unknown");

    internal static async Task<HashSet<long>> ReconciledVendorPaymentIdsAsync(RegalisDbContext db, List<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return new HashSet<long>();
        var matched = await db.Set<BankMatchAllocation>().AsNoTracking()
            .Where(m => m.DocType == BankMatchDocType.VendorPayment && ids.Contains(m.DocId)).Select(m => m.DocId).ToListAsync(ct);
        return matched.ToHashSet();
    }

    public async Task<PurchaseBillFormOptionsDto> GetFormOptionsAsync(CancellationToken ct = default)
    {
        var suppliers = await (from s in db.Suppliers.AsNoTracking()
                               join p in db.Set<SupplierTaxProfile>() on s.SupplierId equals p.SupplierId into pj
                               from p in pj.DefaultIfEmpty()
                               orderby s.SupplierName
                               select new SupplierOptionDto(s.SupplierId, s.SupplierName, s.SupplierCode, s.Gstin, p != null ? p.Pan : null,
                                   p != null && p.StateCode != null ? p.StateCode : null, p != null ? p.DefaultTdsSection : null,
                                   p != null ? p.PaymentTermsDays : null, p != null && p.IsIndividual, s.IsActive)).ToListAsync(ct);
        var withState = suppliers.Select(s => s with
        {
            StateCode = s.StateCode ?? FinanceRules.GstinStateCode(s.Gstin)
        }).ToList();
        var locations = (await db.Locations.AsNoTracking().OrderBy(l => l.LocationName)
                .Select(l => new { l.LocationId, l.LocationName, l.State }).ToListAsync(ct))
            .Select(l => new LocationOptionDto(l.LocationId, l.LocationName, FinanceRules.StateCodeOf(l.State))).ToList();
        DateOnly today = FinanceRules.Today(clock);
        return new PurchaseBillFormOptionsDto(withState, locations, await TdsSectionsAsync(db, today, ct), await GstRatesAsync(db, today, ct),
            await settings.GetIntAsync("Bill.DefaultPaymentTermsDays", 15, ct), await settings.GetStringAsync("Quotation.HomeStateCode", "29", ct),
            await settings.GetDecimalAsync("Bill.ApprovalLimit", 25000m, ct), await sequences.PeekAsync(BillSequence, ct));
    }

    internal static async Task<IReadOnlyList<TdsSectionOptionDto>> TdsSectionsAsync(RegalisDbContext db, DateOnly on, CancellationToken ct)
    {
        return await db.Set<TdsSection>().AsNoTracking()
            .Where(s => s.IsActive && s.EffectiveFrom <= on && (s.EffectiveTo == null || s.EffectiveTo >= on))
            .OrderBy(s => s.Section)
            .Select(s => new TdsSectionOptionDto(s.Section, s.Description, s.Rate, s.IndividualRate, s.NoPanRate)).ToListAsync(ct);
    }

    internal static async Task<IReadOnlyList<decimal>> GstRatesAsync(RegalisDbContext db, DateOnly on, CancellationToken ct)
    {
        var rates = await db.Set<GstRate>().AsNoTracking()
            .Where(r => r.IsActive && r.EffectiveFrom <= on && (r.EffectiveTo == null || r.EffectiveTo >= on))
            .OrderByDescending(r => r.Rate).Select(r => r.Rate).ToListAsync(ct);
        return rates.Count > 0 ? rates : new List<decimal> { 18m, 12m, 5m, 0m };
    }

    /// <summary>The rate in force on a date: individual/HUF rate where it differs; 20% without a PAN.</summary>
    internal static async Task<decimal> TdsRateAsync(RegalisDbContext db, string section, DateOnly on, bool individual, bool hasPan, CancellationToken ct)
    {
        TdsSection? s = await db.Set<TdsSection>().AsNoTracking()
            .Where(x => x.Section == section && x.IsActive && x.EffectiveFrom <= on && (x.EffectiveTo == null || x.EffectiveTo >= on))
            .OrderByDescending(x => x.EffectiveFrom).FirstOrDefaultAsync(ct)
            ?? throw new BusinessRuleException($"TDS section {section} has no rate in force on {on:dd MMM yyyy}.");
        if (!hasPan) return s.NoPanRate;
        return individual && s.IndividualRate != null ? s.IndividualRate.Value : s.Rate;
    }

    public async Task<PoBillPrefillDto> GetPoPrefillAsync(long purchaseOrderId, CancellationToken ct = default)
    {
        PurchaseOrder po = await db.PurchaseOrders.AsNoTracking().Include(p => p.Lines)
            .FirstOrDefaultAsync(p => p.PurchaseOrderId == purchaseOrderId, ct) ?? throw new NotFoundException("Purchase order", purchaseOrderId);
        if (po.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.Cancelled or PurchaseOrderStatus.PendingApproval)
        {
            throw new BusinessRuleException($"{po.PoNumber} is not approved yet, so a bill cannot be raised against it.");
        }
        var existing = await db.Set<PurchaseBill>().AsNoTracking()
            .Where(b => b.PurchaseOrderId == purchaseOrderId && b.Status != PurchaseBillStatus.Cancelled)
            .Select(b => b.SupplierBillNo).ToListAsync(ct);
        string? section = po.TdsCode == null ? null : new string(po.TdsCode.TakeWhile(char.IsLetterOrDigit).ToArray());
        if (section != null && section.Length > 4) section = section[..4];
        return new PoBillPrefillDto(po.PurchaseOrderId, po.PoNumber, po.SupplierId, po.LocationId, section,
            po.Lines.OrderBy(l => l.LineNumber).Select(l => new SavePurchaseBillLineRequest(l.ItemName, l.HsnSacCode,
                l.ReceivedQuantity > 0 ? l.ReceivedQuantity : l.Quantity, l.Rate, l.TaxPercent)).ToList(), existing);
    }

    public Task<PurchaseBillDto> CreateAsync(SavePurchaseBillRequest request, CancellationToken ct = default) => SaveAsync(null, request, ct);

    public Task<PurchaseBillDto> UpdateAsync(long id, SavePurchaseBillRequest request, CancellationToken ct = default) => SaveAsync(id, request, ct);

    private async Task<PurchaseBillDto> SaveAsync(long? id, SavePurchaseBillRequest request, CancellationToken ct)
    {
        if (request.SupplierId <= 0) throw new BusinessRuleException("Choose the supplier.");
        string billNo = request.SupplierBillNo?.Trim() ?? "";
        if (billNo.Length == 0) throw new BusinessRuleException("Enter the supplier's own bill number, exactly as printed on it.");
        if (billNo.Length > 60) throw new BusinessRuleException("The bill number can be up to 60 characters.");
        FinanceRules.EnsureNotFuture(request.BillDate, clock, "The bill date cannot be in the future.");
        if (request.DueDate < request.BillDate) throw new BusinessRuleException("The due date cannot be before the bill date.");
        if (request.Lines == null || request.Lines.Count == 0) throw new BusinessRuleException("Add at least one line to the bill.");
        if (request.Lines.Any(l => l.Quantity <= 0m || l.Rate <= 0m)) throw new BusinessRuleException("Enter a quantity and a rate on every line.");
        if (request.Lines.Any(l => string.IsNullOrWhiteSpace(l.Description))) throw new BusinessRuleException("Describe every line.");
        if (request.Lines.Any(l => l.GstRate > 0m && string.IsNullOrWhiteSpace(l.HsnSac))) throw new BusinessRuleException("Enter the HSN or SAC code for a line that carries GST.");
        if (request.Lines.Any(l => l.GstRate < 0m || l.GstRate > 28m)) throw new BusinessRuleException("GST on a line must be between 0% and 28%.");

        long billId = await FinanceRules.InTransactionAsync(db, async () =>
        {
            DateTime nowUtc = clock.UtcNow;
            Supplier supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId, ct)
                ?? throw new BusinessRuleException("Choose the supplier.");
            SupplierTaxProfile? profile = await db.Set<SupplierTaxProfile>().FirstOrDefaultAsync(p => p.SupplierId == supplier.SupplierId, ct);
            await FinanceRules.EnsurePeriodOpenAsync(db, request.BillDate, ct, "this bill");

            PurchaseBill? bill = null;
            if (id != null)
            {
                bill = await db.Set<PurchaseBill>().Include(b => b.Lines).FirstOrDefaultAsync(b => b.PurchaseBillId == id, ct)
                       ?? throw new NotFoundException("Purchase bill", id.Value);
                if (!IsEditable(bill.Status))
                {
                    throw new BusinessRuleException("Only a draft, rejected or awaiting-approval bill can be edited. Cancel it and enter it again instead.");
                }
                await FinanceRules.EnsurePeriodOpenAsync(db, bill.BillDate, ct, "this bill");
            }

            string key = FinanceRules.BillNoKey(billNo);
            var duplicate = await db.Set<PurchaseBill>().AsNoTracking()
                .Where(b => b.SupplierId == supplier.SupplierId && b.SupplierBillNoKey == key && b.Status != PurchaseBillStatus.Cancelled
                            && (id == null || b.PurchaseBillId != id))
                .Select(b => new { b.PurchaseBillId, b.SupplierBillNo, b.CreatedDate }).FirstOrDefaultAsync(ct);
            if (duplicate != null)
            {
                throw new ConflictException($"{supplier.SupplierName} already has a bill {duplicate.SupplierBillNo}, entered on {duplicate.CreatedDate:dd MMM}. Open it instead.",
                    $"DUPLICATE_BILL_{duplicate.PurchaseBillId}");
            }

            if (request.PurchaseOrderId != null)
            {
                var po = await db.PurchaseOrders.AsNoTracking().Where(p => p.PurchaseOrderId == request.PurchaseOrderId)
                    .Select(p => new { p.SupplierId }).FirstOrDefaultAsync(ct) ?? throw new BusinessRuleException("That purchase order does not exist.");
                if (po.SupplierId != supplier.SupplierId) throw new BusinessRuleException("The purchase order is for a different supplier.");
            }

            int locationId = request.LocationId ?? await db.Locations.Where(l => l.IsDefault).Select(l => (int?)l.LocationId).FirstOrDefaultAsync(ct)
                ?? await db.Locations.Select(l => l.LocationId).FirstAsync(ct);
            string? locationState = await db.Locations.Where(l => l.LocationId == locationId).Select(l => l.State).FirstOrDefaultAsync(ct);
            string placeOfSupply = FinanceRules.StateCodeOf(locationState) ?? await settings.GetStringAsync("Quotation.HomeStateCode", "29", ct);
            string? supplierState = profile?.StateCode ?? FinanceRules.GstinStateCode(supplier.Gstin) ?? FinanceRules.StateCodeOf(supplier.State);
            bool interState = supplierState != null && supplierState != placeOfSupply;

            var lines = new List<PurchaseBillLine>();
            int n = 0;
            foreach (SavePurchaseBillLineRequest l in request.Lines)
            {
                decimal taxable = FinanceRules.Round(l.Quantity * l.Rate);
                decimal tax = FinanceRules.Round(taxable * l.GstRate / 100m);
                decimal cgst = interState ? 0m : FinanceRules.Round(tax / 2m);
                lines.Add(new PurchaseBillLine
                {
                    LineNumber = ++n,
                    Description = l.Description.Trim(),
                    HsnSac = string.IsNullOrWhiteSpace(l.HsnSac) ? null : l.HsnSac.Trim(),
                    Quantity = l.Quantity,
                    Rate = l.Rate,
                    TaxableValue = taxable,
                    GstRate = l.GstRate,
                    CgstAmount = cgst,
                    SgstAmount = interState ? 0m : tax - cgst,
                    IgstAmount = interState ? tax : 0m
                });
            }
            decimal gstTotal = lines.Sum(l => l.CgstAmount + l.SgstAmount + l.IgstAmount);
            if (gstTotal > 0m && string.IsNullOrWhiteSpace(supplier.Gstin) && !request.AcknowledgeNoGstin)
            {
                throw new BusinessRuleException("This supplier has no GSTIN, so no input credit can be claimed on this bill. Continue?", "NO_GSTIN_CONFIRM");
            }

            string? section = string.IsNullOrWhiteSpace(request.TdsSection) ? null : request.TdsSection.Trim().ToUpperInvariant();
            decimal tdsRate = 0m;
            if (section != null)
            {
                if (string.IsNullOrWhiteSpace(profile?.Pan))
                {
                    throw new BusinessRuleException("This supplier has no PAN, so TDS must be deducted at 20%. Add their PAN first.", "PAN_REQUIRED");
                }
                tdsRate = await TdsRateAsync(db, section, request.BillDate, profile!.IsIndividual, true, ct);
            }
            decimal taxableTotal = lines.Sum(l => l.TaxableValue);

            bool isNew = bill == null;
            if (bill == null)
            {
                bill = new PurchaseBill
                {
                    BillNumber = await sequences.NextAsync(BillSequence, ct),
                    EnteredByUserId = currentUser.UserId,
                    Status = PurchaseBillStatus.Draft
                };
                db.Set<PurchaseBill>().Add(bill);
            }
            else
            {
                db.RemoveRange(bill.Lines);
                bill.Lines.Clear();
            }
            bill.SupplierId = supplier.SupplierId;
            bill.SupplierBillNo = billNo;
            bill.SupplierBillNoKey = key;
            bill.BillDate = request.BillDate;
            bill.DueDate = request.DueDate;
            bill.PaymentTermsDays = request.PaymentTermsDays;
            bill.PurchaseOrderId = request.PurchaseOrderId;
            bill.LocationId = locationId;
            bill.SupplierStateCode = supplierState;
            bill.PlaceOfSupplyCode = placeOfSupply;
            bill.IsInterState = interState;
            bill.SupplierGstin = string.IsNullOrWhiteSpace(supplier.Gstin) ? null : supplier.Gstin.Trim();
            bill.TaxableValue = taxableTotal;
            bill.CgstAmount = lines.Sum(l => l.CgstAmount);
            bill.SgstAmount = lines.Sum(l => l.SgstAmount);
            bill.IgstAmount = lines.Sum(l => l.IgstAmount);
            bill.BillTotal = taxableTotal + gstTotal;
            bill.TdsSection = section;
            bill.TdsRate = tdsRate;
            bill.TdsAmount = FinanceRules.Round(taxableTotal * tdsRate / 100m);
            bill.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
            foreach (PurchaseBillLine line in lines) bill.Lines.Add(line);

            AddEvent(bill, isNew ? "Entered" : "Edited", null, nowUtc);
            if (request.Submit || bill.Status == PurchaseBillStatus.AwaitingApproval)
            {
                await SubmitInternalAsync(bill, nowUtc, ct);
            }
            await db.SaveChangesAsync(ct);
            return bill.PurchaseBillId;
        }, ct);
        return await GetBillAsync(billId, ct);
    }

    private void AddEvent(PurchaseBill bill, string type, string? comment, DateTime nowUtc)
    {
        bill.Events.Add(new PurchaseBillEvent { EventType = type, Comment = comment, UserId = currentUser.UserId, EventUtc = nowUtc });
    }

    /// <summary>Below the limit a bill is approved by itself; above it waits for BILL.APPROVE.</summary>
    private async Task SubmitInternalAsync(PurchaseBill bill, DateTime nowUtc, CancellationToken ct)
    {
        decimal limit = await settings.GetDecimalAsync("Bill.ApprovalLimit", 25000m, ct);
        bill.SubmittedUtc = nowUtc;
        bill.RejectReason = null;
        if (bill.BillTotal <= limit)
        {
            bill.Status = PurchaseBillStatus.Approved;
            bill.ApprovedByUserId = null;
            bill.ApprovedUtc = nowUtc;
            bill.ApprovalComment = $"Approved automatically: at or below the {FinanceRules.Money(limit)} approval limit.";
            AddEvent(bill, "Approved", bill.ApprovalComment, nowUtc);
        }
        else
        {
            bill.Status = PurchaseBillStatus.AwaitingApproval;
            AddEvent(bill, "Submitted", $"Above the {FinanceRules.Money(limit)} approval limit; waiting for approval.", nowUtc);
        }
    }

    private async Task<PurchaseBillDto> TransitionAsync(long id, Func<PurchaseBill, DateTime, Task> change, CancellationToken ct)
    {
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            PurchaseBill bill = await db.Set<PurchaseBill>().Include(b => b.Events).FirstOrDefaultAsync(b => b.PurchaseBillId == id, ct)
                ?? throw new NotFoundException("Purchase bill", id);
            await change(bill, clock.UtcNow);
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await GetBillAsync(id, ct);
    }

    public Task<PurchaseBillDto> SubmitAsync(long id, CancellationToken ct = default) => TransitionAsync(id, async (bill, now) =>
    {
        if (bill.Status is not (PurchaseBillStatus.Draft or PurchaseBillStatus.Rejected))
            throw new BusinessRuleException("Only a draft or rejected bill can be submitted.");
        await FinanceRules.EnsurePeriodOpenAsync(db, bill.BillDate, ct, "this bill");
        await SubmitInternalAsync(bill, now, ct);
    }, ct);

    public Task<PurchaseBillDto> ApproveAsync(long id, BillDecisionRequest request, CancellationToken ct = default) => TransitionAsync(id, async (bill, now) =>
    {
        if (bill.Status != PurchaseBillStatus.AwaitingApproval) throw new BusinessRuleException("This bill is not waiting for approval.");
        if (bill.EnteredByUserId == currentUser.UserId)
            throw new BusinessRuleException("You entered this bill, so you cannot approve it. Send it to the office manager.", "SAME_PERSON");
        await FinanceRules.EnsurePeriodOpenAsync(db, bill.BillDate, ct, "this bill");
        bill.Status = PurchaseBillStatus.Approved;
        bill.ApprovedByUserId = currentUser.UserId;
        bill.ApprovedUtc = now;
        bill.ApprovalComment = string.IsNullOrWhiteSpace(request.Comment) ? null : request.Comment.Trim();
        AddEvent(bill, "Approved", bill.ApprovalComment, now);
    }, ct);

    public Task<PurchaseBillDto> RejectAsync(long id, BillDecisionRequest request, CancellationToken ct = default) => TransitionAsync(id, (bill, now) =>
    {
        if (bill.Status != PurchaseBillStatus.AwaitingApproval) throw new BusinessRuleException("This bill is not waiting for approval.");
        if (string.IsNullOrWhiteSpace(request.Comment)) throw new BusinessRuleException("Say why this bill is being rejected.");
        if (bill.EnteredByUserId == currentUser.UserId)
            throw new BusinessRuleException("You entered this bill, so you cannot decide on it. Send it to the office manager.", "SAME_PERSON");
        bill.Status = PurchaseBillStatus.Rejected;
        bill.RejectReason = request.Comment.Trim();
        AddEvent(bill, "Rejected", bill.RejectReason, now);
        return Task.CompletedTask;
    }, ct);

    public Task<PurchaseBillDto> HoldAsync(long id, BillDecisionRequest request, CancellationToken ct = default) => TransitionAsync(id, (bill, now) =>
    {
        if (bill.Status is not (PurchaseBillStatus.Approved or PurchaseBillStatus.PartPaid))
            throw new BusinessRuleException("Only an approved or part-paid bill can be put on hold.");
        if (string.IsNullOrWhiteSpace(request.Comment)) throw new BusinessRuleException("Say what the dispute with the supplier is.");
        bill.Status = PurchaseBillStatus.OnHold;
        bill.HoldReason = request.Comment.Trim();
        AddEvent(bill, "On hold", bill.HoldReason, now);
        return Task.CompletedTask;
    }, ct);

    public Task<PurchaseBillDto> ReleaseAsync(long id, CancellationToken ct = default) => TransitionAsync(id, async (bill, now) =>
    {
        if (bill.Status != PurchaseBillStatus.OnHold) throw new BusinessRuleException("This bill is not on hold.");
        decimal paid = await LiveAllocations(db).Where(a => a.PurchaseBillId == bill.PurchaseBillId).SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;
        bill.Status = paid > 0m ? PurchaseBillStatus.PartPaid : PurchaseBillStatus.Approved;
        bill.HoldReason = null;
        AddEvent(bill, "Released", null, now);
    }, ct);

    public Task<PurchaseBillDto> CancelAsync(long id, BillDecisionRequest request, CancellationToken ct = default) => TransitionAsync(id, async (bill, now) =>
    {
        if (bill.Status is PurchaseBillStatus.Cancelled or PurchaseBillStatus.Paid) throw new BusinessRuleException("This bill cannot be cancelled.");
        if (string.IsNullOrWhiteSpace(request.Comment)) throw new BusinessRuleException("Say why this bill is being cancelled.");
        if (await db.Set<VendorPaymentAllocation>().AnyAsync(a => a.PurchaseBillId == bill.PurchaseBillId
                && db.Set<VendorPayment>().Any(v => v.VendorPaymentId == a.VendorPaymentId && !v.IsReversed), ct))
            throw new BusinessRuleException("A bill with payments against it cannot be cancelled. Reverse the payments first.");
        await FinanceRules.EnsurePeriodOpenAsync(db, bill.BillDate, ct, "this cancellation");
        bill.Status = PurchaseBillStatus.Cancelled;
        bill.CancelReason = request.Comment.Trim();
        AddEvent(bill, "Cancelled", bill.CancelReason, now);
    }, ct);

    /// <summary>Recomputes a bill's status from its live allocations (after a payment or reversal).</summary>
    internal static async Task RefreshPaidStatusAsync(RegalisDbContext db, PurchaseBill bill, long? userId, DateTime nowUtc, CancellationToken ct)
    {
        decimal paid = await LiveAllocations(db).Where(a => a.PurchaseBillId == bill.PurchaseBillId).SumAsync(a => (decimal?)a.Amount, ct) ?? 0m;
        PurchaseBillStatus next = paid >= bill.BillTotal ? PurchaseBillStatus.Paid : paid > 0m ? PurchaseBillStatus.PartPaid : PurchaseBillStatus.Approved;
        if (bill.Status == PurchaseBillStatus.OnHold && next != PurchaseBillStatus.Paid) return;
        if (bill.Status != next)
        {
            bill.Status = next;
            bill.Events.Add(new PurchaseBillEvent
            {
                PurchaseBillId = bill.PurchaseBillId,
                EventType = next == PurchaseBillStatus.Paid ? "Fully paid" : next == PurchaseBillStatus.PartPaid ? "Part paid" : "Payment reversed",
                Comment = $"Paid {FinanceRules.Money(paid)} of {FinanceRules.Money(bill.BillTotal)}.",
                UserId = userId,
                EventUtc = nowUtc
            });
        }
    }

    public async Task<PurchaseBillDto> AddAttachmentAsync(long id, AttachmentUploadInput file, CancellationToken ct = default)
    {
        PurchaseBill bill = await db.Set<PurchaseBill>().Include(b => b.Attachments).Include(b => b.Events)
            .FirstOrDefaultAsync(b => b.PurchaseBillId == id, ct) ?? throw new NotFoundException("Purchase bill", id);
        if (bill.Status == PurchaseBillStatus.Cancelled) throw new BusinessRuleException("This bill is cancelled.");
        if (bill.Attachments.Count >= 10) throw new BusinessRuleException("A bill can have up to 10 attachments.");
        var (path, contentType) = await FinanceRules.SaveUploadAsync(storage.Value.UploadRoot, "purchase-bills", id, file.FileName, file.Content, file.Length, ct);
        DateTime now = clock.UtcNow;
        bill.Attachments.Add(new PurchaseBillAttachment
        {
            FileName = Path.GetFileName(file.FileName),
            StoredPath = path,
            ContentType = contentType,
            SizeBytes = file.Length,
            UploadedByUserId = currentUser.UserId,
            UploadedUtc = now
        });
        AddEvent(bill, "Document added", Path.GetFileName(file.FileName), now);
        await db.SaveChangesAsync(ct);
        return await GetBillAsync(id, ct);
    }

    public async Task<AttachmentFileDto> GetAttachmentAsync(long id, long attachmentId, CancellationToken ct = default)
    {
        PurchaseBillAttachment a = await db.Set<PurchaseBillAttachment>().AsNoTracking()
            .FirstOrDefaultAsync(x => x.PurchaseBillId == id && x.PurchaseBillAttachmentId == attachmentId, ct)
            ?? throw new NotFoundException("Attachment", attachmentId);
        if (!File.Exists(a.StoredPath)) throw new NotFoundException("Attachment file", attachmentId);
        return new AttachmentFileDto(await File.ReadAllBytesAsync(a.StoredPath, ct), a.ContentType, a.FileName);
    }

    public async Task<byte[]> ExportAsync(PurchaseBillListRequest request, CancellationToken ct = default)
    {
        DateOnly today = FinanceRules.Today(clock);
        List<Row> rows = await Rows(Filter(request, today)).OrderBy(x => x.Bill.BillDate).ThenBy(x => x.Bill.PurchaseBillId).ToListAsync(ct);
        using var wb = new XLWorkbook();
        IXLWorksheet ws = wb.AddWorksheet("Purchase bills");
        string[] head = { "Bill", "Supplier bill no.", "Supplier", "GSTIN", "Bill date", "Due date", "PO", "Taxable", "CGST", "SGST", "IGST", "Total", "TDS", "Paid", "Balance", "Status", "Overdue days" };
        for (int c = 0; c < head.Length; c++) ws.Cell(1, c + 1).Value = head[c];
        int r = 2;
        foreach (Row x in rows)
        {
            decimal balance = x.Bill.BillTotal - x.Paid;
            bool overdue = Payable.Contains(x.Bill.Status) && balance > 0m && x.Bill.DueDate < today;
            ws.Cell(r, 1).Value = x.Bill.BillNumber;
            ws.Cell(r, 2).Value = x.Bill.SupplierBillNo;
            ws.Cell(r, 3).Value = x.SupplierName;
            ws.Cell(r, 4).Value = x.Bill.SupplierGstin ?? "";
            ws.Cell(r, 5).Value = x.Bill.BillDate.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 6).Value = x.Bill.DueDate.ToDateTime(TimeOnly.MinValue);
            ws.Cell(r, 7).Value = x.PoNumber ?? "";
            ws.Cell(r, 8).Value = x.Bill.TaxableValue;
            ws.Cell(r, 9).Value = x.Bill.CgstAmount;
            ws.Cell(r, 10).Value = x.Bill.SgstAmount;
            ws.Cell(r, 11).Value = x.Bill.IgstAmount;
            ws.Cell(r, 12).Value = x.Bill.BillTotal;
            ws.Cell(r, 13).Value = x.Bill.TdsAmount;
            ws.Cell(r, 14).Value = x.Paid;
            ws.Cell(r, 15).Value = balance;
            ws.Cell(r, 16).Value = StatusName(x.Bill.Status);
            ws.Cell(r, 17).Value = overdue ? today.DayNumber - x.Bill.DueDate.DayNumber : 0;
            r++;
        }
        ExcelStyle.Finish(ws, head.Length, r - 1, new[] { 5, 6 }, new[] { 8, 9, 10, 11, 12, 13, 14, 15 });
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<SupplierOptionDto> GetSupplierProfileAsync(int supplierId, CancellationToken ct = default)
    {
        Supplier s = await db.Suppliers.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == supplierId, ct)
            ?? throw new NotFoundException("Supplier", supplierId);
        SupplierTaxProfile? p = await db.Set<SupplierTaxProfile>().AsNoTracking().FirstOrDefaultAsync(x => x.SupplierId == supplierId, ct);
        return new SupplierOptionDto(s.SupplierId, s.SupplierName, s.SupplierCode, s.Gstin, p?.Pan,
            p?.StateCode ?? FinanceRules.GstinStateCode(s.Gstin) ?? FinanceRules.StateCodeOf(s.State), p?.DefaultTdsSection, p?.PaymentTermsDays,
            p?.IsIndividual ?? false, s.IsActive);
    }

    /// <summary>PAN (five letters, four digits, a letter), default TDS section, payment terms
    /// and GST state of a supplier (doc §10.7, §19.3).</summary>
    public async Task<SupplierOptionDto> SaveSupplierProfileAsync(int supplierId, SaveSupplierTaxProfileRequest request, CancellationToken ct = default)
    {
        if (!await db.Suppliers.AnyAsync(x => x.SupplierId == supplierId, ct)) throw new NotFoundException("Supplier", supplierId);
        string? pan = string.IsNullOrWhiteSpace(request.Pan) ? null : request.Pan.Trim().ToUpperInvariant();
        if (pan != null && !FinanceRules.IsPan(pan)) throw new BusinessRuleException("A PAN is five letters, four digits and one letter - for example AABCI1234F.");
        string? section = string.IsNullOrWhiteSpace(request.DefaultTdsSection) ? null : request.DefaultTdsSection.Trim().ToUpperInvariant();
        if (section != null)
        {
            if (pan == null) throw new BusinessRuleException("This supplier has no PAN, so TDS must be deducted at 20%. Add their PAN first.", "PAN_REQUIRED");
            if (!await db.Set<TdsSection>().AnyAsync(s => s.Section == section && s.IsActive, ct)) throw new BusinessRuleException($"{section} is not a TDS section on record.");
        }
        if (request.PaymentTermsDays is < 0 or > 365) throw new BusinessRuleException("Payment terms are between 0 and 365 days.");
        string? state = string.IsNullOrWhiteSpace(request.StateCode) ? null : FinanceRules.StateCodeOf(request.StateCode)
            ?? throw new BusinessRuleException("Choose the supplier's state.");
        SupplierTaxProfile? p = await db.Set<SupplierTaxProfile>().FirstOrDefaultAsync(x => x.SupplierId == supplierId, ct);
        if (p == null)
        {
            p = new SupplierTaxProfile { SupplierId = supplierId };
            db.Set<SupplierTaxProfile>().Add(p);
        }
        p.Pan = pan;
        p.DefaultTdsSection = section;
        p.PaymentTermsDays = request.PaymentTermsDays;
        p.StateCode = state;
        p.IsIndividual = request.IsIndividual;
        await db.SaveChangesAsync(ct);
        return await GetSupplierProfileAsync(supplierId, ct);
    }

    public static string StatusName(PurchaseBillStatus s) => s switch
    {
        PurchaseBillStatus.Draft => "Draft",
        PurchaseBillStatus.AwaitingApproval => "Awaiting approval",
        PurchaseBillStatus.Approved => "Approved",
        PurchaseBillStatus.PartPaid => "Part paid",
        PurchaseBillStatus.Paid => "Paid",
        PurchaseBillStatus.Rejected => "Rejected",
        PurchaseBillStatus.OnHold => "On hold",
        _ => "Cancelled"
    };
}

/// <summary>One look for every Excel this module produces: bold frozen header, date and
/// rupee formats, a totals row, fitted columns.</summary>
internal static class ExcelStyle
{
    public static void Finish(IXLWorksheet ws, int columns, int lastRow, int[] dateColumns, int[] moneyColumns, bool totals = true)
    {
        IXLRange header = ws.Range(1, 1, 1, columns);
        header.Style.Font.Bold = true;
        header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2EDE1");
        ws.SheetView.FreezeRows(1);
        foreach (int c in dateColumns) ws.Column(c).Style.DateFormat.Format = "dd-mmm-yyyy";
        foreach (int c in moneyColumns) ws.Column(c).Style.NumberFormat.Format = "#,##0.00";
        if (totals && lastRow >= 2 && moneyColumns.Length > 0)
        {
            int t = lastRow + 1;
            ws.Cell(t, 1).Value = "Total";
            ws.Row(t).Style.Font.Bold = true;
            foreach (int c in moneyColumns)
            {
                ws.Cell(t, c).FormulaA1 = $"SUM({ws.Cell(2, c).Address}:{ws.Cell(lastRow, c).Address})";
            }
        }
        ws.Columns(1, columns).AdjustToContents(1, Math.Min(lastRow + 1, 500));
    }
}
