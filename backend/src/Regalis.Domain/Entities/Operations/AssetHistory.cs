using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>TRN_AssetRepair (§11.1 item_repair). Opened by a "Sent for repair" movement;
/// while it is open the asset cannot be moved or issued. Closing it puts the asset back.</summary>
public class AssetRepair : AuditableEntity
{
    public long AssetRepairId { get; set; }
    public int InventoryItemId { get; set; }
    public long GoodsIssueId { get; set; }
    public DateOnly SentOn { get; set; }
    public string RepairVendor { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal? ExpectedCost { get; set; }
    public decimal? Cost { get; set; }
    public bool IsOpen { get; set; } = true;
    public DateOnly? ClosedOn { get; set; }
    public string? ClosedNote { get; set; }
    /// <summary>The status it goes back to: In use or In store.</summary>
    public AssetStatus ReturnStatus { get; set; } = AssetStatus.InUse;
}

/// <summary>TRN_ItemEvent (§11.1 item_event): every action on an item, with who did it
/// and when. Append-only — the drawer's Movements tab and "Last movements" read it.</summary>
public class ItemEvent
{
    public long ItemEventId { get; set; }
    public int InventoryItemId { get; set; }
    /// <summary>RECEIVED, MOVED, SENT_FOR_REPAIR, REPAIR_CLOSED, RETURNED, SCRAPPED, LOST,
    /// USED_UP, COUNTED, REVERSED, LABEL_PRINTED, EDITED, DEACTIVATED, DOCUMENT_ADDED.</summary>
    public string EventType { get; set; } = string.Empty;
    /// <summary>The business date (the movement's date), not when it was typed.</summary>
    public DateOnly EventDate { get; set; }
    public string Summary { get; set; } = string.Empty;
    public string? FromText { get; set; }
    public string? ToText { get; set; }
    public string? DocumentNumber { get; set; }
    public string? MetaJson { get; set; }
    public long? ActorUserId { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>TRN_ItemDocument: purchase invoice, warranty card, photos (drawer Documents tab).</summary>
public class ItemDocument : AuditableEntity
{
    public long ItemDocumentId { get; set; }
    public int InventoryItemId { get; set; }
    public ItemDocumentType DocumentType { get; set; } = ItemDocumentType.Other;
    public string FileName { get; set; } = string.Empty;
    public string StoredFileName { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public long UploadedByUserId { get; set; }
    public DateTime UploadedUtc { get; set; }
}

/// <summary>TRN_StockCount (§8.4). The counter writes what is on the shelf without seeing
/// the recorded figure; confirming writes one adjustment movement per difference. The
/// recorded figure is never overwritten.</summary>
public class StockCount : AuditableEntity
{
    public long StockCountId { get; set; }
    /// <summary>SC-2026-0003.</summary>
    public string CountNumber { get; set; } = string.Empty;
    public DateOnly CountDate { get; set; }
    /// <summary>Only the consumables kept here; null counts every consumable.</summary>
    public int? AssetLocationId { get; set; }
    public string CountedBy { get; set; } = string.Empty;
    public StockCountStatus Status { get; set; } = StockCountStatus.Open;
    public string? Notes { get; set; }
    public DateTime? SubmittedUtc { get; set; }
    public DateTime? ConfirmedUtc { get; set; }
    public long? ConfirmedByUserId { get; set; }

    public ICollection<StockCountLine> Lines { get; set; } = new List<StockCountLine>();
}

public class StockCountLine : AuditableEntity
{
    public long StockCountLineId { get; set; }
    public long StockCountId { get; set; }
    public int InventoryItemId { get; set; }
    public decimal? CountedQuantity { get; set; }
    /// <summary>The ledger figure when the count was confirmed.</summary>
    public decimal? RecordedQuantity { get; set; }
    public decimal? Difference { get; set; }

    public StockCount StockCount { get; set; } = null!;
}
