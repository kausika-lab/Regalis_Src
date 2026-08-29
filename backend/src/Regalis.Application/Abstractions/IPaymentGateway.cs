namespace Regalis.Application.Abstractions;

/// <summary>An order created at the gateway; KeyId is the public key the client
/// checkout needs (Razorpay) — never the secret.</summary>
public record GatewayOrder(string OrderId, string KeyId, decimal Amount, string Currency);

/// <summary>
/// Payment gateway seam (Design Invariant 5). Razorpay in production, Sandbox for
/// demo/dev — selected by Payments:Provider. Signatures are verified server-side
/// by recomputing HMAC-SHA256(orderId|paymentId, secret); a client-reported
/// "payment succeeded" is never trusted (ARCHITECTURE §8).
/// </summary>
public interface IPaymentGateway
{
    /// <summary>"Razorpay" or "Sandbox" — stamped into TRN_Payment.Gateway.</summary>
    string ProviderName { get; }

    Task<GatewayOrder> CreateOrderAsync(decimal amount, string currency, string receipt,
        CancellationToken ct = default);

    bool VerifySignature(string orderId, string paymentId, string signature);
}
