using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>TRN_InventoryTransaction — append-only. There is no update or delete
/// endpoint; a mistake is corrected with a compensating Adjustment row.</summary>
public class InventoryTransaction : AuditableEntity
{
    public long InventoryTransactionId { get; set; }
    public int InventoryItemId { get; set; }
    public string TransactionNumber { get; set; } = string.Empty;
    public InventoryTransactionType TransactionType { get; set; }
    public decimal Quantity { get; set; }
    public DateOnly TransactionDate { get; set; }
    /// <summary>Supplier for In, department/use for Out (A08 — no supplier master in Phase 1).</summary>
    public string? SourceOrDestination { get; set; }
    public string? ReferenceNumber { get; set; }
    public decimal? UnitCost { get; set; }
    public string? DamageDescription { get; set; }
    public string? DamageCause { get; set; }
    public string? PhotoPath { get; set; }
    public int LocationId { get; set; }
    public long EnteredByUserId { get; set; }
    public string? Notes { get; set; }

    public InventoryItem InventoryItem { get; set; } = null!;
}
