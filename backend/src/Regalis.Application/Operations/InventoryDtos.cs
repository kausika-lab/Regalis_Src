using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Operations;

public record InventoryItemDto(
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    string? Category,
    string Unit,
    decimal OpeningQuantity,
    decimal ReorderLevel,
    decimal? UnitCost,
    int LocationId,
    string? Description,
    decimal CurrentStock,
    bool NeedsReorder,
    bool IsActive,
    // 1 Goods, 2 Service. Services hold no stock and stay off the stock screens.
    byte ItemType = 1,
    // HSN for goods, SAC for services.
    string? HsnSacCode = null,
    decimal SellingRate = 0m,
    decimal TaxPercent = 0m,
    bool IsNonTaxable = true);

public record SaveInventoryItemRequest(
    string ItemCode,
    string ItemName,
    string? Category,
    string Unit,
    decimal OpeningQuantity,
    decimal ReorderLevel,
    decimal? UnitCost,
    int LocationId,
    string? Description,
    // Sales details. Null leaves the stored value alone, so a caller that predates
    // them cannot wipe them; an empty HsnSacCode clears the code.
    byte? ItemType = null,
    string? HsnSacCode = null,
    decimal? SellingRate = null,
    decimal? TaxPercent = null,
    bool? IsNonTaxable = null);

/// <summary>Derived stock (Invariant 11): opening + in + adjustments − out − damage.
/// Never a stored mutable column.</summary>
public record InventoryStockDto(
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    string? Category,
    string Unit,
    decimal OpeningQuantity,
    decimal TotalIn,
    decimal TotalOut,
    decimal TotalDamage,
    decimal TotalAdjustment,
    decimal CurrentStock,
    decimal ReorderLevel,
    bool NeedsReorder);

public record InventoryTransactionDto(
    long InventoryTransactionId,
    string TransactionNumber,
    int InventoryItemId,
    string ItemCode,
    string ItemName,
    string Unit,
    // 1 In, 2 Out, 3 Damage, 4 OpeningBalance, 5 Adjustment.
    byte TransactionType,
    decimal Quantity,
    DateOnly TransactionDate,
    string? SourceOrDestination,
    string? ReferenceNumber,
    int? SupplierId,
    string? SupplierName,
    long? PurchaseOrderId,
    string? PoNumber,
    string? PurchaseInvoiceNumber,
    string? AttachmentPath,
    decimal? UnitCost,
    string? DamageDescription,
    string? DamageCause,
    int LocationId,
    long EnteredByUserId,
    string EnteredByName,
    string? Notes,
    DateTime CreatedDate);

public record CreateInventoryTransactionRequest(
    int InventoryItemId,
    byte TransactionType,
    decimal Quantity,
    DateOnly? TransactionDate,
    string? SourceOrDestination,
    string? ReferenceNumber,
    decimal? UnitCost,
    string? DamageDescription,
    string? DamageCause,
    string? Notes,
    // Appended with defaults so existing positional call sites keep compiling.
    int? SupplierId = null,
    long? PurchaseOrderId = null,
    string? PurchaseInvoiceNumber = null,
    string? AttachmentPath = null);

public class InventoryItemListRequest : PagedRequest
{
    public string? Category { get; set; }
    public int? LocationId { get; set; }
    /// <summary>true = only items at or under their reorder level.</summary>
    public bool? BelowReorder { get; set; }
    /// <summary>1 Goods, 2 Service.</summary>
    public byte? ItemType { get; set; }
}

public class InventoryTransactionListRequest : PagedRequest
{
    public int? InventoryItemId { get; set; }
    public int? SupplierId { get; set; }
    public long? PurchaseOrderId { get; set; }
    public byte? TransactionType { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? LocationId { get; set; }
}

public record InventoryExportResult(byte[] Content, string ContentType, string FileName);

/// <summary>The screens an Excel file can be imported into. Current Stock is not one:
/// stock is derived from the ledger, so it is never imported directly.</summary>
public static class InventoryImportKinds
{
    public const string Items = "items";
    public const string Incoming = "incoming";
    public const string Outgoing = "outgoing";
}

/// <summary>What an Items import does with a code that already exists.</summary>
public static class InventoryImportModes
{
    public const string Skip = "skip";
    public const string Update = "update";
}

/// <summary>An uploaded workbook, as the controller hands it over.</summary>
public record InventoryImportUpload(string FileName, long Length, Stream Content);

/// <summary>One row of the preview. Status is New, Update, Skip or Error; Values are
/// the uploaded cells as text, keyed by column header, so the screen can show them.</summary>
public record InventoryImportRowDto(
    int RowNumber,
    string Status,
    List<string> Errors,
    List<string> Notes,
    Dictionary<string, string?> Values);

/// <summary>The outcome of a preview (Saved = false) or of a save. A save stores every
/// valid row in one database transaction and reports the rest back as errors.</summary>
public record InventoryImportResultDto(
    string Kind,
    List<string> Columns,
    List<string> IgnoredColumns,
    int TotalRows,
    int ValidRows,
    int ErrorRows,
    int SkippedRows,
    int ExistingRows,
    bool Saved,
    int CreatedCount,
    int UpdatedCount,
    List<InventoryImportRowDto> Rows);

public class SaveInventoryItemRequestValidator : AbstractValidator<SaveInventoryItemRequest>
{
    public SaveInventoryItemRequestValidator()
    {
        RuleFor(x => x.ItemCode).NotEmpty().MaximumLength(40);
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.Category).MaximumLength(80);
        RuleFor(x => x.Unit).NotEmpty().MaximumLength(20);
        RuleFor(x => x.OpeningQuantity).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ReorderLevel).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null);
        RuleFor(x => x.LocationId).GreaterThan(0);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.ItemType).InclusiveBetween((byte)1, (byte)2)
            .When(x => x.ItemType is not null)
            .WithMessage("Item type must be Goods or Service.");
        RuleFor(x => x.HsnSacCode).MaximumLength(20)
            .Matches("^[0-9]*$").WithMessage("HSN / SAC code has digits only.");
        RuleFor(x => x.SellingRate).GreaterThanOrEqualTo(0).When(x => x.SellingRate is not null);
        RuleFor(x => x.TaxPercent).InclusiveBetween(0, 100).When(x => x.TaxPercent is not null);
    }
}

public class CreateInventoryTransactionRequestValidator : AbstractValidator<CreateInventoryTransactionRequest>
{
    public CreateInventoryTransactionRequestValidator()
    {
        RuleFor(x => x.InventoryItemId).GreaterThan(0);
        RuleFor(x => x.TransactionType).InclusiveBetween((byte)1, (byte)5);
        // Adjustment may be negative (a compensating entry); everything else is positive.
        RuleFor(x => x.Quantity).GreaterThan(0)
            .When(x => x.TransactionType != 5)
            .WithMessage("Quantity must be greater than zero.");
        RuleFor(x => x.Quantity).NotEqual(0)
            .When(x => x.TransactionType == 5)
            .WithMessage("An adjustment must move the stock — zero does nothing.");
        RuleFor(x => x.UnitCost).GreaterThanOrEqualTo(0).When(x => x.UnitCost is not null);
        RuleFor(x => x.SourceOrDestination).MaximumLength(150);
        RuleFor(x => x.ReferenceNumber).MaximumLength(80);
        RuleFor(x => x.DamageDescription).MaximumLength(500);
        RuleFor(x => x.DamageCause).MaximumLength(250);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.DamageDescription).NotEmpty()
            .When(x => x.TransactionType == 3)
            .WithMessage("Describe the damage — the ledger is the audit history.");
        RuleFor(x => x.SupplierId).GreaterThan(0).When(x => x.SupplierId is not null);
        RuleFor(x => x.PurchaseOrderId).GreaterThan(0).When(x => x.PurchaseOrderId is not null);
        RuleFor(x => x.PurchaseInvoiceNumber).MaximumLength(80);
        RuleFor(x => x.AttachmentPath).MaximumLength(500);
        // Supplier provenance belongs to goods coming in, not going out.
        RuleFor(x => x.SupplierId).Null()
            .When(x => x.TransactionType is 2 or 3)
            .WithMessage("A supplier applies to incoming goods, not to an issue or damage row.");
    }
}

/// <summary>The inventory module (M19). The ledger is append-only: there is no update
/// or delete endpoint; a mistake is corrected with a compensating Adjustment row.</summary>
public interface IInventoryService
{
    Task<PagedResult<InventoryItemDto>> GetItemsAsync(InventoryItemListRequest request,
        CancellationToken ct = default);

    Task<InventoryItemDto> GetItemAsync(int itemId, CancellationToken ct = default);

    Task<InventoryItemDto> CreateItemAsync(SaveInventoryItemRequest request,
        CancellationToken ct = default);

    Task<InventoryItemDto> UpdateItemAsync(int itemId, SaveInventoryItemRequest request,
        CancellationToken ct = default);

    Task DeleteItemAsync(int itemId, CancellationToken ct = default);

    Task<List<InventoryStockDto>> GetStockAsync(int? locationId, bool? belowReorder,
        CancellationToken ct = default);

    Task<InventoryTransactionDto> CreateTransactionAsync(CreateInventoryTransactionRequest request,
        CancellationToken ct = default);

    Task<PagedResult<InventoryTransactionDto>> GetTransactionsAsync(
        InventoryTransactionListRequest request, CancellationToken ct = default);

    /// <summary>CSV of the transaction ledger for the filter window.</summary>
    Task<InventoryExportResult> ExportAsync(InventoryTransactionListRequest request,
        CancellationToken ct = default);

    /// <summary>The item master as an .xlsx workbook, filtered like the Items screen.
    /// Null when no item matches.</summary>
    Task<InventoryExportResult?> ExportItemsExcelAsync(InventoryItemListRequest request,
        CancellationToken ct = default);

    /// <summary>Incoming (In) or Outgoing (Out) goods as .xlsx, filtered like the screen.
    /// Null when no row matches.</summary>
    Task<InventoryExportResult?> ExportGoodsExcelAsync(InventoryTransactionListRequest request,
        byte transactionType, CancellationToken ct = default);

    /// <summary>The Current Stock screen as .xlsx. Null when no row matches.</summary>
    Task<InventoryExportResult?> ExportStockExcelAsync(int? locationId, bool? belowReorder,
        CancellationToken ct = default);

    /// <summary>The empty import workbook: headers, an example row and instructions.</summary>
    Task<InventoryExportResult> ImportTemplateAsync(string kind, CancellationToken ct = default);

    /// <summary>Checks every row of an upload. With <paramref name="save"/> it checks again
    /// inside one transaction and stores the valid rows.</summary>
    Task<InventoryImportResultDto> ImportAsync(string kind, InventoryImportUpload upload,
        string? existingMode, bool save, CancellationToken ct = default);

    /// <summary>The rows of an upload that fail the checks, with an Error column.</summary>
    Task<InventoryExportResult> ImportErrorReportAsync(string kind, InventoryImportUpload upload,
        string? existingMode, CancellationToken ct = default);
}
