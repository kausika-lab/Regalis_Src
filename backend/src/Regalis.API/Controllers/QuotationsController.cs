using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>
/// Quotations (document §6–§8). Both portals are served here: the admin routes manage
/// the quotation, the "my" routes are the customer's own view and their approve/reject
/// decision. Customer routes are scoped by ICurrentUser inside the service, so the id
/// in the URL can never reach another customer's quotation.
/// </summary>
[Route("api/quotations")]
public class QuotationsController(IQuotationService quotationService) : ApiControllerBase
{
    // ── Customer portal ─────────────────────────────────────────────────────

    /// <summary>The caller's own quotations. Drafts are never included.</summary>
    [HttpGet("my")]
    [HasPermission(PermissionCodes.Quotation.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<QuotationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy([FromQuery] QuotationListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.GetMyQuotationsAsync(request, ct));

    /// <summary>Opening a sent quotation marks it Viewed.</summary>
    [HttpGet("my/{id:long}")]
    [HasPermission(PermissionCodes.Quotation.View)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyById(long id, CancellationToken ct) =>
        OkEnvelope(await quotationService.GetMyQuotationAsync(id, ct));

    [HttpPost("{id:long}/approve")]
    [HasPermission(PermissionCodes.Quotation.Approve)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(long id, QuotationDecisionRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.ApproveAsync(id, request, ct), "Quotation approved.");

    [HttpPost("{id:long}/reject")]
    [HasPermission(PermissionCodes.Quotation.Approve)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(long id, QuotationDecisionRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.RejectAsync(id, request, ct), "Quotation rejected.");

    // ── Admin portal ────────────────────────────────────────────────────────

    [HttpGet]
    [HasPermission(PermissionCodes.Quotation.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<QuotationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] QuotationListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.GetQuotationsAsync(request, ct));

    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Quotation.View)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) =>
        OkEnvelope(await quotationService.GetQuotationAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Quotation.Create)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveQuotationRequest request, CancellationToken ct)
    {
        var quotation = await quotationService.CreateAsync(request, ct);
        return CreatedEnvelope($"/api/quotations/{quotation.QuotationId}", quotation,
            "Quotation created.");
    }

    /// <summary>Draft only — a quotation the customer has seen is frozen.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.Quotation.Edit)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, SaveQuotationRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.UpdateAsync(id, request, ct), "Quotation updated.");

    /// <summary>Snapshots the customer, company and tax details, then makes the
    /// quotation visible in the customer portal.</summary>
    [HttpPost("{id:long}/send")]
    [HasPermission(PermissionCodes.Quotation.Edit)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(long id, CancellationToken ct) =>
        OkEnvelope(await quotationService.SendAsync(id, ct), "Quotation sent to the customer.");

    [HttpPost("{id:long}/cancel")]
    [HasPermission(PermissionCodes.Quotation.Delete)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long id, CancelQuotationRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.CancelAsync(id, request.Reason, ct), "Quotation cancelled.");

    // ── Both portals ────────────────────────────────────────────────────────

    /// <summary>Scoped inside the service: a customer can only print their own.</summary>
    [HttpGet("{id:long}/pdf")]
    [HasPermission(PermissionCodes.Quotation.Print)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPdf(long id, CancellationToken ct)
    {
        var pdf = await quotationService.GetQuotationPdfAsync(id, ct);
        return File(pdf, "application/pdf", $"quotation-{id}.pdf");
    }
}

/// <summary>Body for the cancel route — a reason is mandatory, as on a purchase order.</summary>
public record CancelQuotationRequest(string Reason);
