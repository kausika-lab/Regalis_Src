using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Operations;

/// <summary>MST_InventoryItem. Current stock is never stored as a mutable column
/// (Invariant 11) — it is derived from the transaction ledger.</summary>
public class InventoryItem : AuditableEntity
{
    public int InventoryItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? Category { get; set; }
    /// <summary>Nos, Kg, Ltr, Box.</summary>
    public string Unit { get; set; } = "Nos";
    public decimal OpeningQuantity { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal? UnitCost { get; set; }
    public int LocationId { get; set; }
    public string? Description { get; set; }
    public string? ImagePath { get; set; }

    public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
}
