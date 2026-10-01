using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_Expense — a daily company expense (document §5). Description and
/// Remarks are both required: the document calls this out explicitly as a user
/// request, so it is enforced in the configuration and the validator, not just the UI.</summary>
public class Expense : AuditableEntity
{
    public long ExpenseId { get; set; }
    /// <summary>REG/EXP/{FY}/{00001} — gap-free, from MST_NumberSequence.</summary>
    public string ExpenseNumber { get; set; } = string.Empty;
    public DateOnly ExpenseDate { get; set; }
    /// <summary>Free text today, matching InventoryItem.Category. A master table is
    /// an open question (A10) — moving to one later is an additive change.</summary>
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    /// <summary>Signed sum of this expense's live adjustments (increases minus
    /// decreases). ExpenseService recomputes it from TRN_ExpenseAdjustment whenever an
    /// adjustment changes, so every total stays a plain SUM(Amount + AdjustmentTotal):
    /// SQL Server refuses SUM over a correlated subquery.</summary>
    public decimal AdjustmentTotal { get; set; }
    /// <summary>Reuses the existing PaymentMethod enum rather than inventing a
    /// parallel "payment mode" list.</summary>
    public PaymentMethod PaymentMethod { get; set; }
    public string Remarks { get; set; } = string.Empty;
    /// <summary>Scanned bill, if any.</summary>
    public string? AttachmentPath { get; set; }
    public int LocationId { get; set; }
    /// <summary>"Created By" from the document — captured explicitly as well as in
    /// the audit columns, because it is shown on the expense list.</summary>
    public long EnteredByUserId { get; set; }
    /// <summary>Optional provenance when the expense settles a purchase.</summary>
    public int? SupplierId { get; set; }
    public long? PurchaseOrderId { get; set; }
}
