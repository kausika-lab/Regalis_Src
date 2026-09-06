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
    /// <summary>Payments grid (admin/finance).</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.Payment.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PaymentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PaymentListRequest request, CancellationToken ct)
    {
        if (User.FindFirst("user_type")?.Value == "1")
        {
            request.CustomerId = long.Parse(User.FindFirst("sub")!.Value);
        }
        return OkEnvelope(await paymentService.GetPaymentsAsync(request, ct));
    }

    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Payment.View)]
    [ProducesResponseType(typeof(ApiResponse<PaymentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) =>
        OkEnvelope(await paymentService.GetPaymentAsync(id, ct));

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

    /// <summary>Pay Now against an invoice (document �11). Omit the amount to pay the
    /// full outstanding balance.</summary>
    [HttpPost("invoice-order")]
    [HasPermission(PermissionCodes.Payment.Create)]
    [ProducesResponseType(typeof(ApiResponse<PaymentOrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateInvoiceOrder(CreateInvoicePaymentOrderRequest request,
        CancellationToken ct) =>
        OkEnvelope(await paymentService.CreateInvoiceOrderAsync(request, ct));

    /// <summary>Settles the invoice order once the gateway returns. The signature is
    /// recomputed here; a failure leaves the invoice unpaid and retryable.</summary>
    [HttpPost("invoice-verify")]
    [HasPermission(PermissionCodes.Payment.Create)]
    [ProducesResponseType(typeof(ApiResponse<InvoicePaymentResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyInvoicePayment(VerifyInvoicePaymentRequest request,
        CancellationToken ct) =>
        OkEnvelope(await paymentService.VerifyInvoicePaymentAsync(request, ct), "Payment received.");

    /// <summary>The receipt for a successful payment. Scoped: a customer gets only
    /// their own.</summary>
    [HttpGet("{id:long}/receipt")]
    [HasPermission(PermissionCodes.Payment.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetReceipt(long id, CancellationToken ct)
    {
        var pdf = await paymentService.GetReceiptPdfAsync(id, ct);
        return File(pdf, "application/pdf", $"receipt-{id}.pdf");
    }
}
