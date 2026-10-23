namespace Regalis.Domain.Enums;

/// <summary>What an item is. Asked first on the item form and never changed afterwards:
/// every movement already recorded against an item assumes its kind. Services (sold on
/// quotations, never stocked) stay <see cref="InventoryItemType.Service"/> items and
/// keep Consumable here.</summary>
public enum ItemKind : byte
{
    /// <summary>A quantity with a unit and a reorder level: coffee, A4 paper.</summary>
    Consumable = 1,
    /// <summary>One physical thing with its own code and QR sticker.</summary>
    Asset = 2
}

/// <summary>The only statuses an asset can be in (Assets spec §10.4). Set by receipts
/// and movements, never typed.</summary>
public enum AssetStatus : byte
{
    InStore = 1,
    InUse = 2,
    InRepair = 3,
    Scrapped = 4,
    Lost = 5,
    ReturnedToSupplier = 6
}

public enum AssetCondition : byte
{
    Good = 1,
    Fair = 2,
    NeedsAttention = 3,
    Damaged = 4
}

/// <summary>Draft changes nothing; Posted has moved stock and created assets and can
/// only be reversed; Imported came from the first Excel import and behaves as Posted.</summary>
public enum GoodsReceiptStatus : byte
{
    Draft = 1,
    Posted = 2,
    Imported = 3
}

/// <summary>Where a receipt came from.</summary>
public enum GoodsReceiptSource : byte
{
    Screen = 1,
    /// <summary>The "+ New item" form for assets: it posts a receipt behind the scenes,
    /// so every asset is still born from one.</summary>
    ItemForm = 2,
    Import = 3,
    PurchaseOrder = 4,
    Reversal = 5,
    /// <summary>Written by the migration from rows the old Incoming screen recorded.</summary>
    Legacy = 6
}

/// <summary>Why something left (Outgoing Goods). The reason decides the rest of the form.</summary>
public enum GoodsIssueReason : byte
{
    UsedUp = 1,
    Moved = 2,
    SentForRepair = 3,
    ReturnedToSupplier = 4,
    Scrapped = 5,
    Lost = 6,
    /// <summary>The opposite of a posted issue (reverse), never chosen on the form.</summary>
    Reversal = 7
}

/// <summary>What wrote a row into the stock ledger (TRN_InventoryTransaction).</summary>
public enum StockSource : byte
{
    GoodsReceipt = 1,
    GoodsIssue = 2,
    StockCount = 3
}

public enum StockCountStatus : byte
{
    Open = 1,
    Submitted = 2,
    Confirmed = 3,
    Cancelled = 4
}

public enum ItemDocumentType : byte
{
    PurchaseInvoice = 1,
    WarrantyCard = 2,
    Photo = 3,
    Other = 4
}
