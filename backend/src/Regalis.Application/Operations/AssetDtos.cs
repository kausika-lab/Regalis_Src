using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Operations;

// Assets module (Regalis_WorkSpace_Assets_Feature_Specification, Oct 2026): Items,
// Incoming Goods, Outgoing Goods, Current Stock and QR labels. Byte codes match the
// Domain enums: Kind 1 Consumable / 2 Asset; Status 1 In store, 2 In use, 3 In repair,
// 4 Scrapped, 5 Lost, 6 Returned to supplier; Condition 1 Good, 2 Fair, 3 Needs
// attention, 4 Damaged; issue Reason 1 Used up, 2 Moved, 3 Sent for repair, 4 Returned
// to supplier, 5 Scrapped, 6 Lost, 7 Reversal.

// ── Items ───────────────────────────────────────────────────────────────────

public class AssetItemListRequest : PagedRequest
{
    /// <summary>1 Consumable, 2 Asset, 3 Service (sold on quotations, never stocked).</summary>
    public byte? Kind { get; set; }
    public int? CategoryId { get; set; }
    /// <summary>The asset's room, or the room a consumable is kept in.</summary>
    public int? LocationId { get; set; }
    public byte? Status { get; set; }
    public bool? BelowReorder { get; set; }
    /// <summary>Assets whose QR label has never been printed.</summary>
    public bool? NoLabel { get; set; }
    public bool? CostMissing { get; set; }
    public bool? IncludeInactive { get; set; }
    /// <summary>Item ids, comma-separated: "export selected" and labels for a selection.</summary>
    public string? Ids { get; set; }
    /// <summary>The assets one receipt created — "print their labels" after posting.</summary>
    public long? ReceiptId { get; set; }
    /// <summary>asc | desc.</summary>
    public string? SortDir { get; set; }
}

public record AssetItemRowDto(
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    byte Kind,
    bool IsService,
    int? CategoryId,
    string? CategoryName,
    string? BrandModel,
    string? SerialNumber,
    int? LocationId,
    string? LocationName,
    byte? Status,
    byte? Condition,
    decimal? OnHand,
    string Unit,
    decimal ReorderLevel,
    bool BelowReorder,
    decimal? UnitCost,
    decimal? Value,
    DateTime? LabelPrintedUtc,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyUntil,
    bool WarrantyExpiringSoon,
    bool IsActive);

/// <summary>The five cards on Items, counted across every active record, never the page.</summary>
public record AssetSummaryDto(
    int AssetCount,
    decimal AssetPurchaseValue,
    int ConsumableCount,
    int BelowReorderCount,
    List<string> BelowReorderNames,
    int InRepairCount,
    string? InRepairText,
    int NoLabelCount,
    int CostMissingCount);

public record AssetItemListResult(PagedResult<AssetItemRowDto> Page, AssetSummaryDto Summary);

public record AssetValueDto(
    decimal? PurchaseCost,
    decimal? BookValue,
    string Method,
    decimal? UsefulLifeYears,
    decimal? AgeYears,
    decimal? LifeUsedPercent,
    decimal RepairSpend,
    decimal? OnHandValue);

public record ItemEventDto(
    long ItemEventId,
    string EventType,
    DateOnly EventDate,
    string Summary,
    string? FromText,
    string? ToText,
    string? DocumentNumber,
    string? ActorName,
    DateTime CreatedUtc);

public record AssetRepairDto(
    long AssetRepairId,
    long GoodsIssueId,
    string? IssueNumber,
    DateOnly SentOn,
    string RepairVendor,
    string? Description,
    decimal? ExpectedCost,
    decimal? Cost,
    bool IsOpen,
    DateOnly? ClosedOn,
    string? ClosedNote);

public record ItemDocumentDto(
    long ItemDocumentId,
    byte DocumentType,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    string? UploadedByName,
    DateTime UploadedUtc);

public record AssetItemDetailDto(
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    byte Kind,
    bool IsService,
    bool IsActive,
    int? CategoryId,
    string? CategoryName,
    string? Description,
    string? BrandModel,
    decimal? UnitCost,
    int? SupplierId,
    string? SupplierName,
    string? VendorName,
    string? Remarks,
    // Asset half.
    string? CodePrefix,
    string? SerialNumber,
    int? LocationId,
    string? LocationName,
    byte? Status,
    byte? Condition,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyUntil,
    bool WarrantyExpiringSoon,
    DateTime? LabelPrintedUtc,
    string? RetiredReason,
    decimal? RetiredValue,
    DateTime? RetiredUtc,
    int SiblingCount,
    string? SiblingLabel,
    string? QrText,
    AssetRepairDto? OpenRepair,
    // Consumable half.
    string Unit,
    decimal ReorderLevel,
    decimal? OnHand,
    bool BelowReorder,
    int? KeptInLocationId,
    string? KeptInLocationName,
    // Sales details (quotations).
    string? HsnSacCode,
    decimal SellingRate,
    decimal TaxPercent,
    bool IsNonTaxable,
    AssetValueDto Value,
    List<ItemEventDto> RecentEvents,
    int DocumentCount);

/// <summary>The item form (§5). Kind is asked first and never changes. An asset form
/// creates Quantity separate records through a posted receipt; a consumable form has no
/// opening quantity at all — its first stock comes in on Incoming Goods.</summary>
public record CreateAssetItemRequest(
    byte Kind,
    string ItemName,
    int? CategoryId,
    string? Description,
    string? BrandModel,
    decimal? UnitCost,
    int? SupplierId,
    string? VendorName,
    string? Remarks,
    // Asset only.
    string? CodePrefix,
    int? Quantity,
    string? SerialNumber,
    int? LocationId,
    byte? Condition,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyUntil,
    // Consumable / service only.
    string? ItemCode,
    string? Unit,
    decimal? ReorderLevel,
    int? KeptInLocationId,
    bool IsService = false,
    string? HsnSacCode = null,
    decimal? SellingRate = null,
    decimal? TaxPercent = null,
    bool? IsNonTaxable = null);

public record CreateAssetItemResult(
    List<int> InventoryItemIds,
    List<string> Codes,
    string? ReceiptNumber,
    long? GoodsReceiptId,
    decimal? TotalCost);

/// <summary>Edits what an item IS. Never its kind, code, quantity, room or status:
/// those change only through movements.</summary>
public record UpdateAssetItemRequest(
    string ItemName,
    int? CategoryId,
    string? Description,
    string? BrandModel,
    decimal? UnitCost,
    int? SupplierId,
    string? VendorName,
    string? Remarks,
    string? SerialNumber,
    byte? Condition,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyUntil,
    string? Unit,
    decimal? ReorderLevel,
    int? KeptInLocationId,
    string? HsnSacCode = null,
    decimal? SellingRate = null,
    decimal? TaxPercent = null,
    bool? IsNonTaxable = null);

public record AssetItemSaveResult(AssetItemDetailDto Item, List<string> Warnings);

public record DeactivateItemRequest(string? Reason);

public record NextCodesDto(string Prefix, int Count, List<string> Codes, string Range);

public record SerialCheckDto(bool Duplicate, string? ExistingCode, int? ExistingItemId);

public record AssetLabelDto(
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    string? BrandModel,
    string? SerialNumber,
    string? LocationName,
    string QrText,
    DateTime? LabelPrintedUtc);

public record AssetLabelSheetDto(List<AssetLabelDto> Labels, int SkippedConsumables, int SkippedRetired);

public record MarkLabelsPrintedRequest(List<int> InventoryItemIds);

public record CloseRepairRequest(DateOnly? ClosedOn, decimal? Cost, string? Note, byte? Condition);

public record AssetFormOptionsDto(
    List<AssetCategoryDto> Categories,
    List<AssetLocationDto> Locations,
    List<IdNameDto> Suppliers,
    List<string> Units,
    string ContactEmail,
    string OwnerName,
    int WarrantyAlertDays);

public record IdNameDto(int Id, string Name);

// ── Categories and rooms ────────────────────────────────────────────────────

public record AssetCategoryDto(
    int AssetCategoryId,
    string CategoryName,
    byte DefaultKind,
    string CodePrefix,
    decimal? UsefulLifeYears,
    int DisplayOrder,
    bool IsActive,
    int ItemCount);

public record SaveAssetCategoryRequest(
    string CategoryName,
    byte DefaultKind,
    string CodePrefix,
    decimal? UsefulLifeYears,
    int? DisplayOrder,
    bool IsActive = true);

public record AssetLocationDto(
    int AssetLocationId,
    string LocationName,
    int? WorkspaceId,
    string? WorkspaceName,
    bool IsStore,
    int DisplayOrder,
    bool IsActive,
    int AssetCount);

public record SaveAssetLocationRequest(
    string LocationName,
    int? WorkspaceId,
    bool IsStore,
    int? DisplayOrder,
    bool IsActive = true);

// ── Incoming Goods ──────────────────────────────────────────────────────────

public class GoodsReceiptListRequest : PagedRequest
{
    public int? SupplierId { get; set; }
    public byte? Status { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record GoodsReceiptRowDto(
    long GoodsReceiptId,
    string ReceiptNumber,
    DateOnly ReceiptDate,
    byte Status,
    byte Source,
    bool IsOpeningBalance,
    int? SupplierId,
    string? SupplierName,
    string? InvoiceNumber,
    string? PoNumber,
    int LineCount,
    int AssetCount,
    int ConsumableLines,
    decimal TotalAmount,
    bool IsReversal,
    bool IsReversed,
    string? ReversalNumber);

public record GoodsReceiptLineDto(
    long GoodsReceiptLineId,
    int LineNumber,
    byte Kind,
    int? InventoryItemId,
    string? ItemCode,
    string ItemName,
    string Unit,
    decimal Quantity,
    decimal? Rate,
    decimal Amount,
    long? PurchaseOrderLineId,
    int? AssetCategoryId,
    string? CodePrefix,
    string? BrandModel,
    string? Description,
    string? SerialNumber,
    int? LocationId,
    string? LocationName,
    byte? Condition,
    DateOnly? PurchaseDate,
    DateOnly? WarrantyUntil,
    string? VendorName,
    string? Remarks,
    string? FirstCode,
    string? LastCode);

public record GoodsReceiptDto(
    long GoodsReceiptId,
    string ReceiptNumber,
    DateOnly ReceiptDate,
    byte Status,
    byte Source,
    bool IsOpeningBalance,
    int? SupplierId,
    string? SupplierName,
    string? InvoiceNumber,
    long? PurchaseOrderId,
    string? PoNumber,
    int? IntoLocationId,
    string? IntoLocationName,
    decimal TotalAmount,
    string? Notes,
    string? PostedByName,
    DateTime? PostedUtc,
    long? ReversesReceiptId,
    string? ReversesNumber,
    long? ReversedByReceiptId,
    string? ReversedByNumber,
    List<GoodsReceiptLineDto> Lines);

/// <summary>One line of the receipt form. A consumable line names the item. An asset
/// line names an existing asset to receive "more of" (TemplateItemId), or describes a
/// new one (ItemName, CategoryId, CodePrefix …).</summary>
public record SaveGoodsReceiptLineRequest(
    byte Kind,
    int? InventoryItemId,
    decimal Quantity,
    decimal? Rate,
    long? PurchaseOrderLineId = null,
    string? ItemName = null,
    int? AssetCategoryId = null,
    string? CodePrefix = null,
    string? BrandModel = null,
    string? Description = null,
    string? SerialNumber = null,
    int? LocationId = null,
    byte? Condition = null,
    DateOnly? PurchaseDate = null,
    DateOnly? WarrantyUntil = null,
    string? VendorName = null,
    string? Remarks = null);

public record SaveGoodsReceiptRequest(
    DateOnly ReceiptDate,
    bool IsOpeningBalance,
    int? SupplierId,
    string? InvoiceNumber,
    long? PurchaseOrderId,
    int? IntoLocationId,
    string? Notes,
    List<SaveGoodsReceiptLineRequest> Lines,
    /// <summary>true: save and post in one step ("Post receipt" on a new form).</summary>
    bool Post = false,
    /// <summary>The admin saw the amber "no purchase cost" warning and posts anyway.</summary>
    bool ConfirmMissingCost = false);

public record PostGoodsReceiptRequest(bool ConfirmMissingCost = false);

public record ReverseRequest(string Reason);

/// <summary>What posting each line will do, worked out before posting (§6.3).</summary>
public record GoodsReceiptLinePreviewDto(
    int LineIndex,
    byte Kind,
    string Message,
    List<string> Codes,
    decimal? StockBefore,
    decimal? StockAfter,
    string? Unit,
    bool CostMissing,
    string? Error);

public record GoodsReceiptPreviewDto(
    List<GoodsReceiptLinePreviewDto> Lines,
    decimal Total,
    int AssetsCreated,
    int AssetsWithoutCost,
    string? MissingCostWarning);

/// <summary>The lines a purchase order still has to deliver, for "Against purchase order".</summary>
public record PurchaseOrderReceiptLineDto(
    long PurchaseOrderLineId,
    int? InventoryItemId,
    string ItemName,
    string? ItemCode,
    byte? Kind,
    string Unit,
    decimal Ordered,
    decimal Received,
    decimal Pending,
    decimal Rate);

public record OpenPurchaseOrderDto(
    long PurchaseOrderId,
    string PoNumber,
    int SupplierId,
    string SupplierName,
    DateOnly PoDate,
    List<PurchaseOrderReceiptLineDto> Lines);

// ── Outgoing Goods ──────────────────────────────────────────────────────────

public class GoodsIssueListRequest : PagedRequest
{
    public byte? Reason { get; set; }
    public int? InventoryItemId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public record GoodsIssueRowDto(
    long GoodsIssueId,
    string IssueNumber,
    DateOnly IssueDate,
    byte Reason,
    int LineCount,
    int? FirstItemId,
    string? FirstItemName,
    string? FirstItemCode,
    byte? FirstItemKind,
    decimal? FirstQuantity,
    string? FirstUnit,
    string? WhereItWent,
    string? PostedByName,
    bool IsReversal,
    bool IsReversed,
    string? ReversalNumber,
    bool RepairOpen);

public record GoodsIssueLineDto(
    long GoodsIssueLineId,
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    byte Kind,
    string Unit,
    decimal Quantity,
    decimal? UnitCost,
    decimal? Amount,
    byte? PreviousStatus,
    string? PreviousLocationName,
    byte? NewStatus,
    string? NewLocationName);

public record GoodsIssueDto(
    long GoodsIssueId,
    string IssueNumber,
    DateOnly IssueDate,
    byte Reason,
    int? FromLocationId,
    string? FromLocationName,
    int? ToLocationId,
    string? ToLocationName,
    string? RepairVendor,
    decimal? ExpectedCost,
    bool? CreditNoteRaised,
    decimal? AmountRecovered,
    string? ReportedBy,
    string? ReasonText,
    string? Note,
    string? PostedByName,
    DateTime PostedUtc,
    long? ReversesIssueId,
    string? ReversesNumber,
    long? ReversedByIssueId,
    string? ReversedByNumber,
    List<GoodsIssueLineDto> Lines);

public record GoodsIssueLineRequest(int InventoryItemId, decimal Quantity);

public record CreateGoodsIssueRequest(
    byte Reason,
    DateOnly? IssueDate,
    int? FromLocationId,
    int? ToLocationId,
    List<GoodsIssueLineRequest> Lines,
    string? RepairVendor = null,
    decimal? ExpectedCost = null,
    bool? CreditNoteRaised = null,
    decimal? AmountRecovered = null,
    string? ReportedBy = null,
    string? ReasonText = null,
    string? Note = null,
    /// <summary>Bulk actions from the Items list: skip lines the reason does not apply to
    /// (consumables for a move) instead of refusing the whole movement.</summary>
    bool SkipInapplicable = false);

public record GoodsIssueLinePreviewDto(
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    byte Kind,
    string Unit,
    decimal? OnHand,
    decimal? After,
    decimal ReorderLevel,
    bool GoesBelowReorder,
    string Message,
    string? Error);

public record GoodsIssuePreviewDto(List<GoodsIssueLinePreviewDto> Lines, int Skipped, string? SkippedMessage);

public record GoodsIssueResult(GoodsIssueDto Issue, int Skipped, string? Message);

// ── Current Stock ───────────────────────────────────────────────────────────

public record StockRowDto(
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    string Unit,
    string? CategoryName,
    string? KeptIn,
    decimal Opening,
    decimal In,
    decimal Out,
    decimal OnHand,
    decimal ReorderLevel,
    decimal? UnitCost,
    decimal? Value,
    bool OrderNow);

public record StockSheetDto(
    DateTime AsAtUtc,
    DateOnly From,
    DateOnly To,
    decimal ValueOnHand,
    int BelowReorderCount,
    int MovementsThisMonth,
    int InThisMonth,
    int OutThisMonth,
    DateOnly? LastCountDate,
    List<StockRowDto> Rows);

public record AssetsByLocationRowDto(
    int? LocationId,
    string LocationName,
    string? WorkspaceName,
    int AssetCount,
    decimal BookValue,
    decimal PurchaseValue,
    int InRepair,
    int CostMissing);

public class StockLedgerRequest : PagedRequest
{
    public int? InventoryItemId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>1 Consumable, 2 Asset.</summary>
    public byte? Kind { get; set; }
}

public record StockLedgerRowDto(
    long InventoryTransactionId,
    string TransactionNumber,
    DateOnly TransactionDate,
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    byte Kind,
    string Unit,
    byte TransactionType,
    decimal Change,
    decimal Balance,
    string What,
    string? DocumentNumber,
    byte? SourceType,
    long? SourceId,
    string? LocationName,
    string? EnteredByName,
    DateTime CreatedDate);

// ── Stock counts ────────────────────────────────────────────────────────────

public record StartStockCountRequest(DateOnly? CountDate, int? LocationId, string? CountedBy, string? Notes);

public record StockCountLineDto(
    long StockCountLineId,
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    string Unit,
    string? KeptIn,
    decimal? CountedQuantity,
    // Hidden from the counter: only filled once the count is submitted.
    decimal? RecordedQuantity,
    decimal? Difference);

public record StockCountDto(
    long StockCountId,
    string CountNumber,
    DateOnly CountDate,
    int? LocationId,
    string? LocationName,
    string CountedBy,
    byte Status,
    string? Notes,
    DateTime? SubmittedUtc,
    DateTime? ConfirmedUtc,
    int Differences,
    List<StockCountLineDto> Lines);

public record StockCountRowDto(
    long StockCountId,
    string CountNumber,
    DateOnly CountDate,
    string? LocationName,
    string CountedBy,
    byte Status,
    int LineCount,
    int Differences);

public record SubmitStockCountLine(int InventoryItemId, decimal CountedQuantity);

public record SubmitStockCountRequest(List<SubmitStockCountLine> Lines);

// ── Import (§12) ────────────────────────────────────────────────────────────

public record AssetImportPrefixDto(string Prefix, int Rows, int Units, string Range);

public record AssetImportRowDto(
    int RowNumber,
    string Status,
    List<string> Errors,
    List<string> Notes,
    string? ItemName,
    string? Prefix,
    int Quantity,
    string? Codes,
    decimal? UnitCost,
    string? Location,
    string? Category);

public record AssetImportResultDto(
    DateOnly ReceiptDate,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    int Units,
    int CostMissingRows,
    decimal TotalValue,
    List<string> NewLocations,
    List<string> IgnoredColumns,
    List<AssetImportPrefixDto> Prefixes,
    List<AssetImportRowDto> Rows,
    bool Saved,
    string? ReceiptNumber,
    long? GoodsReceiptId);

// ── Validators ──────────────────────────────────────────────────────────────

public class CreateAssetItemRequestValidator : AbstractValidator<CreateAssetItemRequest>
{
    public CreateAssetItemRequestValidator()
    {
        RuleFor(x => x.Kind).InclusiveBetween((byte)1, (byte)2).WithMessage("Choose Asset or Consumable.");
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.BrandModel).MaximumLength(150);
        RuleFor(x => x.VendorName).MaximumLength(150);
        RuleFor(x => x.Remarks).MaximumLength(500);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null);
        RuleFor(x => x.CodePrefix).MaximumLength(30);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.ItemCode).MaximumLength(40);
        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0).When(x => x.ReorderLevel is not null);
        RuleFor(x => x.Condition).InclusiveBetween((byte)1, (byte)4).When(x => x.Condition is not null);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 500).When(x => x.Kind == 2)
            .WithMessage("How many are you adding? Enter 1 to 500.");
        RuleFor(x => x.HsnSacCode).MaximumLength(20).Matches("^[0-9]*$")
            .WithMessage("HSN / SAC code has digits only.");
        RuleFor(x => x.SellingRate).GreaterThanOrEqualTo(0).When(x => x.SellingRate is not null);
        RuleFor(x => x.TaxPercent).InclusiveBetween(0, 100).When(x => x.TaxPercent is not null);
    }
}

public class UpdateAssetItemRequestValidator : AbstractValidator<UpdateAssetItemRequest>
{
    public UpdateAssetItemRequestValidator()
    {
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.BrandModel).MaximumLength(150);
        RuleFor(x => x.VendorName).MaximumLength(150);
        RuleFor(x => x.Remarks).MaximumLength(500);
        RuleFor(x => x.SerialNumber).MaximumLength(100);
        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0).When(x => x.ReorderLevel is not null);
        RuleFor(x => x.Condition).InclusiveBetween((byte)1, (byte)4).When(x => x.Condition is not null);
        RuleFor(x => x.HsnSacCode).MaximumLength(20).Matches("^[0-9]*$")
            .WithMessage("HSN / SAC code has digits only.");
        RuleFor(x => x.SellingRate).GreaterThanOrEqualTo(0).When(x => x.SellingRate is not null);
        RuleFor(x => x.TaxPercent).InclusiveBetween(0, 100).When(x => x.TaxPercent is not null);
    }
}

public class SaveAssetCategoryRequestValidator : AbstractValidator<SaveAssetCategoryRequest>
{
    public SaveAssetCategoryRequestValidator()
    {
        RuleFor(x => x.CategoryName).NotEmpty().MaximumLength(60);
        RuleFor(x => x.DefaultKind).InclusiveBetween((byte)1, (byte)2);
        RuleFor(x => x.CodePrefix).NotEmpty().MaximumLength(30);
        RuleFor(x => x.UsefulLifeYears).InclusiveBetween(0.5m, 100m).When(x => x.UsefulLifeYears is not null)
            .WithMessage("Useful life is between 0.5 and 100 years, or empty for no depreciation.");
    }
}

public class SaveAssetLocationRequestValidator : AbstractValidator<SaveAssetLocationRequest>
{
    public SaveAssetLocationRequestValidator()
    {
        RuleFor(x => x.LocationName).NotEmpty().MaximumLength(100);
    }
}

public class SaveGoodsReceiptRequestValidator : AbstractValidator<SaveGoodsReceiptRequest>
{
    public SaveGoodsReceiptRequestValidator()
    {
        RuleFor(x => x.InvoiceNumber).MaximumLength(80);
        RuleFor(x => x.Notes).MaximumLength(500);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Add at least one line: what arrived.");
        RuleFor(x => x.Lines.Count).LessThanOrEqualTo(200).WithMessage("A receipt has at most 200 lines.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.Kind).InclusiveBetween((byte)1, (byte)2);
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("Quantity must be more than zero.");
            line.RuleFor(l => l.Rate).GreaterThanOrEqualTo(0).When(l => l.Rate is not null);
            line.RuleFor(l => l.ItemName).MaximumLength(150);
            line.RuleFor(l => l.CodePrefix).MaximumLength(30);
            line.RuleFor(l => l.BrandModel).MaximumLength(150);
            line.RuleFor(l => l.Description).MaximumLength(500);
            line.RuleFor(l => l.SerialNumber).MaximumLength(100);
            line.RuleFor(l => l.VendorName).MaximumLength(150);
            line.RuleFor(l => l.Remarks).MaximumLength(500);
            line.RuleFor(l => l.Condition).InclusiveBetween((byte)1, (byte)4).When(l => l.Condition is not null);
        });
    }
}

public class CreateGoodsIssueRequestValidator : AbstractValidator<CreateGoodsIssueRequest>
{
    public CreateGoodsIssueRequestValidator()
    {
        RuleFor(x => x.Reason).InclusiveBetween((byte)1, (byte)6).WithMessage("Choose why it is going out.");
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Choose the item.");
        RuleFor(x => x.Lines.Count).LessThanOrEqualTo(500);
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.InventoryItemId).GreaterThan(0);
            line.RuleFor(l => l.Quantity).GreaterThan(0).WithMessage("Quantity must be more than zero.");
        });
        RuleFor(x => x.RepairVendor).MaximumLength(150);
        RuleFor(x => x.ReportedBy).MaximumLength(150);
        RuleFor(x => x.ReasonText).MaximumLength(300);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.ExpectedCost).GreaterThanOrEqualTo(0).When(x => x.ExpectedCost is not null);
        RuleFor(x => x.AmountRecovered).GreaterThanOrEqualTo(0).When(x => x.AmountRecovered is not null);
    }
}

public class ReverseRequestValidator : AbstractValidator<ReverseRequest>
{
    public ReverseRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().WithMessage("Say why it is being reversed — the trail keeps it.")
            .MaximumLength(300);
    }
}

public class StartStockCountRequestValidator : AbstractValidator<StartStockCountRequest>
{
    public StartStockCountRequestValidator()
    {
        RuleFor(x => x.CountedBy).MaximumLength(150);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

public class SubmitStockCountRequestValidator : AbstractValidator<SubmitStockCountRequest>
{
    public SubmitStockCountRequestValidator()
    {
        RuleForEach(x => x.Lines).ChildRules(line =>
            line.RuleFor(l => l.CountedQuantity).GreaterThanOrEqualTo(0)
                .WithMessage("A count cannot be negative."));
    }
}

public class CloseRepairRequestValidator : AbstractValidator<CloseRepairRequest>
{
    public CloseRepairRequestValidator()
    {
        RuleFor(x => x.Cost).GreaterThanOrEqualTo(0).When(x => x.Cost is not null);
        RuleFor(x => x.Note).MaximumLength(500);
        RuleFor(x => x.Condition).InclusiveBetween((byte)1, (byte)4).When(x => x.Condition is not null);
    }
}

// ── Service ─────────────────────────────────────────────────────────────────

/// <summary>The Assets module. On hand is never stored: it is always the sum of the
/// item's ledger rows. Nothing is deleted — items are made inactive, assets are scrapped
/// or lost, posted documents are reversed.</summary>
public interface IAssetService
{
    // Items
    Task<AssetItemListResult> GetItemsAsync(AssetItemListRequest request, CancellationToken ct = default);
    Task<AssetItemDetailDto> GetItemAsync(int itemId, CancellationToken ct = default);
    Task<AssetItemDetailDto?> FindByCodeAsync(string codeOrQrText, CancellationToken ct = default);
    Task<CreateAssetItemResult> CreateItemAsync(CreateAssetItemRequest request, CancellationToken ct = default);
    Task<AssetItemSaveResult> UpdateItemAsync(int itemId, UpdateAssetItemRequest request, CancellationToken ct = default);
    Task<AssetItemDetailDto> SetActiveAsync(int itemId, bool active, string? reason, CancellationToken ct = default);
    Task<NextCodesDto> NextCodesAsync(string prefix, int count, CancellationToken ct = default);
    Task<SerialCheckDto> CheckSerialAsync(string serial, int? excludeItemId, CancellationToken ct = default);
    Task<List<ItemEventDto>> GetEventsAsync(int itemId, CancellationToken ct = default);
    Task<List<AssetRepairDto>> GetRepairsAsync(int itemId, CancellationToken ct = default);
    Task<AssetItemDetailDto> CloseRepairAsync(int itemId, long repairId, CloseRepairRequest request, CancellationToken ct = default);
    Task<AssetLabelSheetDto> GetLabelsAsync(AssetItemListRequest request, CancellationToken ct = default);
    Task<int> MarkLabelsPrintedAsync(MarkLabelsPrintedRequest request, CancellationToken ct = default);
    Task<AssetFormOptionsDto> GetFormOptionsAsync(CancellationToken ct = default);
    Task<InventoryExportResult?> ExportItemsAsync(AssetItemListRequest request, CancellationToken ct = default);

    // Documents
    Task<List<ItemDocumentDto>> GetDocumentsAsync(int itemId, CancellationToken ct = default);
    Task<ItemDocumentDto> AddDocumentAsync(int itemId, byte documentType, AttachmentUpload upload, CancellationToken ct = default);
    Task<TicketAttachmentContent> GetDocumentAsync(int itemId, long documentId, CancellationToken ct = default);

    // Categories and rooms
    Task<List<AssetCategoryDto>> GetCategoriesAsync(CancellationToken ct = default);
    Task<AssetCategoryDto> SaveCategoryAsync(int? categoryId, SaveAssetCategoryRequest request, CancellationToken ct = default);
    Task<List<AssetLocationDto>> GetLocationsAsync(CancellationToken ct = default);
    Task<AssetLocationDto> SaveLocationAsync(int? locationId, SaveAssetLocationRequest request, CancellationToken ct = default);

    // Incoming Goods
    Task<PagedResult<GoodsReceiptRowDto>> GetReceiptsAsync(GoodsReceiptListRequest request, CancellationToken ct = default);
    Task<GoodsReceiptDto> GetReceiptAsync(long receiptId, CancellationToken ct = default);
    Task<GoodsReceiptDto> CreateReceiptAsync(SaveGoodsReceiptRequest request, CancellationToken ct = default);
    Task<GoodsReceiptDto> UpdateReceiptAsync(long receiptId, SaveGoodsReceiptRequest request, CancellationToken ct = default);
    Task DeleteDraftReceiptAsync(long receiptId, CancellationToken ct = default);
    Task<GoodsReceiptDto> PostReceiptAsync(long receiptId, bool confirmMissingCost, CancellationToken ct = default);
    Task<GoodsReceiptDto> ReverseReceiptAsync(long receiptId, string reason, CancellationToken ct = default);
    Task<GoodsReceiptPreviewDto> PreviewReceiptAsync(SaveGoodsReceiptRequest request, CancellationToken ct = default);
    Task<List<OpenPurchaseOrderDto>> GetOpenPurchaseOrdersAsync(int? supplierId, CancellationToken ct = default);
    Task<InventoryExportResult?> ExportReceiptsAsync(GoodsReceiptListRequest request, CancellationToken ct = default);

    // Outgoing Goods
    Task<PagedResult<GoodsIssueRowDto>> GetIssuesAsync(GoodsIssueListRequest request, CancellationToken ct = default);
    Task<GoodsIssueDto> GetIssueAsync(long issueId, CancellationToken ct = default);
    Task<GoodsIssueResult> CreateIssueAsync(CreateGoodsIssueRequest request, CancellationToken ct = default);
    Task<GoodsIssueDto> ReverseIssueAsync(long issueId, string reason, CancellationToken ct = default);
    Task<GoodsIssuePreviewDto> PreviewIssueAsync(CreateGoodsIssueRequest request, CancellationToken ct = default);
    Task<InventoryExportResult?> ExportIssuesAsync(GoodsIssueListRequest request, CancellationToken ct = default);

    // Current Stock
    Task<StockSheetDto> GetStockAsync(DateOnly? from, DateOnly? to, int? locationId, bool? belowReorder, CancellationToken ct = default);
    Task<List<AssetsByLocationRowDto>> GetAssetsByLocationAsync(CancellationToken ct = default);
    Task<PagedResult<StockLedgerRowDto>> GetLedgerAsync(StockLedgerRequest request, CancellationToken ct = default);
    Task<InventoryExportResult?> ExportStockAsync(DateOnly? from, DateOnly? to, int? locationId, CancellationToken ct = default);

    // Stock counts
    Task<List<StockCountRowDto>> GetStockCountsAsync(CancellationToken ct = default);
    Task<StockCountDto> GetStockCountAsync(long countId, CancellationToken ct = default);
    Task<StockCountDto> StartStockCountAsync(StartStockCountRequest request, CancellationToken ct = default);
    Task<StockCountDto> SubmitStockCountAsync(long countId, SubmitStockCountRequest request, CancellationToken ct = default);
    Task<StockCountDto> ConfirmStockCountAsync(long countId, CancellationToken ct = default);
    Task<StockCountDto> CancelStockCountAsync(long countId, CancellationToken ct = default);

    // Import (§12)
    Task<InventoryExportResult> ImportTemplateAsync(CancellationToken ct = default);
    Task<AssetImportResultDto> ImportAsync(InventoryImportUpload upload, DateOnly? receiptDate, bool save, CancellationToken ct = default);
}
