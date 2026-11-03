using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// Vendor payments (build doc §6): one payment settles several approved bills, less the TDS
/// Regalis must deduct, optionally using up an advance; with no bill ticked it is an
/// advance. Posting is one transaction: the payment, every allocation, the TDS payable
/// (written here, at payment - never at bill entry) and the bill statuses move together.
/// Reversed, never edited.
/// </summary>
public class VendorPaymentService(
    RegalisDbContext db,
    INumberSequenceService sequences,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IVendorPaymentService
{
    public const string PaymentSequence = "VendorPayment";

    private sealed record BillRow(PurchaseBill b, string? PoNumber, decimal Paid, decimal Tds);

    public static string ModeName(VendorPaymentMode mode) => mode switch
    {
        VendorPaymentMode.Neft => "NEFT",
        VendorPaymentMode.Rtgs => "RTGS",
        VendorPaymentMode.Imps => "IMPS",
        VendorPaymentMode.Upi => "UPI",
        VendorPaymentMode.Cheque => "Cheque",
        _ => "Cash"
    };

    /// <summary>Unadjusted advance held with a supplier.</summary>
    internal static async Task<decimal> AdvanceAvailableAsync(RegalisDbContext db, int supplierId, CancellationToken ct)
    {
        var sums = await db.Set<VendorPayment>().Where(v => v.SupplierId == supplierId && !v.IsReversed)
            .GroupBy(_ => 1).Select(g => new { Paid = g.Sum(v => v.AdvanceAmount), Used = g.Sum(v => v.AdvanceAdjusted) })
            .FirstOrDefaultAsync(ct);
        return sums == null ? 0m : sums.Paid - sums.Used;
    }

    public async Task<SupplierPayablesDto> GetSupplierPayablesAsync(int supplierId, CancellationToken ct = default)
    {
        Supplier supplier = await db.Suppliers.AsNoTracking().FirstOrDefaultAsync(s => s.SupplierId == supplierId, ct)
            ?? throw new NotFoundException("Supplier", supplierId);
        string? pan = await db.Set<SupplierTaxProfile>().Where(p => p.SupplierId == supplierId).Select(p => p.Pan).FirstOrDefaultAsync(ct);
        DateOnly today = FinanceRules.Today(clock);
        var bills = await (from b in db.Set<PurchaseBill>().AsNoTracking()
                           where b.SupplierId == supplierId
                                 && (b.Status == PurchaseBillStatus.Approved || b.Status == PurchaseBillStatus.PartPaid
                                     || b.Status == PurchaseBillStatus.AwaitingApproval || b.Status == PurchaseBillStatus.OnHold
                                     || b.Status == PurchaseBillStatus.Draft || b.Status == PurchaseBillStatus.Rejected)
                           join po in db.PurchaseOrders on b.PurchaseOrderId equals (long?)po.PurchaseOrderId into pj
                           from po in pj.DefaultIfEmpty()
                           select new BillRow(b, po != null ? po.PoNumber : null,
                               PurchaseBillService.LiveAllocations(db).Where(a => a.PurchaseBillId == b.PurchaseBillId).Sum(a => (decimal?)a.Amount) ?? 0m,
                               PurchaseBillService.LiveAllocations(db).Where(a => a.PurchaseBillId == b.PurchaseBillId).Sum(a => (decimal?)a.TdsAmount) ?? 0m))
            .ToListAsync(ct);
        OutstandingBillDto Map(BillRow x)
        {
            PurchaseBill b = x.b;
            decimal balance = b.BillTotal - x.Paid;
            decimal tdsLeft = Math.Max(0m, Math.Min(balance, b.TdsAmount - x.Tds));
            return new OutstandingBillDto(b.PurchaseBillId, b.BillNumber, b.SupplierBillNo, b.BillDate, x.PoNumber, b.DueDate,
                b.DueDate < today, b.BillTotal, balance, tdsLeft, b.TdsSection, b.TdsRate, (byte)b.Status);
        }
        var payable = bills.Where(x => x.b.Status is PurchaseBillStatus.Approved or PurchaseBillStatus.PartPaid && x.b.BillTotal - x.Paid > 0m)
            .OrderBy(x => x.b.DueDate).ThenBy(x => x.b.PurchaseBillId).Select(x => Map(x)).ToList();
        var notPayable = bills.Where(x => x.b.Status is not (PurchaseBillStatus.Approved or PurchaseBillStatus.PartPaid))
            .OrderBy(x => x.b.DueDate).Select(x => Map(x)).ToList();
        return new SupplierPayablesDto(supplier.SupplierId, supplier.SupplierName, pan, await AdvanceAvailableAsync(db, supplierId, ct), payable, notPayable);
    }

    private IQueryable<VendorPaymentDto> Project(IQueryable<VendorPayment> query)
    {
        bool canReverse = currentUser.HasPermission("PAYMENT.REVERSE") || currentUser.HasPermission("BILL.PAY");
        return from v in query
               join s in db.Suppliers on v.SupplierId equals s.SupplierId
               join a in db.BankAccounts on v.BankAccountId equals (int?)a.BankAccountId into aj
               from a in aj.DefaultIfEmpty()
               join u in db.Users on v.PaidByUserId equals u.UserId into uj
               from u in uj.DefaultIfEmpty()
               let reconciled = db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.VendorPayment && m.DocId == v.VendorPaymentId)
               select new VendorPaymentDto(v.VendorPaymentId, v.PaymentNumber, v.SupplierId, s.SupplierName, v.PaidOn, v.Amount, v.TdsAmount,
                   v.AdvanceAdjusted, v.AdvanceAmount, (byte)v.Mode,
                   v.Mode == VendorPaymentMode.Neft ? "NEFT" : v.Mode == VendorPaymentMode.Rtgs ? "RTGS" : v.Mode == VendorPaymentMode.Imps ? "IMPS"
                       : v.Mode == VendorPaymentMode.Upi ? "UPI" : v.Mode == VendorPaymentMode.Cheque ? "Cheque" : "Cash",
                   v.Reference, v.BankAccountId, v.FromPettyCash ? "Petty cash" : a != null ? a.BankName + " · " + a.AccountCode : null,
                   v.FromPettyCash, v.Note, v.Allocations.Count, reconciled, v.IsReversed, v.ReverseReason, u != null ? u.FullName : null, v.CreatedDate,
                   (from al in v.Allocations
                    join b in db.Set<PurchaseBill>() on al.PurchaseBillId equals b.PurchaseBillId
                    select new VendorPaymentAllocationDto(al.PurchaseBillId, b.BillNumber, b.SupplierBillNo, al.Amount, al.TdsAmount)).ToList(),
                   canReverse && !v.IsReversed && !reconciled);
    }

    public async Task<PagedResult<VendorPaymentDto>> GetPaymentsAsync(VendorPaymentListRequest request, CancellationToken ct = default)
    {
        IQueryable<VendorPayment> q = db.Set<VendorPayment>().AsNoTracking();
        if (request.SupplierId != null) q = q.Where(v => v.SupplierId == request.SupplierId);
        if (request.From != null) q = q.Where(v => v.PaidOn >= request.From);
        if (request.To != null) q = q.Where(v => v.PaidOn <= request.To);
        if (request.Reconciled != null)
        {
            bool want = request.Reconciled.Value;
            q = q.Where(v => db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.VendorPayment && m.DocId == v.VendorPaymentId) == want);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string s = request.Search.Trim();
            q = q.Where(v => v.PaymentNumber.Contains(s) || (v.Reference != null && v.Reference.Contains(s))
                             || db.Suppliers.Any(x => x.SupplierId == v.SupplierId && x.SupplierName.Contains(s)));
        }
        int total = await q.CountAsync(ct);
        var items = await Project(q.OrderByDescending(v => v.PaidOn).ThenByDescending(v => v.VendorPaymentId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)).ToListAsync(ct);
        return PagedResult<VendorPaymentDto>.Create(items, request.Page, request.PageSize, total);
    }

    public async Task<VendorPaymentDto> GetPaymentAsync(long id, CancellationToken ct = default)
    {
        return await Project(db.Set<VendorPayment>().AsNoTracking().Where(v => v.VendorPaymentId == id)).FirstOrDefaultAsync(ct)
               ?? throw new NotFoundException("Vendor payment", id);
    }

    public async Task<VendorPaymentResultDto> CreateAsync(CreateVendorPaymentRequest request, CancellationToken ct = default)
    {
        long id = await FinanceRules.InTransactionAsync(db, async () =>
        {
            VendorPayment payment = await PostAsync(db, sequences, currentUser, clock, request, null, ct);
            return payment.VendorPaymentId;
        }, ct);
        VendorPaymentDto dto = await GetPaymentAsync(id, ct);
        string message;
        if (dto.Allocations.Count == 0)
        {
            message = $"{dto.PaymentNumber} recorded: an advance of {FinanceRules.Money(dto.Amount)} to {dto.SupplierName}. It will be adjusted against their next bills.";
        }
        else
        {
            var billStates = await db.Set<PurchaseBill>().AsNoTracking()
                .Where(b => dto.Allocations.Select(a => a.PurchaseBillId).Contains(b.PurchaseBillId))
                .Select(b => new { b.SupplierBillNo, b.Status }).ToListAsync(ct);
            string bills = string.Join(", ", billStates.Select(b => $"{b.SupplierBillNo} is now {PurchaseBillService.StatusName(b.Status)}"));
            message = $"{dto.PaymentNumber} recorded. {bills}."
                      + (dto.TdsAmount > 0m ? $" A TDS payable of {FinanceRules.Money(dto.TdsAmount)} has been raised for the quarter." : "")
                      + (dto.FromPettyCash ? " Paid from petty cash." : " It is waiting in the bank matching queue for the debit to appear on the statement.");
        }
        return new VendorPaymentResultDto(dto, message);
    }

    /// <summary>The posting itself; also used by bank matching (a debit line settling bills).
    /// Must run inside the caller's transaction.</summary>
    internal static async Task<VendorPayment> PostAsync(RegalisDbContext db, INumberSequenceService sequences, ICurrentUser currentUser,
        IDateTimeProvider clock, CreateVendorPaymentRequest request, string? fromBankLine, CancellationToken ct)
    {
        if (request.Mode < 1 || request.Mode > 6) throw new BusinessRuleException("Choose the payment mode.");
        var mode = (VendorPaymentMode)request.Mode;
        string? reference = string.IsNullOrWhiteSpace(request.Reference) ? null : request.Reference.Trim();
        if (reference == null && mode != VendorPaymentMode.Cash) throw new BusinessRuleException("Enter the UTR or cheque number.");
        if (request.FromPettyCash && mode != VendorPaymentMode.Cash) throw new BusinessRuleException("A payment from petty cash is a cash payment.");
        if (!request.FromPettyCash && request.BankAccountId == null) throw new BusinessRuleException("Choose which account the money is paid from.");
        FinanceRules.EnsureNotFuture(request.PaidOn, clock, "The payment date cannot be in the future.");
        await FinanceRules.EnsurePeriodOpenAsync(db, request.PaidOn, ct, "this payment");
        if (request.BankAccountId != null && !request.FromPettyCash && !await db.BankAccounts.AnyAsync(a => a.BankAccountId == request.BankAccountId && a.IsActive, ct))
            throw new BusinessRuleException("Choose which account the money is paid from.");

        Supplier supplier = await db.Suppliers.FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId, ct)
            ?? throw new BusinessRuleException("Choose the supplier.");
        SupplierTaxProfile? profile = await db.Set<SupplierTaxProfile>().FirstOrDefaultAsync(p => p.SupplierId == supplier.SupplierId, ct);
        var allocations = (request.Allocations ?? new List<VendorAllocationRequest>()).Where(a => a.Amount > 0m || a.TdsAmount > 0m).ToList();
        if (allocations.Select(a => a.PurchaseBillId).Distinct().Count() != allocations.Count)
            throw new BusinessRuleException("Each bill can be allocated once per payment.");
        decimal advanceToAdjust = FinanceRules.Round(Math.Max(0m, request.AdvanceToAdjust));
        decimal amount = FinanceRules.Round(request.Amount);
        DateTime nowUtc = clock.UtcNow;

        var payment = new VendorPayment
        {
            PaymentNumber = await sequences.NextAsync(PaymentSequence, ct),
            SupplierId = supplier.SupplierId,
            PaidOn = request.PaidOn,
            Mode = mode,
            Reference = reference,
            BankAccountId = request.FromPettyCash ? null : request.BankAccountId,
            FromPettyCash = request.FromPettyCash,
            Note = string.IsNullOrWhiteSpace(request.Note) ? fromBankLine : request.Note.Trim(),
            PaidByUserId = currentUser.UserId
        };

        var touched = new List<PurchaseBill>();
        decimal cashTotal = 0m, tdsTotal = 0m;
        if (allocations.Count == 0)
        {
            if (amount <= 0m) throw new BusinessRuleException("Tick the bills this payment settles, or save it as an advance.");
            if (advanceToAdjust > 0m) throw new BusinessRuleException("An advance can only be adjusted against bills.");
            payment.Amount = amount;
            payment.AdvanceAmount = amount;
        }
        else
        {
            foreach (VendorAllocationRequest a in allocations)
            {
                PurchaseBill bill = await db.Set<PurchaseBill>().Include(b => b.Events).FirstOrDefaultAsync(b => b.PurchaseBillId == a.PurchaseBillId, ct)
                    ?? throw new NotFoundException("Purchase bill", a.PurchaseBillId);
                if (bill.SupplierId != supplier.SupplierId) throw new BusinessRuleException($"{bill.SupplierBillNo} belongs to a different supplier.");
                if (bill.Status is not (PurchaseBillStatus.Approved or PurchaseBillStatus.PartPaid))
                {
                    throw new BusinessRuleException(bill.Status == PurchaseBillStatus.AwaitingApproval
                        ? $"{bill.SupplierBillNo} is still awaiting approval and cannot be paid yet."
                        : $"{bill.SupplierBillNo} is {PurchaseBillService.StatusName(bill.Status).ToLowerInvariant()} and cannot be paid.");
                }
                decimal cash = FinanceRules.Round(Math.Max(0m, a.Amount));
                decimal tds = FinanceRules.Round(Math.Max(0m, a.TdsAmount));
                decimal paid = await PurchaseBillService.LiveAllocations(db).Where(x => x.PurchaseBillId == bill.PurchaseBillId).SumAsync(x => (decimal?)x.Amount, ct) ?? 0m;
                decimal balance = bill.BillTotal - paid;
                if (cash + tds > balance)
                {
                    throw new BusinessRuleException($"{FinanceRules.Money(Math.Max(0m, balance - tds))} is the most that can be allocated to {bill.SupplierBillNo}.");
                }
                if (tds > 0m && string.IsNullOrWhiteSpace(profile?.Pan))
                {
                    throw new BusinessRuleException("This supplier has no PAN, so TDS must be deducted at 20%. Add their PAN first.", "PAN_REQUIRED");
                }
                payment.Allocations.Add(new VendorPaymentAllocation { PurchaseBillId = bill.PurchaseBillId, Amount = cash + tds, TdsAmount = tds });
                cashTotal += cash;
                tdsTotal += tds;
                touched.Add(bill);
            }
            if (advanceToAdjust > 0m)
            {
                decimal available = await AdvanceAvailableAsync(db, supplier.SupplierId, ct);
                if (advanceToAdjust > available)
                    throw new BusinessRuleException($"Only {FinanceRules.Money(available)} of advance is available with this supplier.");
                if (advanceToAdjust > cashTotal) throw new BusinessRuleException("The advance adjusted cannot be more than the bills being paid.");
            }
            decimal expected = cashTotal - advanceToAdjust;
            if (amount != expected)
            {
                throw new BusinessRuleException($"The allocations come to {FinanceRules.Money(expected)} but the payment less TDS is {FinanceRules.Money(amount)}. They must match exactly.", "ALLOCATION_MISMATCH");
            }
            payment.Amount = amount;
            payment.TdsAmount = tdsTotal;
            payment.AdvanceAdjusted = advanceToAdjust;
        }

        db.Set<VendorPayment>().Add(payment);
        await db.SaveChangesAsync(ct);

        foreach (PurchaseBill bill in touched)
        {
            VendorPaymentAllocation alloc = payment.Allocations.First(x => x.PurchaseBillId == bill.PurchaseBillId);
            if (alloc.TdsAmount > 0m)
            {
                string section = bill.TdsSection ?? profile?.DefaultTdsSection ?? "194C";
                decimal rate = bill.TdsRate > 0m ? bill.TdsRate : await PurchaseBillService.TdsRateAsync(db, section, request.PaidOn, profile?.IsIndividual ?? false, true, ct);
                db.Set<TdsEntry>().Add(new TdsEntry
                {
                    Direction = TdsDirection.Payable,
                    PartyType = "Supplier",
                    PartyId = supplier.SupplierId,
                    PartyName = supplier.SupplierName,
                    PartyTaxId = profile?.Pan,
                    Section = section,
                    Rate = rate,
                    BaseAmount = rate > 0m ? FinanceRules.Round(alloc.TdsAmount * 100m / rate) : bill.TaxableValue,
                    TdsAmount = alloc.TdsAmount,
                    DeductedOn = request.PaidOn,
                    Quarter = FinanceRules.Quarter(request.PaidOn),
                    FinancialYear = FinanceRules.FinancialYear(request.PaidOn),
                    SourceType = "VendorPayment",
                    SourceId = payment.VendorPaymentId,
                    DocumentNumber = bill.SupplierBillNo
                });
            }
            bill.Events.Add(new PurchaseBillEvent
            {
                EventType = "Payment",
                Comment = $"{payment.PaymentNumber}: {FinanceRules.Money(alloc.Amount - alloc.TdsAmount)} paid" + (alloc.TdsAmount > 0m ? $" + {FinanceRules.Money(alloc.TdsAmount)} TDS" : ""),
                UserId = currentUser.UserId,
                EventUtc = nowUtc
            });
        }
        await db.SaveChangesAsync(ct);
        foreach (PurchaseBill bill in touched)
        {
            await PurchaseBillService.RefreshPaidStatusAsync(db, bill, currentUser.UserId, nowUtc, ct);
        }
        if (payment.FromPettyCash && payment.Amount > 0m)
        {
            PettyCashEntry entry = await PettyCashService.PostAsync(db, PettyCashEntryType.Expense, -payment.Amount, request.PaidOn,
                $"{payment.PaymentNumber} to {supplier.SupplierName}", currentUser.UserId, null, ct);
            entry.VendorPaymentId = payment.VendorPaymentId;
        }
        await db.SaveChangesAsync(ct);
        return payment;
    }

    public async Task<VendorPaymentDto> ReverseAsync(long id, ReversePaymentRequest request, CancellationToken ct = default)
    {
        string reason = request.Reason?.Trim() ?? "";
        if (reason.Length < 3) throw new BusinessRuleException("Say why this payment is being reversed.");
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            await ReverseInternalAsync(db, currentUser, clock, id, reason, false, ct);
            return true;
        }, ct);
        return await GetPaymentAsync(id, ct);
    }

    internal static async Task ReverseInternalAsync(RegalisDbContext db, ICurrentUser currentUser, IDateTimeProvider clock, long id, string reason,
        bool fromUnmatch, CancellationToken ct)
    {
        DateTime nowUtc = clock.UtcNow;
        VendorPayment payment = await db.Set<VendorPayment>().Include(v => v.Allocations).FirstOrDefaultAsync(v => v.VendorPaymentId == id, ct)
            ?? throw new NotFoundException("Vendor payment", id);
        if (payment.IsReversed) throw new BusinessRuleException($"{payment.PaymentNumber} has already been reversed.");
        if (!fromUnmatch && await db.Set<BankMatchAllocation>().AnyAsync(m => m.DocType == BankMatchDocType.VendorPayment && m.DocId == id, ct))
            throw new BusinessRuleException("This payment is matched to a bank line. Unmatch it on Transactions first.");
        await FinanceRules.EnsurePeriodOpenAsync(db, payment.PaidOn, ct, "this reversal");
        await FinanceRules.EnsurePeriodOpenAsync(db, FinanceRules.Today(clock), ct, "this reversal");
        if (payment.AdvanceAmount > 0m)
        {
            decimal available = await AdvanceAvailableAsync(db, payment.SupplierId, ct);
            if (available < payment.AdvanceAmount)
                throw new BusinessRuleException("This advance has already been adjusted against bills. Reverse those payments first.");
        }
        List<TdsEntry> tds = await db.Set<TdsEntry>().Where(t => t.SourceType == "VendorPayment" && t.SourceId == id).ToListAsync(ct);
        long? challan = tds.FirstOrDefault(t => t.TdsChallanId != null)?.TdsChallanId;
        if (challan != null)
        {
            string no = await db.Set<TdsChallan>().Where(c => c.TdsChallanId == challan).Select(c => c.ChallanNo).FirstAsync(ct);
            throw new BusinessRuleException($"The TDS from this payment is already on challan {no}, so the payment cannot be reversed.");
        }
        foreach (TdsEntry t in tds) db.Remove(t);

        payment.IsReversed = true;
        payment.ReversedByUserId = currentUser.UserId;
        payment.ReversedUtc = nowUtc;
        payment.ReverseReason = reason;
        await db.SaveChangesAsync(ct);

        foreach (VendorPaymentAllocation a in payment.Allocations)
        {
            PurchaseBill bill = await db.Set<PurchaseBill>().Include(b => b.Events).FirstAsync(b => b.PurchaseBillId == a.PurchaseBillId, ct);
            bill.Events.Add(new PurchaseBillEvent { EventType = "Payment reversed", Comment = $"{payment.PaymentNumber}: {reason}", UserId = currentUser.UserId, EventUtc = nowUtc });
            await PurchaseBillService.RefreshPaidStatusAsync(db, bill, currentUser.UserId, nowUtc, ct);
        }
        if (payment.FromPettyCash && payment.Amount > 0m)
        {
            await PettyCashService.PostAsync(db, PettyCashEntryType.Adjustment, payment.Amount, FinanceRules.Today(clock),
                $"Reversal of {payment.PaymentNumber}", currentUser.UserId, reason, ct);
        }
        await db.SaveChangesAsync(ct);
    }
}
