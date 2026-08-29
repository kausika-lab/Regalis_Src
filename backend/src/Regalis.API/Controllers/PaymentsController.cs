using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Payment orders + gateway webhook (API_PLAN §7).</summary>
[Route("api/payments")]
public class PaymentsController(IPaymentService paymentService) : ApiControllerBase
{
    /// <summary>Creates the gateway order for a Pending booking. The client then
    /// runs checkout and confirms the booking with the returned credentials.</summary>
    [HttpPost("order")]
    [HasPermission(PermissionCodes.Payment.Create)]
    [ProducesResponseType(typeof(ApiResponse<PaymentOrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOrder(CreatePaymentOrderRequest request, CancellationToken ct) =>
        OkEnvelope(await paymentService.CreateOrderAsync(request, ct));

    /// <summary>Simulates the gateway checkout — Sandbox provider only.</summary>
    [HttpPost("sandbox-complete")]
    [HasPermission(PermissionCodes.Payment.Create)]
    [ProducesResponseType(typeof(ApiResponse<SandboxCompleteResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SandboxComplete(SandboxCompleteRequest request, CancellationToken ct) =>
        OkEnvelope(await paymentService.SandboxCompleteAsync(request, ct));

    /// <summary>Gateway webhook: anonymous, signature-verified, idempotent.</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    [EnableRateLimiting("webhook")]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);
        await paymentService.HandleWebhookAsync(
            body, Request.Headers["X-Razorpay-Signature"].FirstOrDefault(), ct);
        return OkEnvelope("Acknowledged.");
    }
}
