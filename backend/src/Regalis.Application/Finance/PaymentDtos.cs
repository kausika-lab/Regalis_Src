using FluentValidation;

namespace Regalis.Application.Finance;

public record CreatePaymentOrderRequest(long BookingId);

/// <summary>
/// Pay Now against an invoice (document §11). Amount is optional: omitted means the
/// full outstanding balance, which is the ordinary case. A smaller amount is accepted
/// and leaves the invoice PartiallyPaid — the stored model already supports that, so
/// refusing it here would be the engine lying about what it can represent.
/// </summary>
public record CreateInvoicePaymentOrderRequest(long InvoiceId, decimal? Amount);

/// <summary>Settles an invoice payment order after the gateway returns. The signature
/// is recomputed server-side; a client-reported success is never trusted.</summary>
public record VerifyInvoicePaymentRequest(
    string GatewayOrderId,
    string GatewayPaymentId,
    string GatewaySignature);

public record InvoicePaymentResult(
    PaymentDto Payment,
    long InvoiceId,
    string InvoiceNumber,
    decimal PaidAmount,
    decimal OutstandingAmount,
    byte InvoiceStatus);

public class CreateInvoicePaymentOrderRequestValidator
    : AbstractValidator<CreateInvoicePaymentOrderRequest>
{
    public CreateInvoicePaymentOrderRequestValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.Amount).GreaterThan(0)
            .When(x => x.Amount is not null)
            .WithMessage("Enter an amount greater than zero, or leave it blank to pay in full.");
    }
}

public class VerifyInvoicePaymentRequestValidator : AbstractValidator<VerifyInvoicePaymentRequest>
{
    public VerifyInvoicePaymentRequestValidator()
    {
        RuleFor(x => x.GatewayOrderId).NotEmpty().MaximumLength(120);
        RuleFor(x => x.GatewayPaymentId).NotEmpty().MaximumLength(120);
        RuleFor(x => x.GatewaySignature).NotEmpty().MaximumLength(256);
    }
}

public record PaymentOrderResponse(
    long PaymentId,
    string PaymentNumber,
    string GatewayOrderId,
    string KeyId,
    decimal Amount,
    string Currency,
    string Provider);

/// <summary>Sandbox checkout simulation: exchanges an order for the payment id +
/// signature the real gateway would return. Only answers while
/// Payments:Provider = Sandbox — 422 otherwise.</summary>
public record SandboxCompleteRequest(string GatewayOrderId);

public record SandboxCompleteResponse(string GatewayPaymentId, string GatewaySignature);

public class CreatePaymentOrderRequestValidator : AbstractValidator<CreatePaymentOrderRequest>
{
    public CreatePaymentOrderRequestValidator()
    {
        RuleFor(x => x.BookingId).GreaterThan(0);
    }
}

public class SandboxCompleteRequestValidator : AbstractValidator<SandboxCompleteRequest>
{
    public SandboxCompleteRequestValidator()
    {
        RuleFor(x => x.GatewayOrderId).NotEmpty();
    }
}

public record PaymentDto(
    long PaymentId,
    string PaymentNumber,
    long CustomerId,
    string CustomerName,
    long? BookingId,
    long? InvoiceId,
    string? InvoiceNumber,
    decimal? InvoiceAmount,
    long? CustomerPassId,
    decimal Amount,
    byte PaymentMethod,
    string? Gateway,
    string? GatewayOrderId,
    // The gateway transaction id: document §12 calls this the Transaction ID.
    string? GatewayPaymentId,
    decimal? RefundedAmount,
    string? RefundReference,
    bool IsSignatureVerified,
    byte Status,
    DateTime? PaidUtc,
    string? FailureReason,
    DateTime CreatedDate);

public class PaymentListRequest : Shared.Api.PagedRequest
{
    public byte? Status { get; set; }
    public byte? PaymentMethod { get; set; }
    public long? CustomerId { get; set; }
}

public interface IPaymentService
{
    Task<Shared.Api.PagedResult<PaymentDto>> GetPaymentsAsync(PaymentListRequest request,
        CancellationToken ct = default);

    Task<PaymentDto> GetPaymentAsync(long paymentId, CancellationToken ct = default);

    /// <summary>Creates the gateway order for a Pending booking and a Pending
    /// TRN_Payment row carrying the order id.</summary>
    Task<PaymentOrderResponse> CreateOrderAsync(CreatePaymentOrderRequest request,
        CancellationToken ct = default);

    Task<SandboxCompleteResponse> SandboxCompleteAsync(SandboxCompleteRequest request,
        CancellationToken ct = default);

    /// <summary>Gateway webhook: signature-verified, idempotent on GatewayPaymentId.
    /// A safety net behind the inline confirm verification.</summary>
    Task HandleWebhookAsync(string body, string? signature, CancellationToken ct = default);

    /// <summary>Opens a gateway order against an invoice for the amount owed.</summary>
    Task<PaymentOrderResponse> CreateInvoiceOrderAsync(CreateInvoicePaymentOrderRequest request,
        CancellationToken ct = default);

    /// <summary>Verifies the gateway signature and applies the money to the invoice in
    /// one transaction: payment, invoice balance and invoice status move together.</summary>
    Task<InvoicePaymentResult> VerifyInvoicePaymentAsync(VerifyInvoicePaymentRequest request,
        CancellationToken ct = default);

    /// <summary>The payment receipt as a PDF. The payment number is the receipt
    /// reference -- there is no second numbered document.</summary>
    Task<byte[]> GetReceiptPdfAsync(long paymentId, CancellationToken ct = default);
}
