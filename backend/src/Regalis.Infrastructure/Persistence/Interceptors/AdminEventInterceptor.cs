using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Shared;

namespace Regalis.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Raises admin notifications from the change tracker, so every code path that saves a
/// payment, a wallet entry, a new customer, a customer's own profile edit, a purchase
/// order or a maintenance status change produces its notification — not just the paths
/// someone remembered to hook.
///
/// What changed is read before the save (original values are still there); the rows are
/// written after it, in a second save, once new records have their real keys to link to.
/// Bookings, tickets and staff accounts are raised explicitly through AdminAlerts.
/// </summary>
public class AdminEventInterceptor(ICurrentUser currentUser, IDateTimeProvider clock)
    : SaveChangesInterceptor
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly CultureInfo Indian = CultureInfo.GetCultureInfo("en-IN");

    private sealed class Pending(AdminNotification row, EntityEntry? reference, EntityEntry? customer)
    {
        public AdminNotification Row { get; } = row;
        /// <summary>The record the row links to; its key is only real after the save.</summary>
        public EntityEntry? Reference { get; } = reference;
        public EntityEntry? Customer { get; } = customer;
    }

    private readonly ConditionalWeakTable<DbContext, List<Pending>> _pending = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        Collect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Collect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (Stage(eventData.Context) is { } context) context.SaveChanges();
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (Stage(eventData.Context) is { } context) await context.SaveChangesAsync(cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is not null) _pending.Remove(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) _pending.Remove(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    /// <summary>Stamps the real keys and adds the rows; returns the context to save.</summary>
    private DbContext? Stage(DbContext? context)
    {
        if (context is null || !_pending.TryGetValue(context, out var pending) || pending.Count == 0)
        {
            return null;
        }
        _pending.Remove(context);
        foreach (var p in pending)
        {
            if (p.Reference is not null)
            {
                p.Row.ReferenceId = KeyOf(p.Reference);
                p.Row.ActionRoute = p.Row.ActionRoute?.Replace("{id}", p.Row.ReferenceId?.ToString(Invariant));
            }
            if (p.Customer is not null)
            {
                p.Row.CustomerId = KeyOf(p.Customer);
                p.Row.ActionRoute = p.Row.ActionRoute?.Replace("{customer}", p.Row.CustomerId?.ToString(Invariant));
            }
        }
        context.Set<AdminNotification>().AddRange(pending.Select(p => p.Row));
        return context;
    }

    private static long? KeyOf(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey()?.Properties.FirstOrDefault();
        return key is null ? null : Convert.ToInt64(entry.Property(key.Name).CurrentValue, Invariant);
    }

    private void Collect(DbContext? context)
    {
        if (context is null) return;
        // Original values must be compared against what the code set, whether or not EF
        // has run its own detection yet at this point of the save.
        context.ChangeTracker.DetectChanges();
        var entries = context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified
                        && e.Entity is User or Payment or WalletTransaction or PurchaseOrder
                            or InventoryTransaction or MaintenanceWorkItem)
            .ToList();
        if (entries.Count == 0) return;

        var nowUtc = clock.UtcNow;
        var found = new List<Pending>();
        var billsSeen = new HashSet<string>();
        foreach (var entry in entries)
        {
            switch (entry.Entity)
            {
                case User user when user.UserType == UserType.Customer:
                    if (entry.State == EntityState.Added) found.Add(Registered(entry, user, nowUtc));
                    else if (CustomerEdit(context, entry, user, nowUtc) is { } edit) found.Add(edit);
                    break;
                case Payment payment when StatusBecame(entry, nameof(Payment.Status), out var status):
                    if (PaymentEvent(context, entry, payment, (PaymentStatus)status!, nowUtc) is { } p) found.Add(p);
                    break;
                case WalletTransaction txn when entry.State == EntityState.Added:
                    found.AddRange(WalletEvents(context, entry, txn, nowUtc));
                    break;
                case PurchaseOrder order when StatusBecame(entry, nameof(PurchaseOrder.Status), out var status):
                    if (PurchaseOrderEvent(context, entry, order, (PurchaseOrderStatus)status!, nowUtc) is { } po) found.Add(po);
                    break;
                case InventoryTransaction stock when entry.State == EntityState.Added
                        && stock.PurchaseOrderId is not null
                        && !string.IsNullOrWhiteSpace(stock.PurchaseInvoiceNumber)
                        && billsSeen.Add($"{stock.PurchaseOrderId}|{stock.PurchaseInvoiceNumber.Trim()}"):
                    found.Add(SupplierBill(context, stock, nowUtc));
                    break;
                case MaintenanceWorkItem work when entry.State == EntityState.Modified
                        && StatusBecame(entry, nameof(MaintenanceWorkItem.Status), out var status):
                    if (MaintenanceEvent(context, entry, work, (MaintenanceWorkStatus)status!, nowUtc) is { } m) found.Add(m);
                    break;
            }
        }
        if (found.Count == 0) return;
        if (_pending.TryGetValue(context, out var existing)) existing.AddRange(found);
        else _pending.Add(context, found);
    }

    /// <summary>True when the entity is new with that status, or its status just changed.</summary>
    private static bool StatusBecame(EntityEntry entry, string property, out object? status)
    {
        var prop = entry.Property(property);
        status = prop.CurrentValue;
        return entry.State == EntityState.Added || !Equals(prop.OriginalValue, prop.CurrentValue);
    }

    // ── Customer ────────────────────────────────────────────────────────────

    private Pending Registered(EntityEntry entry, User user, DateTime nowUtc)
    {
        var byAdmin = currentUser.IsAuthenticated;
        var contact = string.Join(" · ", new[] { user.Email, user.MobileNumber }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var row = Row(AdminNotificationType.Customer, "customer.registered",
            $"New customer registered — {user.FullName}",
            (byAdmin ? "Added by an administrator" : "Signed up") +
            (contact.Length > 0 ? $" with {contact}." : "."),
            AdminNotificationSeverity.Info, "User", $"Customer · {user.FullName}",
            "Open customer", "/admin/customers/{id}", nowUtc);
        return new Pending(row, entry, entry);
    }

    /// <summary>A customer changing their own details: which fields, old → new.</summary>
    private Pending? CustomerEdit(DbContext context, EntityEntry entry, User user, DateTime nowUtc)
    {
        if (!currentUser.IsAuthenticated || currentUser.UserId != user.UserId) return null;
        string? Was(string name) => (string?)entry.Property(name).OriginalValue;
        bool Changed(string name) => !string.Equals((Was(name) ?? "").Trim(),
            ((string?)entry.Property(name).CurrentValue ?? "").Trim(), StringComparison.Ordinal);
        string FromTo(string label, string name)
        {
            var before = Was(name);
            var after = (string?)entry.Property(name).CurrentValue;
            return string.IsNullOrWhiteSpace(before) ? $"added {label} {after}"
                : string.IsNullOrWhiteSpace(after) ? $"removed {label} {before}"
                : $"changed {label} from {before} to {after}";
        }

        var changes = new List<string>();
        if (Changed(nameof(User.FullName))) changes.Add(FromTo("name", nameof(User.FullName)));
        if (Changed(nameof(User.MobileNumber))) changes.Add(FromTo("mobile number", nameof(User.MobileNumber)));
        if (Changed(nameof(User.Email))) changes.Add(FromTo("email", nameof(User.Email)));
        if (Changed(nameof(User.CompanyName))) changes.Add(FromTo("company", nameof(User.CompanyName)));
        if (Changed(nameof(User.Gstin))) changes.Add(string.IsNullOrWhiteSpace(Was(nameof(User.Gstin))) ? "added a GSTIN" : "updated the GSTIN");
        if (new[] { nameof(User.Address), nameof(User.AddressLine1), nameof(User.AddressLine2),
                nameof(User.City), nameof(User.State), nameof(User.PostalCode) }.Any(Changed))
        {
            changes.Add("updated the address");
        }
        if (changes.Count == 0) return null;

        var sentence = changes.Count == 1 ? changes[0]
            : string.Join(", ", changes.Take(changes.Count - 1)) + " and " + changes[^1];
        var row = Row(AdminNotificationType.AccountChange, "customer.details_changed",
            "Customer updated their account details", $"{user.FullName} {sentence}.",
            AdminNotificationSeverity.Info, "User", $"Customer · {user.FullName}",
            "Review change", $"/admin/customers/{user.UserId}", nowUtc);
        row.CustomerId = user.UserId;
        row.ReferenceId = user.UserId;
        return new Pending(row, null, null);
    }

    // ── Payment ─────────────────────────────────────────────────────────────

    private static Pending? PaymentEvent(DbContext context, EntityEntry entry, Payment payment,
        PaymentStatus status, DateTime nowUtc)
    {
        if (status is not (PaymentStatus.Success or PaymentStatus.Failed)) return null;
        // A top-up has no booking, invoice, pass or order: the wallet notification covers it.
        if (payment.BookingId is null && payment.InvoiceId is null && payment.CustomerPassId is null
            && payment.ServiceOrderId is null) return null;

        var customer = NameOf(context, payment.CustomerId);
        var what = payment.InvoiceId is { } invoiceId
            ? "invoice " + (context.Set<Invoice>().AsNoTracking().Where(i => i.InvoiceId == invoiceId)
                .Select(i => i.InvoiceNumber).FirstOrDefault() ?? "")
            : payment.BookingId is not null ? "a booking"
            : payment.CustomerPassId is not null ? "a pass" : "an order";
        var amount = Rupees(payment.Amount);
        var failed = status == PaymentStatus.Failed;
        var row = failed
            ? Row(AdminNotificationType.Payment, "payment.failed", $"Payment failed — {amount}",
                $"{customer}'s {payment.PaymentMethod} payment for {what} failed" +
                (string.IsNullOrWhiteSpace(payment.FailureReason) ? "." : $": {payment.FailureReason}."),
                AdminNotificationSeverity.ActionNeeded, "Payment", $"Payment {payment.PaymentNumber}",
                "Open payment", "/admin/payments", nowUtc)
            : Row(AdminNotificationType.Payment, "payment.received", $"Payment received — {amount}",
                $"{customer} paid {what} by {payment.PaymentMethod}." +
                (string.IsNullOrWhiteSpace(payment.GatewayPaymentId) ? "" : $" Reference {payment.GatewayPaymentId}."),
                AdminNotificationSeverity.Info, "Payment", $"Payment {payment.PaymentNumber}",
                "Open payment", "/admin/payments", nowUtc);
        row.CustomerId = payment.CustomerId;
        return new Pending(row, entry, null);
    }

    // ── Wallet ──────────────────────────────────────────────────────────────

    private static IEnumerable<Pending> WalletEvents(DbContext context, EntityEntry entry,
        WalletTransaction txn, DateTime nowUtc)
    {
        var customer = NameOf(context, txn.CustomerId);
        var label = $"Wallet {txn.TransactionNumber} · {customer}";
        var balance = Rupees(txn.BalanceAfter);
        (string Code, string Title, string Body)? ev = txn.Category switch
        {
            WalletCategory.Recharge when txn.Direction == WalletDirection.Credit =>
                ("wallet.recharge", $"Wallet recharged — {customer}",
                    $"{Rupees(txn.Amount)} added. Balance {balance}."),
            WalletCategory.Refund or WalletCategory.Cancellation when txn.Direction == WalletDirection.Credit =>
                ("wallet.refund", $"Refund to wallet — {customer}",
                    $"{Rupees(txn.Amount)} refunded: {txn.Narration}. Balance {balance}."),
            WalletCategory.AdminAdjustment =>
                ("wallet.adjustment", $"Wallet adjusted — {customer}",
                    $"{(txn.Direction == WalletDirection.Credit ? "+" : "−")}{Rupees(txn.Amount)} by an administrator" +
                    (string.IsNullOrWhiteSpace(txn.Narration) ? "" : $" ({txn.Narration})") + $". Balance {balance}."),
            _ => null
        };
        if (ev is { } e)
        {
            var row = Row(AdminNotificationType.Wallet, e.Code, e.Title, e.Body,
                AdminNotificationSeverity.Info, "WalletTransaction", label, "Open wallet", "/admin/wallet", nowUtc);
            row.CustomerId = txn.CustomerId;
            yield return new Pending(row, entry, null);
        }

        // Only when this debit crosses the limit, not on every debit below it.
        if (txn.Direction == WalletDirection.Debit)
        {
            var threshold = LowBalanceThreshold(context);
            if (threshold > 0 && txn.BalanceAfter < threshold && txn.BalanceAfter + txn.Amount >= threshold)
            {
                var row = Row(AdminNotificationType.Wallet, "wallet.low_balance", $"Wallet balance low — {customer}",
                    $"Balance is {balance}, below the {Rupees(threshold)} alert limit set in Settings → Billing.",
                    AdminNotificationSeverity.Info, "WalletTransaction", label, "Open wallet", "/admin/wallet", nowUtc);
                row.CustomerId = txn.CustomerId;
                yield return new Pending(row, entry, null);
            }
        }
    }

    private static decimal LowBalanceThreshold(DbContext context)
    {
        var raw = context.Set<AppSetting>().AsNoTracking()
            .Where(s => s.SettingKey == "Wallet.LowBalanceThreshold")
            .Select(s => s.SettingValue).FirstOrDefault();
        return decimal.TryParse(raw, NumberStyles.Number, Invariant, out var value) ? value : 100m;
    }

    // ── Purchase ────────────────────────────────────────────────────────────

    private static Pending? PurchaseOrderEvent(DbContext context, EntityEntry entry, PurchaseOrder order,
        PurchaseOrderStatus status, DateTime nowUtc)
    {
        if (entry.State == EntityState.Added) return null;
        var supplier = SupplierName(context, order.SupplierId);
        var row = status switch
        {
            PurchaseOrderStatus.Sent => Row(AdminNotificationType.Purchase, "purchase.po_sent",
                $"PO sent — {order.PoNumber}", $"Sent to {supplier} for {Rupees(order.TotalAmount)}.",
                AdminNotificationSeverity.Info, "PurchaseOrder", $"PO {order.PoNumber} · {supplier}",
                "Open PO", "/admin/purchase-orders/{id}", nowUtc),
            PurchaseOrderStatus.PartiallyReceived or PurchaseOrderStatus.Received => Row(
                AdminNotificationType.Purchase, "purchase.goods_received",
                $"Goods received — {order.PoNumber}",
                $"{(status == PurchaseOrderStatus.Received ? "Everything" : "Part of the order")} from {supplier} has been received into stock.",
                AdminNotificationSeverity.Info, "PurchaseOrder", $"PO {order.PoNumber} · {supplier}",
                "Open PO", "/admin/purchase-orders/{id}", nowUtc),
            _ => null
        };
        return row is null ? null : new Pending(row, entry, null);
    }

    private static Pending SupplierBill(DbContext context, InventoryTransaction stock, DateTime nowUtc)
    {
        var poId = stock.PurchaseOrderId!.Value;
        var po = context.ChangeTracker.Entries<PurchaseOrder>().Select(e => e.Entity)
                     .FirstOrDefault(p => p.PurchaseOrderId == poId)
                 ?? context.Set<PurchaseOrder>().AsNoTracking().FirstOrDefault(p => p.PurchaseOrderId == poId);
        var supplier = SupplierName(context, po?.SupplierId ?? stock.SupplierId ?? 0);
        var row = Row(AdminNotificationType.Purchase, "purchase.bill_recorded",
            $"Supplier bill recorded — {stock.PurchaseInvoiceNumber!.Trim()}",
            $"{supplier} billed against PO {po?.PoNumber}.", AdminNotificationSeverity.Info,
            "PurchaseOrder", $"PO {po?.PoNumber} · {supplier}", "Open PO", $"/admin/purchase-orders/{poId}", nowUtc);
        row.ReferenceId = poId;
        return new Pending(row, null, null);
    }

    // ── Maintenance ─────────────────────────────────────────────────────────

    private static Pending? MaintenanceEvent(DbContext context, EntityEntry entry, MaintenanceWorkItem work,
        MaintenanceWorkStatus status, DateTime nowUtc)
    {
        var assignee = NameOf(context, work.AssignedToUserId);
        var row = status switch
        {
            MaintenanceWorkStatus.Done => Row(AdminNotificationType.Maintenance, "maintenance.verification",
                $"Work waiting for verification — {work.Title}",
                $"{work.WorkNumber}, assigned to {assignee}, is marked done and needs verifying.",
                AdminNotificationSeverity.ActionNeeded, "MaintenanceWork", $"Maintenance · {work.WorkNumber}",
                "Open task", "/admin/housekeeping", nowUtc),
            MaintenanceWorkStatus.Overdue => Row(AdminNotificationType.Maintenance, "maintenance.overdue",
                $"Maintenance task overdue — {work.Title}",
                $"{work.WorkNumber}, assigned to {assignee}, passed its due time of " +
                $"{IstClock.ToIst(work.DueUtc).ToString("dd MMM, hh:mm tt", Invariant)} without being completed.",
                AdminNotificationSeverity.ActionNeeded, "MaintenanceWork", $"Maintenance · {work.WorkNumber}",
                "Open task", "/admin/housekeeping", nowUtc),
            _ => null
        };
        if (row is null) return null;
        row.ReferenceId = work.MaintenanceWorkItemId;
        return new Pending(row, null, null);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static AdminNotification Row(AdminNotificationType type, string code, string title, string body,
        AdminNotificationSeverity severity, string referenceType, string referenceLabel,
        string actionLabel, string actionRoute, DateTime nowUtc) => new()
    {
        Type = type,
        EventCode = code,
        Severity = severity,
        Title = title.Length > 200 ? title[..199] + "…" : title,
        Body = body.Length > 1000 ? body[..999] + "…" : body,
        ReferenceType = referenceType,
        ReferenceLabel = referenceLabel.Length > 120 ? referenceLabel[..119] + "…" : referenceLabel,
        ActionLabel = actionLabel,
        ActionRoute = actionRoute,
        CreatedUtc = nowUtc
    };

    private static string NameOf(DbContext context, long userId) =>
        context.ChangeTracker.Entries<User>().Select(e => e.Entity).FirstOrDefault(u => u.UserId == userId)?.FullName
        ?? context.Set<User>().AsNoTracking().Where(u => u.UserId == userId).Select(u => u.FullName).FirstOrDefault()
        ?? "a customer";

    private static string SupplierName(DbContext context, int supplierId) =>
        context.Set<Supplier>().AsNoTracking().Where(s => s.SupplierId == supplierId)
            .Select(s => s.SupplierName).FirstOrDefault() ?? "the supplier";

    private static string Rupees(decimal amount) => "₹" + amount.ToString("#,##0.00", Indian);
}
