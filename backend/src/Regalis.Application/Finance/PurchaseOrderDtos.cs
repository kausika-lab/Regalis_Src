using FluentValidation;
using Regalis.Application.Operations;
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
    string? Remarks,
    decimal CgstAmount = 0,
    decimal SgstAmount = 0,
    decimal IgstAmount = 0,
    string? AccountCategory = null);

public record PurchaseOrderAttachmentDto(
    long PurchaseOrderAttachmentId,
    string FileName,
    string ContentType,
    long FileSizeBytes,
    DateTime UploadedUtc);

/// <summary>One workflow step: who did what, when, and why.</summary>
public record PurchaseOrderHistoryDto(
    string Action,
    byte? FromStatus,
    byte ToStatus,
    string? ChangedByName,
    DateTime ChangedUtc,
    string? Reason);

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
    // 1 Draft, 2 Sent, 3 PartiallyReceived, 4 Received, 5 Cancelled,
    // 6 PendingApproval, 7 Approved.
    byte Status,
    DateTime? SentUtc,
    DateTime? CancelledUtc,
    string? CancellationReason,
    string? Remarks,
    string? AttachmentPath,
    DateTime CreatedDate,
    List<PurchaseOrderLineDto> Lines,
    string? ReferenceNumber = null,
    string? ShipmentPreference = null,
    bool IsReverseCharge = false,
    string? DeliveryAddress = null,
    bool IsInterState = false,
    byte? DiscountType = null,
    decimal DiscountValue = 0,
    decimal CgstAmount = 0,
    decimal SgstAmount = 0,
    decimal IgstAmount = 0,
    string? TdsCode = null,
    string? TdsLabel = null,
    decimal TdsPercent = 0,
    decimal TdsAmount = 0,
    string? AdjustmentLabel = null,
    decimal AdjustmentAmount = 0,
    string? TermsAndConditions = null,
    DateTime? SubmittedUtc = null,
    DateTime? ApprovedUtc = null,
    string? ApprovedByName = null,
    string? CreatedByName = null,
    string? SupplierEmail = null,
    string? SupplierMobile = null,
    string? SupplierAddress = null,
    List<PurchaseOrderAttachmentDto>? Attachments = null,
    List<PurchaseOrderHistoryDto>? History = null);

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
    string? Remarks,
    string? AccountCategory = null);

/// <summary>Everything after <c>Lines</c> is optional, so a client that only knows the
/// original fields keeps working unchanged.</summary>
public record SavePurchaseOrderRequest(
    DateOnly? PoDate,
    int SupplierId,
    int LocationId,
    DateOnly? ExpectedDeliveryDate,
    string? Remarks,
    List<SavePurchaseOrderLineRequest> Lines,
    string? ReferenceNumber = null,
    string? ShipmentPreference = null,
    bool IsReverseCharge = false,
    string? DeliveryAddress = null,
    // 1 Percentage, 2 Amount; null means no whole-order discount.
    byte? DiscountType = null,
    decimal? DiscountValue = null,
    string? TdsCode = null,
    string? AdjustmentLabel = null,
    decimal? AdjustmentAmount = null,
    // Null on create means "use Purchase.TermsAndConditions".
    string? TermsAndConditions = null);

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

/// <summary>Approve (comment optional) or reject (reason required).</summary>
public record PurchaseOrderDecisionRequest(string? Comment);

public record PurchaseOrderAttachmentContent(byte[] Content, string ContentType, string FileName);

/// <summary>Everything the purchase-order form needs in one call, all from settings
/// and masters so nothing is hard-coded in the client.</summary>
public record PurchaseOrderFormOptionsDto(
    string NextPoNumber,
    bool RequireApproval,
    decimal DefaultGstPercent,
    IReadOnlyList<decimal> GstRateOptions,
    IReadOnlyList<string> ShipmentPreferences,
    IReadOnlyList<string> AccountCategories,
    string? DefaultTermsAndConditions,
    string HomeStateCode,
    IReadOnlyList<QuotationTdsOptionDto> TdsOptions,
    IReadOnlyList<QuotationStateOptionDto> States,
    int MaxAttachments,
    long MaxAttachmentBytes);

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
        RuleFor(x => x.SupplierId).GreaterThan(0).WithMessage("Select a supplier.");
        RuleFor(x => x.LocationId).GreaterThan(0).WithMessage("Select a delivery location.");
        RuleFor(x => x.Remarks).MaximumLength(1000);
        RuleFor(x => x.ReferenceNumber).MaximumLength(80);
        RuleFor(x => x.ShipmentPreference).MaximumLength(60);
        RuleFor(x => x.DeliveryAddress).MaximumLength(600);
        RuleFor(x => x.TdsCode).MaximumLength(20);
        RuleFor(x => x.AdjustmentLabel).MaximumLength(60);
        RuleFor(x => x.TermsAndConditions).MaximumLength(4000);
        RuleFor(x => x.DiscountType).Must(t => t is null or 1 or 2)
            .WithMessage("Discount type must be 1 (percentage) or 2 (amount).");
        RuleFor(x => x.DiscountValue).GreaterThanOrEqualTo(0).When(x => x.DiscountValue is not null);
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100).When(x => x.DiscountType == 1)
            .WithMessage("A percentage discount cannot exceed 100%.");
        RuleFor(x => x.AdjustmentAmount).InclusiveBetween(-10_000_000m, 10_000_000m)
            .When(x => x.AdjustmentAmount is not null);
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
        RuleFor(x => x.AccountCategory).MaximumLength(100);
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

public class PurchaseOrderDecisionRequestValidator : AbstractValidator<PurchaseOrderDecisionRequest>
{
    public PurchaseOrderDecisionRequestValidator()
    {
        RuleFor(x => x.Comment).MaximumLength(500);
    }
}

/// <summary>Purchase orders (document §4). Totals are always recomputed server-side
/// from quantity, rate, discount, TDS and adjustment — a client-sent total is never
/// trusted. A PO is editable only while Draft; from Sent onwards it is frozen and can
/// only be received or cancelled.</summary>
public interface IPurchaseOrderService
{
    Task<PagedResult<PurchaseOrderDto>> GetPurchaseOrdersAsync(PurchaseOrderListRequest request,
        CancellationToken ct = default);

    Task<PurchaseOrderDto> GetPurchaseOrderAsync(long purchaseOrderId, CancellationToken ct = default);

    /// <summary>Settings-driven options for the form, and a preview of the next number.</summary>
    Task<PurchaseOrderFormOptionsDto> GetFormOptionsAsync(CancellationToken ct = default);

    Task<PurchaseOrderDto> CreateAsync(SavePurchaseOrderRequest request, CancellationToken ct = default);

    /// <summary>Draft only — a PO that has been sent to a supplier is frozen.</summary>
    Task<PurchaseOrderDto> UpdateAsync(long purchaseOrderId, SavePurchaseOrderRequest request,
        CancellationToken ct = default);

    /// <summary>Draft to PendingApproval, after the submit-time checks (expected
    /// delivery, terms, a delivery address).</summary>
    Task<PurchaseOrderDto> SubmitAsync(long purchaseOrderId, CancellationToken ct = default);

    /// <summary>PendingApproval to Approved; Draft to Approved when approval is not required.</summary>
    Task<PurchaseOrderDto> ApproveAsync(long purchaseOrderId, string? comment,
        CancellationToken ct = default);

    /// <summary>PendingApproval back to Draft, with the approver's reason.</summary>
    Task<PurchaseOrderDto> RejectAsync(long purchaseOrderId, string reason,
        CancellationToken ct = default);

    /// <summary>Approved to Sent — the point after which the PO is frozen.</summary>
    Task<PurchaseOrderDto> SendAsync(long purchaseOrderId, CancellationToken ct = default);

    /// <summary>Receipt writes the incoming inventory rows and the PO status in one
    /// transaction — stock and the order never disagree.</summary>
    Task<PurchaseOrderDto> ReceiveAsync(long purchaseOrderId, ReceiveGoodsRequest request,
        CancellationToken ct = default);

    Task<PurchaseOrderDto> CancelAsync(long purchaseOrderId, string reason,
        CancellationToken ct = default);

    Task<PurchaseOrderDto> AddAttachmentAsync(long purchaseOrderId, AttachmentUpload upload,
        CancellationToken ct = default);

    Task<PurchaseOrderDto> RemoveAttachmentAsync(long purchaseOrderId, long attachmentId,
        CancellationToken ct = default);

    Task<PurchaseOrderAttachmentContent> GetAttachmentAsync(long purchaseOrderId, long attachmentId,
        CancellationToken ct = default);

    Task<byte[]> GetPdfAsync(long purchaseOrderId, CancellationToken ct = default);
}
