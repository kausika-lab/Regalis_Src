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

    // Assets module (Assets spec §3). An asset is one physical unit: its own row, its own
    // code and its own MST_AssetDetail row. A consumable is one row whose quantity is the
    // sum of its ledger rows. Kind is set when the item is created and never changes.
    public ItemKind Kind { get; set; } = ItemKind.Consumable;
    public int? AssetCategoryId { get; set; }
    /// <summary>Blue Star, Godrej, Featherlite — kept out of the name.</summary>
    public string? BrandModel { get; set; }
    /// <summary>Who it was bought from, when that supplier is in the Suppliers module.</summary>
    public int? SupplierId { get; set; }
    /// <summary>The vendor as written (the team's sheet has names, not supplier records).</summary>
    public string? VendorName { get; set; }
    public string? Remarks { get; set; }
    /// <summary>Consumables: the room it is kept in (Pantry Area, Store). Assets keep their
    /// room on <see cref="AssetDetail"/>.</summary>
    public int? KeptInLocationId { get; set; }

    public AssetCategory? AssetCategory { get; set; }
    public AssetDetail? AssetDetail { get; set; }
    public ICollection<InventoryTransaction> Transactions { get; set; } = new List<InventoryTransaction>();
}
