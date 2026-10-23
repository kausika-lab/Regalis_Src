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
    /// <summary>Supplier master for an In row, superseding the free-text
    /// SourceOrDestination the original A08 note anticipated.</summary>
    public int? SupplierId { get; set; }
    /// <summary>The purchase order these goods were received against.</summary>
    public long? PurchaseOrderId { get; set; }
    /// <summary>The supplier's own invoice number for this delivery.</summary>
    public string? PurchaseInvoiceNumber { get; set; }
    /// <summary>Delivery note or bill scan. PhotoPath stays damage-specific.</summary>
    public string? AttachmentPath { get; set; }
    public decimal? UnitCost { get; set; }
    public string? DamageDescription { get; set; }
    public string? DamageCause { get; set; }
    public string? PhotoPath { get; set; }
    public int LocationId { get; set; }
    public long EnteredByUserId { get; set; }
    public string? Notes { get; set; }

    // The document behind the row (Assets spec §11.1 stock_movement). Null on rows the old
    // screens wrote directly.
    public StockSource? SourceType { get; set; }
    public long? SourceId { get; set; }
    /// <summary>For an issue row, why it left; read by the ledger and the reports.</summary>
    public GoodsIssueReason? IssueReason { get; set; }
    /// <summary>The room the goods came into or were taken from (MST_AssetLocation).</summary>
    public int? AssetLocationId { get; set; }

    public InventoryItem InventoryItem { get; set; } = null!;
}
