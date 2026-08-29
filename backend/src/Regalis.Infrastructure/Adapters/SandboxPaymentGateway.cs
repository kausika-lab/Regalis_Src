using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Regalis.Application.Abstractions;

namespace Regalis.Infrastructure.Adapters;

/// <summary>
/// Sandbox gateway (Payments:Provider = "Sandbox"): lets the entire payment flow be
/// exercised before Razorpay keys arrive (A12). Orders are local; signatures are the
/// same HMAC-SHA256(orderId|paymentId) scheme Razorpay uses, keyed with a local
/// secret, so the verification path under test is identical to production.
/// </summary>
public class SandboxPaymentGateway(IConfiguration configuration) : IPaymentGateway
{
    public const string Name = "Sandbox";

    private readonly string _secret =
        configuration["Payments:Sandbox:Secret"] ?? "regalis-sandbox-not-a-real-secret";

    public string ProviderName => Name;

    public Task<GatewayOrder> CreateOrderAsync(decimal amount, string currency, string receipt,
        CancellationToken ct = default) =>
        Task.FromResult(new GatewayOrder(
            $"order_SBX{Convert.ToHexString(RandomNumberGenerator.GetBytes(8))}",
            "rzp_test_sandbox", amount, currency));

    public bool VerifySignature(string orderId, string paymentId, string signature) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(Sign(orderId, paymentId)),
            Encoding.UTF8.GetBytes(signature));

    /// <summary>Used by the sandbox checkout simulation to produce a valid signature —
    /// exposed on the adapter, never over an API in production configuration.</summary>
    public string Sign(string orderId, string paymentId) =>
        Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_secret),
            Encoding.UTF8.GetBytes($"{orderId}|{paymentId}"))).ToLowerInvariant();
}
