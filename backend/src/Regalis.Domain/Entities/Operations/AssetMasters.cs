using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>MST_AssetCategory (Assets spec §3.4). Admin-edited, never hard-coded: the
/// default kind and code prefix only pre-fill the item form.</summary>
public class AssetCategory : AuditableEntity
{
    public int AssetCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public ItemKind DefaultKind { get; set; } = ItemKind.Asset;
    /// <summary>Suggested code prefix, e.g. R-2W-E-E. The admin may change it per item.</summary>
    public string CodePrefix { get; set; } = string.Empty;
    /// <summary>Straight-line life for book value (§11.3). Null: not depreciated.</summary>
    public decimal? UsefulLifeYears { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>MST_AssetLocation (§3.5): a room inside the building, not a city — that is
/// MST_Location. Linked to a workspace when the room is one, so "what is in Private
/// Cabin 3" can be answered from either side.</summary>
public class AssetLocation : AuditableEntity
{
    public int AssetLocationId { get; set; }
    public string LocationName { get; set; } = string.Empty;
    public int? WorkspaceId { get; set; }
    /// <summary>A store room: an asset received or moved here is In store, not In use.</summary>
    public bool IsStore { get; set; }
    public int DisplayOrder { get; set; }
}

/// <summary>MST_AssetDetail (§11.1 item_asset): the asset-only half of an item, one row
/// per physical unit. Status is set by receipts and movements; book value is computed
/// on read and never stored.</summary>
public class AssetDetail : IAuditableEntity
{
    public int InventoryItemId { get; set; }
    /// <summary>R-2W-E-EAC — the code is <c>{CodePrefix}-{CodeNumber:000}</c>.</summary>
    public string CodePrefix { get; set; } = string.Empty;
    public int CodeNumber { get; set; }
    public string? SerialNumber { get; set; }
    public int AssetLocationId { get; set; }
    public AssetCondition Condition { get; set; } = AssetCondition.Good;
    public AssetStatus Status { get; set; } = AssetStatus.InUse;
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyUntil { get; set; }
    /// <summary>When its QR label was last printed. Null: no sticker on it yet.</summary>
    public DateTime? LabelPrintedUtc { get; set; }
    /// <summary>The receipt line it was born from.</summary>
    public long? GoodsReceiptLineId { get; set; }
    public string? RetiredReason { get; set; }
    /// <summary>Amount recovered when scrapped, or credit when returned.</summary>
    public decimal? RetiredValue { get; set; }
    public DateTime? RetiredUtc { get; set; }

    public long? CreatedBy { get; set; }
    public DateTime CreatedDate { get; set; }
    public long? ModifiedBy { get; set; }
    public DateTime? ModifiedDate { get; set; }

    public InventoryItem InventoryItem { get; set; } = null!;
}

/// <summary>MST_CodeSequence (§11.1 code_sequence): the last running number issued per
/// prefix — asset codes (R-2W-E-EAC → 004) and document numbers (IN-2026 → 0007). The
/// row is locked while numbers are issued, inside the posting transaction, so two admins
/// posting at once never get the same number; numbers are never handed out twice.</summary>
public class CodeSequence
{
    public string Prefix { get; set; } = string.Empty;
    public int LastNumber { get; set; }
}
