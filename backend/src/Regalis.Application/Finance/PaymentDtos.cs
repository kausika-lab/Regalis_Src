using FluentValidation;

namespace Regalis.Application.Finance;

public record CreatePaymentOrderRequest(long BookingId);

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
    long? CustomerPassId,
    decimal Amount,
    byte PaymentMethod,
    string? Gateway,
    string? GatewayOrderId,
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
}
