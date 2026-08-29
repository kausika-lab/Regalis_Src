using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Adapters;

/// <summary>
/// Razorpay adapter (Payments:Provider = "Razorpay"). Orders via the REST API with
/// basic auth; signature verification recomputes HMAC-SHA256(orderId|paymentId,
/// key_secret) server-side — a client-reported "payment succeeded" is never trusted
/// (ARCHITECTURE §8). Keys come from configuration/user-secrets only.
/// </summary>
public class RazorpayPaymentGateway(HttpClient httpClient, IConfiguration configuration) : IPaymentGateway
{
    public const string Name = "Razorpay";

    private readonly string _keyId = configuration["Payments:Razorpay:KeyId"] ?? string.Empty;
    private readonly string _keySecret = configuration["Payments:Razorpay:KeySecret"] ?? string.Empty;

    public string ProviderName => Name;

    public async Task<GatewayOrder> CreateOrderAsync(decimal amount, string currency, string receipt,
        CancellationToken ct = default)
    {
        EnsureConfigured();
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.razorpay.com/v1/orders");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_keyId}:{_keySecret}")));
        request.Content = JsonContent.Create(new
        {
            amount = (long)Math.Round(amount * 100m, 0), // paise
            currency,
            receipt
        });

        using var response = await httpClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new BusinessRuleException(
                $"The payment gateway rejected the order ({(int)response.StatusCode}).",
                ErrorCodes.PaymentVerificationFailed)
            {
                // body intentionally not exposed to the client; logged by middleware
                Data = { ["GatewayResponse"] = body }
            };
        }
        var order = await response.Content.ReadFromJsonAsync<RazorpayOrderResponse>(ct)
                    ?? throw new BusinessRuleException("Empty gateway response.",
                        ErrorCodes.PaymentVerificationFailed);
        return new GatewayOrder(order.Id, _keyId, amount, currency);
    }

    public bool VerifySignature(string orderId, string paymentId, string signature)
    {
        EnsureConfigured();
        var expected = Convert.ToHexString(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_keySecret),
            Encoding.UTF8.GetBytes($"{orderId}|{paymentId}"))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrEmpty(_keyId) || string.IsNullOrEmpty(_keySecret))
        {
            throw new BusinessRuleException(
                "Razorpay keys are not configured. Set Payments:Razorpay:KeyId/KeySecret " +
                "or switch Payments:Provider to Sandbox.",
                ErrorCodes.PaymentVerificationFailed);
        }
    }

    private sealed record RazorpayOrderResponse([property: JsonPropertyName("id")] string Id);
}
