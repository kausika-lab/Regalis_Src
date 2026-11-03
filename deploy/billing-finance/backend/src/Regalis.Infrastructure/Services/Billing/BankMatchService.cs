using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance;
using Regalis.Application.Finance.Billing;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Persistence.Interceptors;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.Billing;

/// <summary>
/// Matching a bank line (build doc §9): one line to several documents, and every rupee of
/// difference explained by one of six reasons. Applied amounts plus explained differences
/// must equal the bank line to the rupee or nothing reconciles. The system ranks candidates
/// and may suggest one; a person presses the button. A reconciled line, its allocations and
/// whatever its differences created are frozen until someone unmatches it, with a reason.
/// </summary>
public class BankMatchService(
    RegalisDbContext db,
    INumberSequenceService sequences,
    IWalletService wallets,
    ISettingService settings,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IBankMatchService
{
    private static readonly Dictionary<BankDifferenceReason, (string Code, string Label, string Help, string? Permission)> ReasonInfo = new()
    {
        [BankDifferenceReason.TdsDeducted] = ("TDS", "TDS deducted", "They paid less because they withheld tax. The document is settled in full and a TDS entry is raised.", null),
        [BankDifferenceReason.BankCharge] = ("BANK_CHARGE", "Bank charge", "NEFT fee, cheque return fee, card commission. Recorded as an expense in the bank charges category.", null),
        [BankDifferenceReason.InterestEarned] = ("INTEREST", "Interest earned", "Interest credited by the bank. Recorded as other income against this line.", null),
        [BankDifferenceReason.Rounding] = ("ROUNDING", "Rounding", "A few paise or rupees either way. Written off, up to the rounding cap.", null),
        [BankDifferenceReason.ShortPaymentAccepted] = ("SHORT_PAYMENT", "Short payment accepted", "A discount agreed with the customer. The invoice is closed with this reason.", "BANK.WRITEOFF"),
        [BankDifferenceReason.AdvanceReceived] = ("ADVANCE", "Advance", "They paid more than they owed (the extra goes to the customer's wallet), or this is an advance to a supplier.", null)
    };

    private static string DocLabel(BankMatchDocType t) => t switch
    {
        BankMatchDocType.Invoice => "Invoice",
        BankMatchDocType.CustomerPayment => "Customer payment",
        BankMatchDocType.PurchaseBill => "Purchase bill",
        BankMatchDocType.VendorPayment => "Vendor payment",
        BankMatchDocType.Expense => "Expense",
        _ => "Petty cash top-up"
    };

    /// <summary>The effect of a difference on the bank figure: bank = applied + sum(signed).</summary>
    private static decimal Signed(BankDifferenceReason reason, BankTransactionDirection dir, decimal amount) => reason switch
    {
        BankDifferenceReason.Rounding => amount,
        BankDifferenceReason.TdsDeducted => -Math.Abs(amount),
        BankDifferenceReason.ShortPaymentAccepted => -Math.Abs(amount),
        BankDifferenceReason.InterestEarned => Math.Abs(amount),
        BankDifferenceReason.AdvanceReceived => Math.Abs(amount),
        BankDifferenceReason.BankCharge => dir == BankTransactionDirection.Credit ? -Math.Abs(amount) : Math.Abs(amount),
        _ => amount
    };

    private static bool Allowed(BankDifferenceReason reason, BankTransactionDirection dir) =>
        dir == BankTransactionDirection.Credit || reason is not (BankDifferenceReason.InterestEarned or BankDifferenceReason.ShortPaymentAccepted);

    // ── Lines ─────────────────────────────────────────────────────────────────────

    private async Task<List<BankLineDto>> ShapeAsync(IQueryable<BankTransaction> q, CancellationToken ct)
    {
        var lines = await (from t in q
                           join a in db.BankAccounts on t.BankAccountId equals a.BankAccountId
                           join u in db.Users on t.ReconciledByUserId equals (long?)u.UserId into uj
                           from u in uj.DefaultIfEmpty()
                           join p in db.Payments on t.MatchedPaymentId equals (long?)p.PaymentId into pj
                           from p in pj.DefaultIfEmpty()
                           join x in db.Expenses on t.MatchedExpenseId equals (long?)x.ExpenseId into xj
                           from x in xj.DefaultIfEmpty()
                           select new { t, Account = a.BankName + " · " + a.AccountCode, By = u != null ? u.FullName : null,
                               Legacy = p != null ? p.PaymentNumber : x != null ? x.ExpenseNumber : null }).ToListAsync(ct);
        var ids = lines.Select(l => l.t.BankTransactionId).ToList();
        var allocs = await db.Set<BankMatchAllocation>().AsNoTracking().Where(m => ids.Contains(m.BankTransactionId)).ToListAsync(ct);
        var diffs = await db.Set<BankMatchDifference>().AsNoTracking().Where(d => ids.Contains(d.BankTransactionId)).ToListAsync(ct);
        return lines.Select(l => new BankLineDto(l.t.BankTransactionId, l.t.BankAccountId, l.Account, l.t.TransactionDate, (byte)l.t.Direction,
            l.t.Amount, l.t.Narration, l.t.ReferenceNumber, l.t.BankReferenceId, l.t.Category, (byte)l.t.ReconciliationStatus, l.t.ImportBatchId,
            l.t.ReconciledUtc, l.By,
            allocs.Where(a => a.BankTransactionId == l.t.BankTransactionId).Select(a => new BankLineDocDto((byte)a.DocType, a.DocId, a.DocNumber, a.Amount)).ToList(),
            diffs.Where(d => d.BankTransactionId == l.t.BankTransactionId).Select(d => new BankLineDifferenceDto((byte)d.Reason, ReasonInfo[d.Reason].Label,
                d.SignedAmount, d.TdsSection, d.Note)).ToList(),
            l.Legacy)).ToList();
    }

    public async Task<BankLinesResultDto> GetLinesAsync(BankLineListRequest request, CancellationToken ct = default)
    {
        IQueryable<BankTransaction> q = db.BankTransactions.AsNoTracking();
        if (request.BankAccountId != null) q = q.Where(t => t.BankAccountId == request.BankAccountId);
        if (request.Direction != null) q = q.Where(t => (byte)t.Direction == request.Direction);
        if (request.Status == "open") q = q.Where(t => t.ReconciliationStatus != ReconciliationStatus.Reconciled);
        else if (byte.TryParse(request.Status, out byte st)) q = q.Where(t => (byte)t.ReconciliationStatus == st);
        if (request.From != null) q = q.Where(t => t.TransactionDate >= request.From);
        if (request.To != null) q = q.Where(t => t.TransactionDate <= request.To);
        if (!string.IsNullOrWhiteSpace(request.BatchCode)) q = q.Where(t => t.ImportBatchId == request.BatchCode);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            string s = request.Search.Trim();
            q = q.Where(t => t.Narration.Contains(s) || (t.ReferenceNumber != null && t.ReferenceNumber.Contains(s)) || (t.BankReferenceId != null && t.BankReferenceId.Contains(s))
                             || db.Set<BankMatchAllocation>().Any(m => m.BankTransactionId == t.BankTransactionId && m.DocNumber.Contains(s)));
        }
        int total = await q.CountAsync(ct);
        int open = await q.CountAsync(t => t.ReconciliationStatus != ReconciliationStatus.Reconciled, ct);
        var items = await ShapeAsync(q.OrderByDescending(t => t.TransactionDate).ThenByDescending(t => t.BankTransactionId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize), ct);
        decimal balance = 0m;
        if (request.BankAccountId != null)
        {
            decimal opening = await db.BankAccounts.Where(a => a.BankAccountId == request.BankAccountId).Select(a => a.OpeningBalance).FirstOrDefaultAsync(ct);
            balance = opening + (await db.BankTransactions.Where(t => t.BankAccountId == request.BankAccountId)
                .SumAsync(t => (decimal?)(t.Direction == BankTransactionDirection.Credit ? t.Amount : -t.Amount), ct) ?? 0m);
        }
        return new BankLinesResultDto(PagedResult<BankLineDto>.Create(items, request.Page, request.PageSize, total), open, balance);
    }

    private async Task<BankLineDto> LineAsync(long id, CancellationToken ct) =>
        (await ShapeAsync(db.BankTransactions.AsNoTracking().Where(t => t.BankTransactionId == id), ct)).FirstOrDefault()
        ?? throw new NotFoundException("Bank line", id);

    // ── Candidates ───────────────────────────────────────────────────────────────

    private static readonly Regex Words = new("[A-Za-z]{4,}");

    private static int Score(BankTransaction line, decimal outstanding, DateOnly date, string number, string? party, string? reference)
    {
        int score = 0;
        string narr = $"{line.Narration} {line.ReferenceNumber} {line.BankReferenceId}".ToUpperInvariant();
        if (number.Length > 3 && narr.Contains(number.ToUpperInvariant())) score += 40;
        if (!string.IsNullOrWhiteSpace(reference) && reference.Length > 3 && narr.Contains(reference.ToUpperInvariant())) score += 40;
        if (party != null && Words.Matches(party).Any(m => narr.Contains(m.Value.ToUpperInvariant()))) score += 30;
        if (outstanding == line.Amount) score += 50;
        else if (Math.Abs(outstanding - line.Amount) <= line.Amount * 0.01m) score += 30;
        else if (outstanding < line.Amount) score += 10;
        int days = Math.Abs(line.TransactionDate.DayNumber - date.DayNumber);
        score += days <= 3 ? 20 : days <= 7 ? 10 : days <= 30 ? 5 : 0;
        return score;
    }

    private async Task<List<MatchCandidateRowDto>> CandidatesAsync(BankTransaction line, string? search, CancellationToken ct)
    {
        var rows = new List<MatchCandidateRowDto>();
        string? s = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        DateOnly winFrom = line.TransactionDate.AddDays(-120), winTo = line.TransactionDate.AddDays(30);
        void Add(BankMatchDocType type, long id, string number, string? party, long? partyId, DateOnly date, decimal total, decimal outstanding, bool full, string? reference, string? hint)
        {
            rows.Add(new MatchCandidateRowDto((byte)type, DocLabel(type), id, number, party, partyId, date, total, outstanding, full,
                Score(line, outstanding, date, number, party, reference), false, hint));
        }
        if (line.Direction == BankTransactionDirection.Credit)
        {
            var invoices = await db.Invoices.AsNoTracking()
                .Where(i => i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded && i.TotalAmount - i.PaidAmount > 0m)
                .Where(i => s != null ? (i.InvoiceNumber.Contains(s) || i.CustomerName.Contains(s)) : i.InvoiceDate <= winTo)
                .Select(i => new { i.InvoiceId, i.InvoiceNumber, i.CustomerName, i.CustomerId, i.InvoiceDate, i.TotalAmount, Outstanding = i.TotalAmount - i.PaidAmount })
                .Take(300).ToListAsync(ct);
            foreach (var i in invoices) Add(BankMatchDocType.Invoice, i.InvoiceId, i.InvoiceNumber, i.CustomerName, i.CustomerId, i.InvoiceDate, i.TotalAmount, i.Outstanding, false, null, null);
            DateTime fromUtc = Shared.IstClock.ToUtc(winFrom.ToDateTime(TimeOnly.MinValue)), toUtc = Shared.IstClock.ToUtc(winTo.AddDays(1).ToDateTime(TimeOnly.MinValue));
            var payments = await (from p in db.Payments.AsNoTracking()
                                  join u in db.Users on p.CustomerId equals u.UserId into uj
                                  from u in uj.DefaultIfEmpty()
                                  where p.Status == PaymentStatus.Success && p.PaymentMethod != PaymentMethod.Wallet
                                        && !db.BankTransactions.Any(t => t.MatchedPaymentId == p.PaymentId)
                                        && !db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.CustomerPayment && m.DocId == p.PaymentId)
                                        && !db.Set<InvoicePayment>().Any(x => x.PaymentId == p.PaymentId && x.BankTransactionId != null)
                                        && (s != null ? (p.PaymentNumber.Contains(s) || (u != null && u.FullName.Contains(s)) || (p.GatewayPaymentId != null && p.GatewayPaymentId.Contains(s)))
                                                      : (p.PaidUtc ?? p.CreatedDate) >= fromUtc && (p.PaidUtc ?? p.CreatedDate) < toUtc)
                                  select new { p.PaymentId, p.PaymentNumber, FullName = u != null ? u.FullName : "", p.CustomerId, When = p.PaidUtc ?? p.CreatedDate, p.Amount, p.PaymentMethod, p.GatewayPaymentId, p.Gateway })
                .Take(300).ToListAsync(ct);
            foreach (var p in payments)
                Add(BankMatchDocType.CustomerPayment, p.PaymentId, p.PaymentNumber, p.FullName, p.CustomerId, Shared.IstClock.IstDateOf(p.When), p.Amount, p.Amount, true,
                    p.GatewayPaymentId, $"{p.PaymentMethod}{(p.Gateway != null ? " · " + p.Gateway : "")}");
        }
        else
        {
            var bills = await (from b in db.Set<PurchaseBill>().AsNoTracking()
                               join sp in db.Suppliers on b.SupplierId equals sp.SupplierId
                               where (b.Status == PurchaseBillStatus.Approved || b.Status == PurchaseBillStatus.PartPaid)
                                     && (s != null ? (b.SupplierBillNo.Contains(s) || b.BillNumber.Contains(s) || sp.SupplierName.Contains(s)) : b.BillDate <= winTo)
                               select new
                               {
                                   b.PurchaseBillId, b.SupplierBillNo, sp.SupplierName, b.SupplierId, b.BillDate, b.BillTotal, b.TdsAmount,
                                   Paid = PurchaseBillService.LiveAllocations(db).Where(a => a.PurchaseBillId == b.PurchaseBillId).Sum(a => (decimal?)a.Amount) ?? 0m
                               }).Take(300).ToListAsync(ct);
            foreach (var b in bills.Where(b => b.BillTotal - b.Paid > 0m))
                Add(BankMatchDocType.PurchaseBill, b.PurchaseBillId, b.SupplierBillNo, b.SupplierName, b.SupplierId, b.BillDate, b.BillTotal, b.BillTotal - b.Paid, false, null,
                    b.TdsAmount > 0m ? $"TDS {FinanceRules.Money(b.TdsAmount)} expected" : null);
            var vps = await (from v in db.Set<VendorPayment>().AsNoTracking()
                             join sp in db.Suppliers on v.SupplierId equals sp.SupplierId
                             where !v.IsReversed && !v.FromPettyCash
                                   && !db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.VendorPayment && m.DocId == v.VendorPaymentId)
                                   && !db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.PurchaseBill && m.CreatedRefs == "VP:" + v.VendorPaymentId)
                                   && (s != null ? (v.PaymentNumber.Contains(s) || sp.SupplierName.Contains(s) || (v.Reference != null && v.Reference.Contains(s))) : v.PaidOn >= winFrom && v.PaidOn <= winTo)
                             select new { v.VendorPaymentId, v.PaymentNumber, sp.SupplierName, v.SupplierId, v.PaidOn, v.Amount, v.Reference }).Take(300).ToListAsync(ct);
            foreach (var v in vps) Add(BankMatchDocType.VendorPayment, v.VendorPaymentId, v.PaymentNumber, v.SupplierName, v.SupplierId, v.PaidOn, v.Amount, v.Amount, true, v.Reference, v.Reference);
            var expenses = await (from x in db.Expenses.AsNoTracking()
                                  join c in db.Set<ExpenseControl>() on x.ExpenseId equals c.ExpenseId into cj
                                  from c in cj.DefaultIfEmpty()
                                  join sp in db.Suppliers on x.SupplierId equals (int?)sp.SupplierId into sj
                                  from sp in sj.DefaultIfEmpty()
                                  where (c == null || (c.ApprovalStatus == ExpenseApprovalStatus.Approved && c.PaidFrom != ExpensePaidFrom.PettyCash && !c.IsRecurringDraft))
                                        && !db.BankTransactions.Any(t => t.MatchedExpenseId == x.ExpenseId)
                                        && !db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.Expense && m.DocId == x.ExpenseId)
                                        && (s != null ? (x.ExpenseNumber.Contains(s) || x.Description.Contains(s) || x.Category.Contains(s)) : x.ExpenseDate >= winFrom && x.ExpenseDate <= winTo)
                                  select new { x.ExpenseId, x.ExpenseNumber, Party = sp != null ? sp.SupplierName : x.Category, x.ExpenseDate, Net = x.Amount + x.AdjustmentTotal, x.Description })
                .Take(300).ToListAsync(ct);
            foreach (var x in expenses.Where(x => x.Net > 0m)) Add(BankMatchDocType.Expense, x.ExpenseId, x.ExpenseNumber, x.Party, null, x.ExpenseDate, x.Net, x.Net, false, null, x.Description);
            var topups = await db.Set<PettyCashEntry>().AsNoTracking()
                .Where(e => e.EntryType == PettyCashEntryType.TopUp && !db.Set<BankMatchAllocation>().Any(m => m.DocType == BankMatchDocType.PettyCashTopUp && m.DocId == e.PettyCashEntryId))
                .Where(e => e.HappenedOn >= winFrom && e.HappenedOn <= winTo).Take(100).ToListAsync(ct);
            foreach (var e in topups) Add(BankMatchDocType.PettyCashTopUp, e.PettyCashEntryId, $"PC-{e.PettyCashEntryId}", "Petty cash", null, e.HappenedOn, e.Amount, e.Amount, true, null, e.Description);
        }
        var ranked = rows.OrderByDescending(r => r.Score).ThenBy(r => Math.Abs(r.Outstanding - line.Amount)).ThenBy(r => Math.Abs(r.Date.DayNumber - line.TransactionDate.DayNumber)).Take(60).ToList();
        // Suggest, never decide: pre-tick only a single, exact, well-scored candidate.
        var exact = ranked.Where(r => r.Outstanding == line.Amount && r.Score >= 70).ToList();
        if (exact.Count == 1)
        {
            int i = ranked.IndexOf(exact[0]);
            ranked[i] = exact[0] with { Suggested = true };
        }
        return ranked;
    }

    public async Task<BankMatchContextDto> GetContextAsync(long bankTransactionId, string? search, CancellationToken ct = default)
    {
        BankTransaction line = await db.BankTransactions.AsNoTracking().FirstOrDefaultAsync(t => t.BankTransactionId == bankTransactionId, ct)
            ?? throw new NotFoundException("Bank line", bankTransactionId);
        var candidates = line.ReconciliationStatus == ReconciliationStatus.Reconciled ? new List<MatchCandidateRowDto>() : await CandidatesAsync(line, search, ct);
        var labels = await db.Set<DifferenceReason>().AsNoTracking().Where(r => r.IsActive).ToDictionaryAsync(r => r.DifferenceReasonId, ct);
        var reasons = ReasonInfo.Where(r => Allowed(r.Key, line.Direction) && (labels.Count == 0 || labels.ContainsKey((byte)r.Key)))
            .OrderBy(r => labels.TryGetValue((byte)r.Key, out var l) ? l.DisplayOrder : (int)r.Key)
            .Select(r =>
            {
                labels.TryGetValue((byte)r.Key, out DifferenceReason? row);
                string? perm = row?.RequiredPermission ?? r.Value.Permission;
                return new DifferenceReasonDto((byte)r.Key, r.Value.Code, row?.Label ?? r.Value.Label, row?.Help ?? r.Value.Help,
                    perm == null || currentUser.HasPermission(perm), perm);
            }).ToList();
        return new BankMatchContextDto(await LineAsync(bankTransactionId, ct), candidates, reasons,
            await settings.GetDecimalAsync("Bank.RoundingCap", 10m, ct), await PurchaseBillService.TdsSectionsAsync(db, line.TransactionDate, ct));
    }

    // ── Match ────────────────────────────────────────────────────────────────────

    public async Task<BankLineDto> MatchAsync(long bankTransactionId, MatchBankLineRequest request, CancellationToken ct = default)
    {
        var allocations = (request.Allocations ?? new List<MatchAllocationRequest>()).ToList();
        var differences = (request.Differences ?? new List<MatchDifferenceRequest>()).ToList();
        if (allocations.Count == 0 && differences.Count == 0) throw new BusinessRuleException("Tick what this money is for.");
        if (allocations.Any(a => a.Amount <= 0m)) throw new BusinessRuleException("Every ticked document needs an amount above zero.");
        if (allocations.GroupBy(a => (a.DocType, a.DocId)).Any(g => g.Count() > 1)) throw new BusinessRuleException("A document can be ticked once per line.");
        if (differences.Any(d => !Enum.IsDefined(typeof(BankDifferenceReason), d.Reason))) throw new BusinessRuleException("Choose what the difference is.");
        if (differences.Any(d => d.Amount == 0m)) throw new BusinessRuleException("Enter the amount of each difference.");
        decimal cap = await settings.GetDecimalAsync("Bank.RoundingCap", 10m, ct);

        await FinanceRules.InTransactionAsync(db, async () =>
        {
            DateTime nowUtc = clock.UtcNow;
            BankTransaction line = await db.BankTransactions.FirstOrDefaultAsync(t => t.BankTransactionId == bankTransactionId, ct)
                ?? throw new NotFoundException("Bank line", bankTransactionId);
            if (line.ReconciliationStatus != ReconciliationStatus.Unreconciled || line.MatchedPaymentId != null || line.MatchedExpenseId != null
                || await db.Set<BankMatchAllocation>().AnyAsync(m => m.BankTransactionId == line.BankTransactionId, ct))
                throw new BusinessRuleException("This line is already matched. Unmatch it first.");
            await FinanceRules.EnsurePeriodOpenAsync(db, line.TransactionDate, ct, "this match");
            bool credit = line.Direction == BankTransactionDirection.Credit;
            var creditTypes = new[] { BankMatchDocType.Invoice, BankMatchDocType.CustomerPayment };
            if (allocations.Any(a => creditTypes.Contains((BankMatchDocType)a.DocType) != credit))
                throw new BusinessRuleException(credit ? "Money in can only be matched to invoices and customer payments." : "Money out can only be matched to bills, vendor payments, expenses and petty cash top-ups.");

            // The arithmetic first: applied + explained differences = the bank line, to the rupee.
            decimal applied = allocations.Sum(a => FinanceRules.Round(a.Amount));
            foreach (MatchDifferenceRequest d in differences)
            {
                var reason = (BankDifferenceReason)d.Reason;
                if (!Allowed(reason, line.Direction)) throw new BusinessRuleException($"{ReasonInfo[reason].Label} does not apply to money {(credit ? "in" : "out")}.");
                if (reason == BankDifferenceReason.Rounding && Math.Abs(d.Amount) > cap)
                    throw new BusinessRuleException($"{FinanceRules.Money(Math.Abs(d.Amount))} is more than the {FinanceRules.Money(cap)} rounding cap. Choose a different reason.");
                if (reason == BankDifferenceReason.ShortPaymentAccepted && !currentUser.HasPermission("BANK.WRITEOFF"))
                    throw new ForbiddenException("Accepting a short payment needs the write-off permission. Ask the office manager.");
            }
            decimal explained = differences.Sum(d => Signed((BankDifferenceReason)d.Reason, line.Direction, FinanceRules.Round(d.Amount)));
            decimal gap = line.Amount - (applied + explained);
            if (gap != 0m)
                throw new BusinessRuleException($"{FinanceRules.Money(Math.Abs(gap))} is still unexplained. Say what it is before reconciling.", "MATCH_UNBALANCED");

            // Non-cash parts of a document: TDS and short payments belong to one ticked document.
            var tdsBy = new Dictionary<(BankMatchDocType, long), decimal>();
            var shortBy = new Dictionary<(BankMatchDocType, long), decimal>();
            foreach (MatchDifferenceRequest d in differences.Where(d => d.Reason is (byte)BankDifferenceReason.TdsDeducted or (byte)BankDifferenceReason.ShortPaymentAccepted))
            {
                var target = allocations.FirstOrDefault(a => a.DocType == d.TargetDocType && a.DocId == d.TargetDocId)
                             ?? (allocations.Count(a => a.DocType is (byte)BankMatchDocType.Invoice or (byte)BankMatchDocType.PurchaseBill) == 1
                                 ? allocations.First(a => a.DocType is (byte)BankMatchDocType.Invoice or (byte)BankMatchDocType.PurchaseBill) : null);
                if (target == null || target.DocType is not ((byte)BankMatchDocType.Invoice or (byte)BankMatchDocType.PurchaseBill))
                    throw new BusinessRuleException("Say which invoice or bill the TDS or short payment belongs to.");
                if (d.Reason == (byte)BankDifferenceReason.TdsDeducted && string.IsNullOrWhiteSpace(d.TdsSection))
                    throw new BusinessRuleException("Choose the TDS section.");
                var key = ((BankMatchDocType)target.DocType, target.DocId);
                var dict = d.Reason == (byte)BankDifferenceReason.TdsDeducted ? tdsBy : shortBy;
                dict[key] = dict.GetValueOrDefault(key) + Math.Abs(FinanceRules.Round(d.Amount));
            }

            string bankRef = line.BankReferenceId ?? line.ReferenceNumber ?? $"BANK-{line.BankTransactionId}";
            if (bankRef.Length > 60) bankRef = bankRef[..60];
            var created = new List<BankMatchAllocation>();
            long? firstCustomer = null;
            int? firstSupplier = null;

            // Purchase bills: one vendor payment per supplier, less any TDS explained on them.
            var billAllocs = allocations.Where(a => a.DocType == (byte)BankMatchDocType.PurchaseBill).ToList();
            if (billAllocs.Count > 0)
            {
                var bills = await db.Set<PurchaseBill>().AsNoTracking().Where(b => billAllocs.Select(a => a.DocId).Contains(b.PurchaseBillId))
                    .Select(b => new { b.PurchaseBillId, b.SupplierId, b.SupplierBillNo }).ToListAsync(ct);
                if (bills.Count != billAllocs.Count) throw new BusinessRuleException("One of the ticked bills no longer exists.");
                foreach (var group in bills.GroupBy(b => b.SupplierId))
                {
                    firstSupplier ??= group.Key;
                    var reqAllocs = group.Select(b =>
                    {
                        decimal apply = FinanceRules.Round(billAllocs.First(a => a.DocId == b.PurchaseBillId).Amount);
                        decimal tds = tdsBy.GetValueOrDefault((BankMatchDocType.PurchaseBill, b.PurchaseBillId));
                        if (tds > apply) throw new BusinessRuleException($"The TDS on {b.SupplierBillNo} is more than the amount applied to it.");
                        return new VendorAllocationRequest(b.PurchaseBillId, apply - tds, tds);
                    }).ToList();
                    VendorPayment vp = await VendorPaymentService.PostAsync(db, sequences, currentUser, clock,
                        new CreateVendorPaymentRequest(group.Key, line.TransactionDate, line.BankAccountId, false, (byte)VendorPaymentMode.Neft, bankRef,
                            $"From bank line of {line.TransactionDate:dd MMM yyyy}", reqAllocs.Sum(a => a.Amount), 0m, reqAllocs), null, ct);
                    foreach (var b in group)
                    {
                        created.Add(new BankMatchAllocation
                        {
                            BankTransactionId = line.BankTransactionId, DocType = BankMatchDocType.PurchaseBill, DocId = b.PurchaseBillId,
                            DocNumber = b.SupplierBillNo, Amount = FinanceRules.Round(billAllocs.First(a => a.DocId == b.PurchaseBillId).Amount),
                            CreatedRefs = $"VP:{vp.VendorPaymentId}"
                        });
                    }
                }
            }

            foreach (MatchAllocationRequest a in allocations.Where(a => a.DocType != (byte)BankMatchDocType.PurchaseBill))
            {
                decimal apply = FinanceRules.Round(a.Amount);
                var type = (BankMatchDocType)a.DocType;
                var alloc = new BankMatchAllocation { BankTransactionId = line.BankTransactionId, DocType = type, DocId = a.DocId, Amount = apply };
                switch (type)
                {
                    case BankMatchDocType.Invoice:
                    {
                        Invoice invoice = await db.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == a.DocId, ct) ?? throw new NotFoundException("Invoice", a.DocId);
                        if (invoice.Status is InvoiceStatus.Cancelled or InvoiceStatus.Refunded) throw new BusinessRuleException($"{invoice.InvoiceNumber} is cancelled.");
                        decimal outstanding = invoice.TotalAmount - invoice.PaidAmount;
                        if (apply > outstanding) throw new BusinessRuleException($"{FinanceRules.Money(outstanding)} is the most that can be applied to {invoice.InvoiceNumber}.");
                        decimal tds = tdsBy.GetValueOrDefault((type, a.DocId));
                        decimal shortPaid = shortBy.GetValueOrDefault((type, a.DocId));
                        decimal cash = apply - tds - shortPaid;
                        if (cash < 0m) throw new BusinessRuleException($"The TDS and short payment on {invoice.InvoiceNumber} are more than the amount applied to it.");
                        firstCustomer ??= invoice.CustomerId;
                        var refs = new List<string>();
                        if (cash > 0m) refs.Add("IP:" + await ReceiptAsync(invoice, cash, InvoicePaymentMode.BankTransfer, bankRef, line, nowUtc, ct));
                        if (tds > 0m) refs.Add("IP:" + await ReceiptAsync(invoice, tds, InvoicePaymentMode.TdsCredit, null, line, nowUtc, ct));
                        if (shortPaid > 0m) refs.Add("IP:" + await ReceiptAsync(invoice, shortPaid, InvoicePaymentMode.WriteOff, null, line, nowUtc, ct));
                        await CustomerPaymentService.ApplyAsync(db, invoice, apply, nowUtc, ct);
                        alloc.DocNumber = invoice.InvoiceNumber;
                        alloc.CreatedRefs = string.Join(",", refs);
                        break;
                    }
                    case BankMatchDocType.CustomerPayment:
                    {
                        Payment p = await db.Payments.FirstOrDefaultAsync(x => x.PaymentId == a.DocId && x.Status == PaymentStatus.Success, ct)
                            ?? throw new BusinessRuleException("That payment is not a successful payment.");
                        if (apply != p.Amount) throw new BusinessRuleException($"{p.PaymentNumber} is {FinanceRules.Money(p.Amount)}; a payment is matched in full.");
                        if (await db.BankTransactions.AnyAsync(t => t.MatchedPaymentId == p.PaymentId, ct)
                            || await db.Set<BankMatchAllocation>().AnyAsync(m => m.DocType == type && m.DocId == p.PaymentId, ct))
                            throw new BusinessRuleException($"{p.PaymentNumber} is already matched to another bank line.");
                        firstCustomer ??= p.CustomerId;
                        alloc.DocNumber = p.PaymentNumber;
                        if (line.MatchedPaymentId == null && line.MatchedExpenseId == null) line.MatchedPaymentId = p.PaymentId;
                        break;
                    }
                    case BankMatchDocType.VendorPayment:
                    {
                        VendorPayment v = await db.Set<VendorPayment>().FirstOrDefaultAsync(x => x.VendorPaymentId == a.DocId && !x.IsReversed && !x.FromPettyCash, ct)
                            ?? throw new BusinessRuleException("That vendor payment cannot be matched.");
                        if (apply != v.Amount) throw new BusinessRuleException($"{v.PaymentNumber} is {FinanceRules.Money(v.Amount)}; a payment is matched in full.");
                        if (await db.Set<BankMatchAllocation>().AnyAsync(m => m.DocType == type && m.DocId == v.VendorPaymentId, ct))
                            throw new BusinessRuleException($"{v.PaymentNumber} is already matched to another bank line.");
                        firstSupplier ??= v.SupplierId;
                        alloc.DocNumber = v.PaymentNumber;
                        break;
                    }
                    case BankMatchDocType.Expense:
                    {
                        Expense x = await db.Expenses.FirstOrDefaultAsync(e => e.ExpenseId == a.DocId, ct) ?? throw new NotFoundException("Expense", a.DocId);
                        ExpenseControl? c = await db.Set<ExpenseControl>().AsNoTracking().FirstOrDefaultAsync(e => e.ExpenseId == x.ExpenseId, ct);
                        if (c != null && (c.ApprovalStatus != ExpenseApprovalStatus.Approved || c.IsRecurringDraft))
                            throw new BusinessRuleException($"{x.ExpenseNumber} is not approved yet, so it cannot be matched.");
                        if (c?.PaidFrom == ExpensePaidFrom.PettyCash) throw new BusinessRuleException($"{x.ExpenseNumber} was paid from petty cash, not the bank.");
                        if (apply > x.Amount + x.AdjustmentTotal) throw new BusinessRuleException($"{x.ExpenseNumber} is only {FinanceRules.Money(x.Amount + x.AdjustmentTotal)}.");
                        if (await db.BankTransactions.AnyAsync(t => t.MatchedExpenseId == x.ExpenseId, ct)
                            || await db.Set<BankMatchAllocation>().AnyAsync(m => m.DocType == type && m.DocId == x.ExpenseId, ct))
                            throw new BusinessRuleException($"{x.ExpenseNumber} is already matched to another bank line.");
                        alloc.DocNumber = x.ExpenseNumber;
                        if (line.MatchedPaymentId == null && line.MatchedExpenseId == null) line.MatchedExpenseId = x.ExpenseId;
                        break;
                    }
                    case BankMatchDocType.PettyCashTopUp:
                    {
                        PettyCashEntry e = await db.Set<PettyCashEntry>().FirstOrDefaultAsync(x => x.PettyCashEntryId == a.DocId && x.EntryType == PettyCashEntryType.TopUp, ct)
                            ?? throw new BusinessRuleException("That petty cash top-up does not exist.");
                        if (apply != e.Amount) throw new BusinessRuleException($"The top-up was {FinanceRules.Money(e.Amount)}; it is matched in full.");
                        if (await db.Set<BankMatchAllocation>().AnyAsync(m => m.DocType == type && m.DocId == e.PettyCashEntryId, ct))
                            throw new BusinessRuleException("That top-up is already matched to another bank line.");
                        alloc.DocNumber = $"PC-{e.PettyCashEntryId}";
                        break;
                    }
                    default:
                        throw new BusinessRuleException("Unknown document type.");
                }
                created.Add(alloc);
            }
            db.Set<BankMatchAllocation>().AddRange(created);

            foreach (MatchDifferenceRequest d in differences)
            {
                var reason = (BankDifferenceReason)d.Reason;
                decimal amount = Math.Abs(FinanceRules.Round(d.Amount));
                var row = new BankMatchDifference
                {
                    BankTransactionId = line.BankTransactionId,
                    Reason = reason,
                    SignedAmount = Signed(reason, line.Direction, FinanceRules.Round(d.Amount)),
                    TdsSection = d.TdsSection?.Trim().ToUpperInvariant(),
                    Note = string.IsNullOrWhiteSpace(d.Note) ? null : d.Note.Trim(),
                    TargetDocType = (BankMatchDocType?)d.TargetDocType,
                    TargetDocId = d.TargetDocId
                };
                switch (reason)
                {
                    case BankDifferenceReason.TdsDeducted when credit:
                    {
                        var target = tdsBy.Keys.First();
                        if (d.TargetDocId != null) target = (BankMatchDocType.Invoice, d.TargetDocId.Value);
                        Invoice inv = await db.Invoices.AsNoTracking().FirstAsync(i => i.InvoiceId == target.Item2, ct);
                        string? tan = await db.Set<CustomerTaxProfile>().Where(p => p.CustomerId == inv.CustomerId).Select(p => p.Tan).FirstOrDefaultAsync(ct);
                        decimal rate = await PurchaseBillService.TdsRateAsync(db, row.TdsSection!, line.TransactionDate, false, true, ct);
                        var entry = new TdsEntry
                        {
                            Direction = TdsDirection.Receivable, PartyType = "Customer", PartyId = inv.CustomerId, PartyName = inv.CustomerName, PartyTaxId = tan,
                            Section = row.TdsSection!, Rate = rate, BaseAmount = rate > 0m ? FinanceRules.Round(amount * 100m / rate) : inv.TaxableAmount,
                            TdsAmount = amount, DeductedOn = line.TransactionDate, Quarter = FinanceRules.Quarter(line.TransactionDate),
                            FinancialYear = FinanceRules.FinancialYear(line.TransactionDate), SourceType = "BankMatch", SourceId = line.BankTransactionId,
                            DocumentNumber = inv.InvoiceNumber
                        };
                        db.Set<TdsEntry>().Add(entry);
                        await db.SaveChangesAsync(ct);
                        row.CreatedTdsEntryId = entry.TdsEntryId;
                        row.TargetDocType = BankMatchDocType.Invoice;
                        row.TargetDocId = inv.InvoiceId;
                        break;
                    }
                    case BankDifferenceReason.BankCharge:
                        row.CreatedExpenseId = await BankChargeExpenseAsync(line, amount, row.Note, ct);
                        break;
                    case BankDifferenceReason.AdvanceReceived when credit:
                    {
                        long customer = d.PartyId ?? firstCustomer ?? throw new BusinessRuleException("Say which customer the extra money belongs to.");
                        await wallets.CreditAsync(customer, amount, WalletCategory.Recharge,
                            $"Paid in advance by bank transfer ({line.TransactionDate:dd MMM yyyy}, {bankRef})", "BankTransaction", line.BankTransactionId, null, ct);
                        await db.SaveChangesAsync(ct);
                        row.CreatedWalletTransactionId = await db.WalletTransactions.Where(w => w.CustomerId == customer && w.ReferenceType == "BankTransaction" && w.ReferenceId == line.BankTransactionId)
                            .OrderByDescending(w => w.WalletTransactionId).Select(w => (long?)w.WalletTransactionId).FirstOrDefaultAsync(ct);
                        break;
                    }
                    case BankDifferenceReason.AdvanceReceived:
                    {
                        int supplier = (int?)d.PartyId ?? firstSupplier ?? throw new BusinessRuleException("Say which supplier the advance was paid to.");
                        VendorPayment vp = await VendorPaymentService.PostAsync(db, sequences, currentUser, clock,
                            new CreateVendorPaymentRequest(supplier, line.TransactionDate, line.BankAccountId, false, (byte)VendorPaymentMode.Neft, bankRef,
                                "Advance, from a bank line", amount, 0m, new List<VendorAllocationRequest>()), null, ct);
                        row.CreatedVendorPaymentId = vp.VendorPaymentId;
                        break;
                    }
                }
                db.Set<BankMatchDifference>().Add(row);
            }

            line.ReconciliationStatus = ReconciliationStatus.Reconciled;
            line.MatchedUtc = nowUtc;
            line.MatchedByUserId = currentUser.UserId;
            line.ReconciledUtc = nowUtc;
            line.ReconciledByUserId = currentUser.UserId;
            await db.SaveChangesAsync(ct);
            return true;
        }, ct);
        return await LineAsync(bankTransactionId, ct);
    }

    private async Task<long> ReceiptAsync(Invoice invoice, decimal amount, InvoicePaymentMode mode, string? reference, BankTransaction line, DateTime nowUtc, CancellationToken ct)
    {
        long? paymentId = null;
        if (mode == InvoicePaymentMode.BankTransfer)
        {
            var payment = new Payment
            {
                PaymentNumber = await sequences.NextAsync(SequenceKeys.Payment, ct),
                CustomerId = invoice.CustomerId,
                BookingId = invoice.BookingId,
                CustomerPassId = invoice.CustomerPassId,
                InvoiceId = invoice.InvoiceId,
                Amount = amount,
                PaymentMethod = PaymentMethod.NetBanking,
                Gateway = "Offline",
                Status = PaymentStatus.Success,
                PaidUtc = Shared.IstClock.ToUtc(line.TransactionDate.ToDateTime(new TimeOnly(12, 0)))
            };
            db.Payments.Add(payment);
            await db.SaveChangesAsync(ct);
            paymentId = payment.PaymentId;
            invoice.PaymentId ??= payment.PaymentId;
        }
        var receipt = new InvoicePayment
        {
            ReceiptNumber = await sequences.NextAsync(CustomerPaymentService.ReceiptSequence, ct),
            InvoiceId = invoice.InvoiceId,
            CustomerId = invoice.CustomerId,
            PaymentId = paymentId,
            Amount = amount,
            PaidOn = line.TransactionDate,
            Mode = mode,
            Reference = reference,
            BankAccountId = line.BankAccountId,
            Note = mode == InvoicePaymentMode.BankTransfer ? "Matched from the bank statement" : $"Explained difference on the bank line of {line.TransactionDate:dd MMM yyyy}",
            BankTransactionId = line.BankTransactionId,
            RecordedByUserId = currentUser.UserId,
            RecordedUtc = nowUtc
        };
        db.Set<InvoicePayment>().Add(receipt);
        await db.SaveChangesAsync(ct);
        return receipt.InvoicePaymentId;
    }

    private async Task<long> BankChargeExpenseAsync(BankTransaction line, decimal amount, string? note, CancellationToken ct)
    {
        string category = await settings.GetStringAsync("Bank.ChargeCategory", "Bank charges", ct);
        var expense = new Expense
        {
            ExpenseNumber = await sequences.NextAsync(SequenceKeys.Expense, ct),
            ExpenseDate = line.TransactionDate,
            Category = category,
            Description = note ?? $"Bank charge on: {(line.Narration.Length > 200 ? line.Narration[..200] : line.Narration)}",
            Amount = amount,
            PaymentMethod = PaymentMethod.NetBanking,
            Remarks = "Explained difference during bank reconciliation.",
            LocationId = line.LocationId,
            EnteredByUserId = currentUser.UserId
        };
        db.Expenses.Add(expense);
        await db.SaveChangesAsync(ct);
        ExpenseControl control = await db.Set<ExpenseControl>().FirstOrDefaultAsync(c => c.ExpenseId == expense.ExpenseId, ct)
            ?? db.Set<ExpenseControl>().Add(new ExpenseControl { ExpenseId = expense.ExpenseId }).Entity;
        control.ApprovalStatus = ExpenseApprovalStatus.Approved;
        control.DecisionComment = "Bank charge taken by the bank; recorded during reconciliation.";
        control.DecidedByUserId = currentUser.UserId;
        control.DecidedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return expense.ExpenseId;
    }

    // ── Unmatch ──────────────────────────────────────────────────────────────────

    public async Task<BankLineDto> UnmatchAsync(long bankTransactionId, UnmatchBankLineRequest request, CancellationToken ct = default)
    {
        string reason = request.Reason?.Trim() ?? "";
        if (reason.Length < 3) throw new BusinessRuleException("Say why this line is being unmatched.");
        await FinanceRules.InTransactionAsync(db, async () =>
        {
            using (BillingFinanceScope.Unmatch())
            {
                DateTime nowUtc = clock.UtcNow;
                BankTransaction line = await db.BankTransactions.FirstOrDefaultAsync(t => t.BankTransactionId == bankTransactionId, ct)
                    ?? throw new NotFoundException("Bank line", bankTransactionId);
                await FinanceRules.EnsurePeriodOpenAsync(db, line.TransactionDate, ct, "this unmatch");
                var allocs = await db.Set<BankMatchAllocation>().Where(m => m.BankTransactionId == bankTransactionId).ToListAsync(ct);
                var diffs = await db.Set<BankMatchDifference>().Where(d => d.BankTransactionId == bankTransactionId).ToListAsync(ct);
                if (allocs.Count == 0 && diffs.Count == 0 && line.ReconciliationStatus == ReconciliationStatus.Unreconciled && line.MatchedPaymentId == null && line.MatchedExpenseId == null)
                    throw new BusinessRuleException("This line is not matched.");

                foreach (BankMatchAllocation a in allocs) { a.UnmatchReason = reason; db.Remove(a); }
                foreach (BankMatchDifference d in diffs) { d.Note = $"{d.Note} · unmatched: {reason}".Trim(' ', '·'); db.Remove(d); }
                await db.SaveChangesAsync(ct);

                // Undo what the match created, in reverse: receipts, vendor payments, expenses, TDS, wallet.
                var receipts = await db.Set<InvoicePayment>().Where(p => p.BankTransactionId == bankTransactionId && p.ReversalOfId == null && !p.IsReversed).ToListAsync(ct);
                foreach (InvoicePayment r in receipts)
                {
                    Invoice invoice = await db.Invoices.FirstAsync(i => i.InvoiceId == r.InvoiceId, ct);
                    db.Set<InvoicePayment>().Add(new InvoicePayment
                    {
                        ReceiptNumber = await sequences.NextAsync(CustomerPaymentService.ReceiptSequence, ct),
                        InvoiceId = r.InvoiceId, CustomerId = r.CustomerId, Amount = -r.Amount, PaidOn = FinanceRules.Today(clock), Mode = r.Mode,
                        Reference = $"Reversal of {r.ReceiptNumber}", BankAccountId = r.BankAccountId, Note = $"Bank line unmatched: {reason}",
                        ReversalOfId = r.InvoicePaymentId, RecordedByUserId = currentUser.UserId, RecordedUtc = nowUtc
                    });
                    r.IsReversed = true;
                    r.ReversedByUserId = currentUser.UserId;
                    r.ReversedUtc = nowUtc;
                    r.ReverseReason = $"Bank line unmatched: {reason}";
                    if (r.PaymentId != null)
                    {
                        Payment p = await db.Payments.FirstAsync(x => x.PaymentId == r.PaymentId, ct);
                        p.Status = PaymentStatus.Refunded;
                        p.RefundedAmount = p.Amount;
                        p.RefundReference = $"Bank line unmatched: {reason}";
                        if (invoice.PaymentId == p.PaymentId) invoice.PaymentId = null;
                    }
                    await CustomerPaymentService.ApplyAsync(db, invoice, -r.Amount, nowUtc, ct);
                }
                foreach (string vp in allocs.Where(a => a.CreatedRefs?.StartsWith("VP:") == true).Select(a => a.CreatedRefs!).Distinct())
                {
                    long id = long.Parse(vp[3..]);
                    if (!await db.Set<VendorPayment>().AnyAsync(v => v.VendorPaymentId == id && v.IsReversed, ct))
                        await VendorPaymentService.ReverseInternalAsync(db, currentUser, clock, id, $"Bank line unmatched: {reason}", true, ct);
                }
                foreach (BankMatchDifference d in diffs)
                {
                    if (d.CreatedVendorPaymentId != null)
                        await VendorPaymentService.ReverseInternalAsync(db, currentUser, clock, d.CreatedVendorPaymentId.Value, $"Bank line unmatched: {reason}", true, ct);
                    if (d.CreatedExpenseId != null)
                    {
                        Expense? x = await db.Expenses.FirstOrDefaultAsync(e => e.ExpenseId == d.CreatedExpenseId, ct);
                        if (x != null) db.Expenses.Remove(x);
                    }
                    if (d.CreatedTdsEntryId != null)
                    {
                        TdsEntry? t = await db.Set<TdsEntry>().FirstOrDefaultAsync(e => e.TdsEntryId == d.CreatedTdsEntryId, ct);
                        if (t?.CertificateNo != null) throw new BusinessRuleException($"The TDS on this line already has certificate {t.CertificateNo}. Remove the certificate first.");
                        if (t != null) db.Remove(t);
                    }
                    if (d.CreatedWalletTransactionId != null)
                    {
                        WalletTransaction w = await db.WalletTransactions.AsNoTracking().FirstAsync(x => x.WalletTransactionId == d.CreatedWalletTransactionId, ct);
                        await wallets.DebitAsync(w.CustomerId, w.Amount, WalletCategory.AdminAdjustment, $"Bank line unmatched: {reason}", "BankTransaction", line.BankTransactionId, null, ct);
                    }
                }

                line.ReconciliationStatus = ReconciliationStatus.Unreconciled;
                line.MatchedPaymentId = null;
                line.MatchedExpenseId = null;
                line.MatchedUtc = null;
                line.MatchedByUserId = null;
                line.ReconciledUtc = null;
                line.ReconciledByUserId = null;
                string remark = $"Unmatched {Shared.IstClock.ToIst(nowUtc):dd MMM yyyy HH:mm} by {currentUser.FullName ?? "#" + currentUser.UserId}: {reason}";
                line.Remarks = string.IsNullOrWhiteSpace(line.Remarks) ? remark : $"{line.Remarks} · {remark}";
                if (line.Remarks.Length > 500) line.Remarks = line.Remarks[^500..];
                await db.SaveChangesAsync(ct);
            }
            return true;
        }, ct);
        return await LineAsync(bankTransactionId, ct);
    }
}
