using Regalis.Domain.Common;
using Regalis.Domain.Enums;

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

    // Sales details, used when the item is picked on a quotation.
    public InventoryItemType ItemType { get; set; } = InventoryItemType.Goods;
    /// <summary>HSN for goods, SAC for services.</summary>
    public string? HsnSacCode { get; set; }
    /// <summary>The price it is quoted at; UnitCost is what it costs to buy.</summary>
    public decimal SellingRate { get; set; }
    /// <summary>Default GST rate. Ignored when <see cref="IsNonTaxable"/>.</summary>
    public decimal TaxPercent { get; set; }
    /// <summary>Outside GST altogether, which is not the same as a 0% rate.</summary>
    public bool IsNonTaxable { get; set; } = true;

    public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
}
