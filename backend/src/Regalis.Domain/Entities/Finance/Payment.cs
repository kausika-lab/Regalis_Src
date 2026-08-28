using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_Payment. Gateway payments are verified server-side by recomputing the
/// HMAC signature — a client-reported "payment succeeded" is never trusted.</summary>
public class Payment : AuditableEntity
{
    public long PaymentId { get; set; }
    public string PaymentNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public long? BookingId { get; set; }
    public long? InvoiceId { get; set; }
    public long? CustomerPassId { get; set; }
    public long? ServiceOrderId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    /// <summary>"Razorpay" / "Sandbox".</summary>
    public string? Gateway { get; set; }
    public string? GatewayOrderId { get; set; }
    /// <summary>Filtered unique index makes gateway webhooks idempotent.</summary>
    public string? GatewayPaymentId { get; set; }
    public string? GatewaySignature { get; set; }
    public bool IsSignatureVerified { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;
    public DateTime? PaidUtc { get; set; }
    public string? FailureReason { get; set; }
    public decimal? RefundedAmount { get; set; }
    public string? RefundReference { get; set; }
    public string? RawGatewayResponse { get; set; }
}
