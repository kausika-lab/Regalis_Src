namespace Regalis.Application.Abstractions;

/// <summary>Well-known sequence keys seeded in MST_NumberSequence.</summary>
public static class SequenceKeys
{
    public const string Booking = "Booking";
    public const string Invoice = "Invoice";
    public const string Payment = "Payment";
    public const string WalletTransaction = "WalletTransaction";
    public const string CustomerPass = "CustomerPass";
    public const string ServiceOrder = "ServiceOrder";
    public const string Ticket = "Ticket";
    public const string HousekeepingSchedule = "HousekeepingSchedule";
    public const string InventoryTransaction = "InventoryTransaction";
    public const string PurchaseOrder = "PurchaseOrder";
    public const string Expense = "Expense";
    public const string Quotation = "Quotation";
}

/// <summary>
/// Atomic, gap-free customer-visible numbers (REG-000123, REG/INV/2026-27/00042)
/// via `UPDATE … SET CurrentValue += 1 OUTPUT`. Call inside the business
/// transaction so the number commits or rolls back with the record.
/// IDENTITY is not used for these — it leaks volume and skips on rollback.
/// </summary>
public interface INumberSequenceService
{
    Task<string> NextAsync(string sequenceKey, CancellationToken ct = default);

    /// <summary>The number <see cref="NextAsync"/> would hand out, without consuming
    /// it — for the read-only "Quotation #" preview on the new-quotation form
    /// (document §3.2). It is a preview only: the number that sticks is the one the
    /// server issues on first save, so two admins drafting at once still get
    /// different numbers.</summary>
    Task<string> PeekAsync(string sequenceKey, CancellationToken ct = default);
}
