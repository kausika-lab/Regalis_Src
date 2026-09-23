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
    bool IsActive);

public record SaveInventoryItemRequest(
    string ItemCode,
    string ItemName,
    string? Category,
    string Unit,
    decimal OpeningQuantity,
    decimal ReorderLevel,
    decimal? UnitCost,
    int LocationId,
    string? Description);

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
}
