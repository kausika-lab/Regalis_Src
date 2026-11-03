using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Finance.Billing;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Enums;

namespace Regalis.Infrastructure.Persistence.Interceptors;

/// <summary>Lets the bank-matching and payment-reversal code do what the rules below
/// otherwise refuse (unreconcile a line, void a cash receipt written by Record payment).</summary>
public static class BillingFinanceScope
{
    private static readonly AsyncLocal<int> Unmatching = new();
    private static readonly AsyncLocal<int> Reversing = new();

    public static bool IsUnmatching => Unmatching.Value > 0;
    public static bool IsReversing => Reversing.Value > 0;

    public static IDisposable Unmatch() => new Flag(Unmatching);
    public static IDisposable Reverse() => new Flag(Reversing);

    private sealed class Flag : IDisposable
    {
        private readonly AsyncLocal<int> _flag;
        public Flag(AsyncLocal<int> flag) { _flag = flag; _flag.Value++; }
        public void Dispose() => _flag.Value--;
    }
}

/// <summary>
/// The Billing &amp; Finance rules that must hold on every write path of the existing app, not
/// only on the new screens (build doc §9.3, §12.2, §13.3):
/// - nothing dated in a locked month is created, edited, cancelled or deleted (invoices,
///   expenses, expense adjustments, bank lines);
/// - an expense matched to a reconciled bank line is frozen; a line reconciled on the new
///   matching screen is unmatched only there; an unapproved expense cannot be matched;
/// - a cash receipt written by Record payment is voided only by reversing the payment;
/// - every new expense gets its approval row, and an approved expense whose amount grows past
///   its approved level goes back for approval.
/// An interceptor rather than database triggers: EF Core cannot save to a table with triggers
/// unless every existing configuration declares them.
/// </summary>
public class BillingFinanceRulesInterceptor : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, List<Expense>> _newExpenses = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context != null) CheckAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null) await CheckAsync(eventData.Context, cancellationToken);
        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context != null) AddControlsAsync(eventData.Context, CancellationToken.None).GetAwaiter().GetResult();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null) await AddControlsAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context != null) _newExpenses.Remove(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null) _newExpenses.Remove(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static bool Changed(EntityEntry e, string property) =>
        e.State == EntityState.Modified && e.Property(property).IsModified && !Equals(e.Property(property).OriginalValue, e.Property(property).CurrentValue);

    private static bool Deleting(EntityEntry e) =>
        e.State == EntityState.Deleted || (e.State == EntityState.Modified && e.Property("IsDeleted").IsModified && (bool)e.Property("IsDeleted").CurrentValue! && !(bool)e.Property("IsDeleted").OriginalValue!);

    private async Task CheckAsync(DbContext db, CancellationToken ct)
    {
        db.ChangeTracker.DetectChanges();
        var entries = db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToList();
        if (entries.Count == 0) return;

        // ── Period lock ──────────────────────────────────────────────────────────────
        var dates = new List<(DateOnly Date, string What)>();
        foreach (EntityEntry e in entries)
        {
            switch (e.Entity)
            {
                case Invoice i when e.State == EntityState.Added
                                    || Changed(e, nameof(Invoice.TotalAmount)) || Changed(e, nameof(Invoice.TaxableAmount))
                                    || Changed(e, nameof(Invoice.InvoiceDate))
                                    || (Changed(e, nameof(Invoice.Status)) && i.Status == InvoiceStatus.Cancelled):
                    dates.Add((i.InvoiceDate, "this invoice"));
                    if (e.State == EntityState.Modified) dates.Add(((DateOnly)e.Property(nameof(Invoice.InvoiceDate)).OriginalValue!, "this invoice"));
                    break;
                case Expense x when e.State == EntityState.Added || Deleting(e)
                                    || Changed(e, nameof(Expense.Amount)) || Changed(e, nameof(Expense.AdjustmentTotal))
                                    || Changed(e, nameof(Expense.ExpenseDate)) || Changed(e, nameof(Expense.Category))
                                    || Changed(e, nameof(Expense.PaymentMethod)):
                    dates.Add((x.ExpenseDate, "this expense"));
                    if (e.State != EntityState.Added) dates.Add(((DateOnly)e.Property(nameof(Expense.ExpenseDate)).OriginalValue!, "this expense"));
                    break;
                case ExpenseAdjustment a:
                    dates.Add((a.AdjustmentDate, "this adjustment"));
                    break;
                case BankTransaction t when e.State == EntityState.Added || Deleting(e)
                                            || Changed(e, nameof(BankTransaction.Amount)) || Changed(e, nameof(BankTransaction.TransactionDate))
                                            || Changed(e, nameof(BankTransaction.Direction)):
                    dates.Add((t.TransactionDate, "this bank line"));
                    if (e.State != EntityState.Added) dates.Add(((DateOnly)e.Property(nameof(BankTransaction.TransactionDate)).OriginalValue!, "this bank line"));
                    break;
            }
        }
        if (dates.Count > 0)
        {
            var periods = dates.Select(d => d.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture)).Distinct().ToList();
            var locked = await db.Set<PeriodLock>().AsNoTracking().Where(p => p.IsLocked && periods.Contains(p.Period)).Select(p => p.Period).ToListAsync(ct);
            if (locked.Count > 0)
            {
                var hit = dates.First(d => locked.Contains(d.Date.ToString("yyyy-MM", CultureInfo.InvariantCulture)));
                throw new BusinessRuleException($"{hit.Date.ToString("MMMM", CultureInfo.InvariantCulture)} is locked. Unlock it in Settings, or date {hit.What} in an open period.", "PERIOD_LOCKED");
            }
        }

        // ── Reconciled means locked ────────────────────────────────────────────────────
        var frozenExpenseIds = entries
            .Where(e => e.Entity is Expense && e.State != EntityState.Added && (Deleting(e) || Changed(e, nameof(Expense.Amount))
                        || Changed(e, nameof(Expense.AdjustmentTotal)) || Changed(e, nameof(Expense.ExpenseDate))))
            .Select(e => ((Expense)e.Entity).ExpenseId)
            .Concat(entries.Where(e => e.Entity is ExpenseAdjustment && e.State == EntityState.Added).Select(e => ((ExpenseAdjustment)e.Entity).ExpenseId))
            .Distinct().ToList();
        if (frozenExpenseIds.Count > 0 && !BillingFinanceScope.IsUnmatching
            && (await db.Set<BankMatchAllocation>().AsNoTracking().AnyAsync(a => a.DocType == BankMatchDocType.Expense && frozenExpenseIds.Contains(a.DocId), ct)
                || await db.Set<BankMatchDifference>().AsNoTracking().AnyAsync(d => d.CreatedExpenseId != null && frozenExpenseIds.Contains(d.CreatedExpenseId.Value), ct)
                || await db.Set<BankTransaction>().AsNoTracking().AnyAsync(t => t.MatchedExpenseId != null && frozenExpenseIds.Contains(t.MatchedExpenseId.Value)
                                                                             && t.ReconciliationStatus == ReconciliationStatus.Reconciled, ct)))
        {
            throw new BusinessRuleException("This expense is matched to a reconciled bank line and is locked. Unmatch the line first.", "RECONCILED");
        }

        foreach (EntityEntry e in entries.Where(e => e.Entity is BankTransaction && e.State == EntityState.Modified))
        {
            var t = (BankTransaction)e.Entity;
            var before = (ReconciliationStatus)e.Property(nameof(BankTransaction.ReconciliationStatus)).OriginalValue!;
            if (!BillingFinanceScope.IsUnmatching && before == ReconciliationStatus.Reconciled && t.ReconciliationStatus != ReconciliationStatus.Reconciled
                && await db.Set<BankMatchAllocation>().AsNoTracking().AnyAsync(a => a.BankTransactionId == t.BankTransactionId, ct))
            {
                throw new BusinessRuleException("This line was matched on the Transactions page. Use Unmatch there, with a reason.", "RECONCILED");
            }
        }
        foreach (EntityEntry e in entries.Where(e => e.Entity is BankTransaction && e.State is EntityState.Added or EntityState.Modified))
        {
            var t = (BankTransaction)e.Entity;
            if (t.MatchedExpenseId == null) continue;
            if (e.State == EntityState.Modified && Equals(e.Property(nameof(BankTransaction.MatchedExpenseId)).OriginalValue, t.MatchedExpenseId)) continue;
            ExpenseControl? c = await db.Set<ExpenseControl>().AsNoTracking().FirstOrDefaultAsync(x => x.ExpenseId == t.MatchedExpenseId, ct);
            if (c != null && (c.ApprovalStatus != ExpenseApprovalStatus.Approved || c.IsRecurringDraft))
                throw new BusinessRuleException("This expense is not approved yet, so it cannot be matched to a bank line.", "NOT_APPROVED");
        }

        // ── A supplier with bills or payments stays on record ─────────────────────────
        foreach (EntityEntry e in entries.Where(e => e.Entity is Supplier && Deleting(e)))
        {
            int id = ((Supplier)e.Entity).SupplierId;
            if (await db.Set<PurchaseBill>().AsNoTracking().AnyAsync(b => b.SupplierId == id, ct)
                || await db.Set<VendorPayment>().AsNoTracking().AnyAsync(v => v.SupplierId == id, ct))
                throw new BusinessRuleException("A supplier with bills or payments cannot be deleted — deactivate it instead.", "SUPPLIER_IN_USE");
        }

        // ── Cash receipts written by Record payment ───────────────────────────────────
        foreach (EntityEntry e in entries.Where(e => e.Entity is CashCollection && e.State == EntityState.Modified))
        {
            var c = (CashCollection)e.Entity;
            if (c.Status == CashCollectionStatus.Voided && (CashCollectionStatus)e.Property(nameof(CashCollection.Status)).OriginalValue! == CashCollectionStatus.Recorded
                && !BillingFinanceScope.IsReversing
                && await db.Set<InvoicePayment>().AsNoTracking().AnyAsync(p => p.CashCollectionId == c.CashCollectionId && !p.IsReversed, ct))
            {
                throw new BusinessRuleException("This cash receipt was recorded against an invoice. Reverse it on the Payments page.", "RECORDED_FROM_INVOICE");
            }
        }

        // ── Expense approval ───────────────────────────────────────────────────────────
        var grown = entries.Where(e => e.Entity is Expense && e.State == EntityState.Modified
                                       && (Changed(e, nameof(Expense.Amount)) || Changed(e, nameof(Expense.AdjustmentTotal)))).ToList();
        if (grown.Count > 0)
        {
            var (none, manager) = await LimitsAsync(db, ct);
            foreach (EntityEntry e in grown)
            {
                var x = (Expense)e.Entity;
                decimal before = (decimal)e.Property(nameof(Expense.Amount)).OriginalValue! + (decimal)e.Property(nameof(Expense.AdjustmentTotal)).OriginalValue!;
                decimal after = x.Amount + x.AdjustmentTotal;
                if (after <= before) continue;
                byte level = Level(after, none, manager);
                ExpenseControl? c = db.ChangeTracker.Entries<ExpenseControl>().Select(en => en.Entity).FirstOrDefault(en => en.ExpenseId == x.ExpenseId)
                                    ?? await db.Set<ExpenseControl>().FirstOrDefaultAsync(en => en.ExpenseId == x.ExpenseId, ct);
                bool bankCharge = await db.Set<BankMatchDifference>().AsNoTracking().AnyAsync(d => d.CreatedExpenseId == x.ExpenseId, ct);
                if (c != null && !bankCharge && c.ApprovalStatus == ExpenseApprovalStatus.Approved && level > c.ApprovalLevel)
                {
                    c.ApprovalStatus = ExpenseApprovalStatus.AwaitingApproval;
                    c.ApprovalLevel = level;
                    c.DecidedByUserId = null;
                    c.DecidedUtc = null;
                    c.DecisionComment = null;
                    db.Entry(c).State = EntityState.Modified;
                }
            }
        }

        var added = entries.Where(e => e.Entity is Expense && e.State == EntityState.Added).Select(e => (Expense)e.Entity).ToList();
        if (added.Count > 0)
        {
            _newExpenses.AddOrUpdate(db, added);
        }
    }

    private static byte Level(decimal amount, decimal none, decimal manager) =>
        amount <= none ? (byte)0 : amount <= manager ? (byte)1 : (byte)2;

    private static async Task<(decimal None, decimal Manager)> LimitsAsync(DbContext db, CancellationToken ct)
    {
        var rows = await db.Set<AppSetting>().AsNoTracking()
            .Where(s => s.SettingKey == "Expense.ApprovalLimitNone" || s.SettingKey == "Expense.ApprovalLimitManager")
            .Select(s => new { s.SettingKey, s.SettingValue }).ToListAsync(ct);
        decimal Get(string key, decimal fallback) =>
            decimal.TryParse(rows.FirstOrDefault(r => r.SettingKey == key)?.SettingValue, NumberStyles.Number, CultureInfo.InvariantCulture, out decimal v) ? v : fallback;
        return (Get("Expense.ApprovalLimitNone", 2000m), Get("Expense.ApprovalLimitManager", 25000m));
    }

    /// <summary>After an expense is saved (and has its id), writes its approval row: approved by
    /// itself up to the first limit, otherwise waiting for EXPENSE.APPROVE.</summary>
    private async Task AddControlsAsync(DbContext db, CancellationToken ct)
    {
        if (!_newExpenses.TryGetValue(db, out List<Expense>? expenses)) return;
        _newExpenses.Remove(db);
        var ids = expenses.Select(x => x.ExpenseId).Where(id => id > 0).ToList();
        if (ids.Count == 0) return;
        var existing = await db.Set<ExpenseControl>().AsNoTracking().Where(c => ids.Contains(c.ExpenseId)).Select(c => c.ExpenseId).ToListAsync(ct);
        var (none, manager) = await LimitsAsync(db, ct);
        bool any = false;
        foreach (Expense x in expenses.Where(x => x.ExpenseId > 0 && !existing.Contains(x.ExpenseId)))
        {
            if (db.ChangeTracker.Entries<ExpenseControl>().Any(en => en.Entity.ExpenseId == x.ExpenseId)) continue;
            byte level = Level(x.Amount + x.AdjustmentTotal, none, manager);
            db.Set<ExpenseControl>().Add(new ExpenseControl
            {
                ExpenseId = x.ExpenseId,
                ApprovalLevel = level,
                ApprovalStatus = level == 0 ? ExpenseApprovalStatus.Approved : ExpenseApprovalStatus.AwaitingApproval,
                PaidFrom = ExpensePaidFrom.Bank
            });
            any = true;
        }
        if (any) await db.SaveChangesAsync(ct);
    }
}
