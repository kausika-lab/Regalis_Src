using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Finance;

public record PurchaseOrderLineDto(
    long PurchaseOrderLineId,
    int LineNumber,
    int? InventoryItemId,
    string? ItemCode,
    string ItemName,
    string? HsnSacCode,
    string? Unit,
    decimal Quantity,
    decimal ReceivedQuantity,
    decimal PendingQuantity,
    decimal Rate,
    decimal DiscountAmount,
    decimal LineAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal TotalAmount,
    string? Remarks);

public record PurchaseOrderDto(
    long PurchaseOrderId,
    string PoNumber,
    DateOnly PoDate,
    int SupplierId,
    string SupplierName,
    string? SupplierGstin,
    int LocationId,
    string? LocationName,
    DateOnly? ExpectedDeliveryDate,
    decimal SubTotal,
    decimal DiscountAmount,
    decimal TaxableAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    // 1 Draft, 2 Sent, 3 PartiallyReceived, 4 Received, 5 Cancelled.
    byte Status,
    DateTime? SentUtc,
    DateTime? CancelledUtc,
    string? CancellationReason,
    string? Remarks,
    string? AttachmentPath,
    DateTime CreatedDate,
    List<PurchaseOrderLineDto> Lines);

public record SavePurchaseOrderLineRequest(
    int? InventoryItemId,
    string ItemName,
    string? HsnSacCode,
    string? Unit,
    decimal Quantity,
    decimal Rate,
    decimal DiscountAmount,
    // The supplier GST rate as printed on their quote. This is the vendor output
    // tax, not ours, so it is entered rather than read from the Tax.* settings.
    decimal TaxPercent,
    string? Remarks);

public record SavePurchaseOrderRequest(
    DateOnly? PoDate,
    int SupplierId,
    int LocationId,
    DateOnly? ExpectedDeliveryDate,
    string? Remarks,
    List<SavePurchaseOrderLineRequest> Lines);

public record ReceiveGoodsLineRequest(
    long PurchaseOrderLineId,
    decimal ReceivedQuantity);

/// <summary>Goods received against a purchase order. Each line writes one incoming
/// TRN_InventoryTransaction, and the PO status follows from the received totals —
/// there is no separate "IncomingGoods" table, because stock is derived from the one
/// append-only ledger (Invariant 11).</summary>
public record ReceiveGoodsRequest(
    DateOnly? ReceivedDate,
    string? PurchaseInvoiceNumber,
    string? AttachmentPath,
    string? Notes,
    List<ReceiveGoodsLineRequest> Lines);

public class PurchaseOrderListRequest : PagedRequest
{
    public byte? Status { get; set; }
    public int? SupplierId { get; set; }
    public int? LocationId { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
}

public class SavePurchaseOrderRequestValidator : AbstractValidator<SavePurchaseOrderRequest>
{
    public SavePurchaseOrderRequestValidator()
    {
        RuleFor(x => x.SupplierId).GreaterThan(0);
        RuleFor(x => x.LocationId).GreaterThan(0);
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("A purchase order needs at least one line.");
        RuleForEach(x => x.Lines).SetValidator(new SavePurchaseOrderLineRequestValidator());
        RuleFor(x => x)
            .Must(x => x.ExpectedDeliveryDate is null || x.PoDate is null
                       || x.ExpectedDeliveryDate >= x.PoDate)
            .WithMessage("Expected delivery cannot be before the PO date.")
            .WithName(nameof(SavePurchaseOrderRequest.ExpectedDeliveryDate));
    }
}

public class SavePurchaseOrderLineRequestValidator : AbstractValidator<SavePurchaseOrderLineRequest>
{
    public SavePurchaseOrderLineRequestValidator()
    {
        RuleFor(x => x.ItemName).NotEmpty().MaximumLength(300);
        RuleFor(x => x.HsnSacCode).MaximumLength(20);
        RuleFor(x => x.Unit).MaximumLength(20);
        RuleFor(x => x.Quantity).GreaterThan(0);
        RuleFor(x => x.Rate).GreaterThanOrEqualTo(0);
        RuleFor(x => x.DiscountAmount).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TaxPercent).InclusiveBetween(0, 100);
        RuleFor(x => x.InventoryItemId).GreaterThan(0).When(x => x.InventoryItemId is not null);
        RuleFor(x => x.Remarks).MaximumLength(500);
        RuleFor(x => x)
            .Must(x => x.DiscountAmount <= x.Quantity * x.Rate)
            .WithMessage("Line discount cannot exceed the line value.")
            .WithName(nameof(SavePurchaseOrderLineRequest.DiscountAmount));
    }
}

public class ReceiveGoodsRequestValidator : AbstractValidator<ReceiveGoodsRequest>
{
    public ReceiveGoodsRequestValidator()
    {
        RuleFor(x => x.PurchaseInvoiceNumber).MaximumLength(80);
        RuleFor(x => x.AttachmentPath).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotEmpty().WithMessage("Record at least one received line.");
        RuleForEach(x => x.Lines).ChildRules(line =>
        {
            line.RuleFor(l => l.PurchaseOrderLineId).GreaterThan(0);
            line.RuleFor(l => l.ReceivedQuantity).GreaterThan(0)
                .WithMessage("Received quantity must be greater than zero.");
        });
    }
}

/// <summary>Purchase orders (document §4). Totals are always recomputed server-side
/// from quantity, rate, discount and the supplier's tax percent — a client-sent total
/// is never trusted. A PO is editable only while Draft.</summary>
public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderDto>> GetPurchaseOrdersAsync(PurchaseOrderListRequest request,
        CancellationToken ct = default);

    Task<PurchaseOrderDto> GetPurchaseOrderAsync(long purchaseOrderId, CancellationToken ct = default);

    Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request, CancellationToken ct = default);

    /// <summary>Draft only — a PO that has been sent to a supplier is frozen.</summary>
    Task<PurchaseOrderDto> UpdateAsync(long purchaseOrderId, SavePurchaseOrderRequest request,
        CancellationToken ct = default);

    /// <summary>Draft to Sent. Recorded with a timestamp so the supplier copy is dated.</summary>
    Task<PurchaseOrderDto> SendAsync(long purchaseOrderId, CancellationToken ct = default);

    /// <summary>Receipt writes the incoming inventory rows and the PO status in one
    /// transaction — stock and the order never disagree.</summary>
    Task<PurchaseOrderDto> ReceiveAsync(long purchaseOrderId, ReceiveGoodsRequest request,
        CancellationToken ct = default);

    Task<PurchaseOrderDto> CancelAsync(long purchaseOrderId, string reason,
        CancellationToken ct = default);
}
