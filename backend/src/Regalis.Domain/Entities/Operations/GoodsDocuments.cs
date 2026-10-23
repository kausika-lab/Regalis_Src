using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>TRN_GoodsReceipt — Incoming Goods (Assets spec §6). The only way stock goes
/// up and the only way an asset record is born. A draft changes nothing; posting moves
/// stock and creates the assets; a posted receipt is never edited, only reversed by an
/// opposite receipt that stays beside it.</summary>
public class GoodsReceipt : AuditableEntity
{
    public long GoodsReceiptId { get; set; }
    /// <summary>IN-2026-0007.</summary>
    public string ReceiptNumber { get; set; } = string.Empty;
    /// <summary>The day the goods arrived, not the day they were typed in.</summary>
    public DateOnly ReceiptDate { get; set; }
    public GoodsReceiptStatus Status { get; set; } = GoodsReceiptStatus.Draft;
    public GoodsReceiptSource Source { get; set; } = GoodsReceiptSource.Screen;
    /// <summary>The reserved "Opening balance" supplier: stock that was already here.</summary>
    public bool IsOpeningBalance { get; set; }
    public int? SupplierId { get; set; }
    public string? InvoiceNumber { get; set; }
    public long? PurchaseOrderId { get; set; }
    /// <summary>Where it physically went. Null only on an import, whose rows carry rooms.</summary>
    public int? IntoLocationId { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Notes { get; set; }
    public long? PostedByUserId { get; set; }
    public DateTime? PostedUtc { get; set; }
    public long? ReversesReceiptId { get; set; }
    public long? ReversedByReceiptId { get; set; }

    public ICollection<GoodsReceiptLine> Lines { get; set; } = new List<GoodsReceiptLine>();
}

/// <summary>TRN_GoodsReceiptLine. A consumable line names its item. An asset line
/// describes what to create — name, category, prefix, room, cost — and posting it with a
/// quantity of 8 creates eight separate asset records, each with its own code.</summary>
public class GoodsReceiptLine : AuditableEntity
{
    public long GoodsReceiptLineId { get; set; }
    public long GoodsReceiptId { get; set; }
    public int LineNumber { get; set; }
    public ItemKind Kind { get; set; }
    /// <summary>Consumable: the item. Asset: the existing asset it is "more of", if any.</summary>
    public int? InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    /// <summary>Rate each. Null on an asset line means the cost is missing.</summary>
    public decimal? Rate { get; set; }
    public decimal Amount { get; set; }
    public long? PurchaseOrderLineId { get; set; }

    // What an asset line creates.
    public string? ItemName { get; set; }
    public int? AssetCategoryId { get; set; }
    public string? CodePrefix { get; set; }
    public string? BrandModel { get; set; }
    public string? Description { get; set; }
    /// <summary>Only when one unit arrives; several units get theirs from the item page.</summary>
    public string? SerialNumber { get; set; }
    public int? AssetLocationId { get; set; }
    public AssetCondition? Condition { get; set; }
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyUntil { get; set; }
    public string? VendorName { get; set; }
    public string? Remarks { get; set; }

    /// <summary>The codes posting created: R-2W-E-EGC-015 … R-2W-E-EGC-022.</summary>
    public string? FirstCode { get; set; }
    public string? LastCode { get; set; }

    public GoodsReceipt GoodsReceipt { get; set; } = null!;
}

/// <summary>TRN_GoodsIssue — Outgoing Goods (§7). Used up, moved, sent for repair,
/// returned, scrapped or lost. The only way stock goes down and the only way an asset
/// changes room or status. Never deleted; a mistake is reversed.</summary>
public class GoodsIssue : AuditableEntity
{
    public long GoodsIssueId { get; set; }
    /// <summary>OUT-2026-0042.</summary>
    public string IssueNumber { get; set; } = string.Empty;
    public DateOnly IssueDate { get; set; }
    public GoodsIssueReason Reason { get; set; }
    public int? FromLocationId { get; set; }
    public int? ToLocationId { get; set; }
    /// <summary>Sent for repair: who it went to.</summary>
    public string? RepairVendor { get; set; }
    public decimal? ExpectedCost { get; set; }
    /// <summary>Returned to supplier: whether a credit note was raised.</summary>
    public bool? CreditNoteRaised { get; set; }
    /// <summary>Scrapped: what was recovered.</summary>
    public decimal? AmountRecovered { get; set; }
    /// <summary>Lost: who reported it.</summary>
    public string? ReportedBy { get; set; }
    /// <summary>Returned, scrapped: the reason.</summary>
    public string? ReasonText { get; set; }
    public string? Note { get; set; }
    public long PostedByUserId { get; set; }
    public DateTime PostedUtc { get; set; }
    public long? ReversesIssueId { get; set; }
    public long? ReversedByIssueId { get; set; }

    public ICollection<GoodsIssueLine> Lines { get; set; } = new List<GoodsIssueLine>();
}

/// <summary>TRN_GoodsIssueLine. For an asset it keeps the status and room before and
/// after, so a reversal can put them back exactly.</summary>
public class GoodsIssueLine : AuditableEntity
{
    public long GoodsIssueLineId { get; set; }
    public long GoodsIssueId { get; set; }
    public int InventoryItemId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public decimal? Amount { get; set; }
    public AssetStatus? PreviousStatus { get; set; }
    public int? PreviousLocationId { get; set; }
    public AssetStatus? NewStatus { get; set; }
    public int? NewLocationId { get; set; }

    public GoodsIssue GoodsIssue { get; set; } = null!;
    public InventoryItem InventoryItem { get; set; } = null!;
}
