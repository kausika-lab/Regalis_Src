using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Application.Operations;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>
/// Quotations (document §6–§8). Both portals are served here: the admin routes manage
/// the quotation, the "my" routes are the customer's own view and their approve/reject
/// decision. Customer routes are scoped by ICurrentUser inside the service, so the id
/// in the URL can never reach another customer's quotation.
///
/// Every state rule the screen shows as a greyed-out button is enforced again here:
/// a sent quotation cannot be edited, only the newest revision can be approved, and a
/// quotation converts to an invoice once and only from Approved.
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

    /// <summary>The last rates an item from Assets > Items was quoted at (newest
    /// first), for the Rate column's Recent Transactions popup.</summary>
    [HttpGet("item-rates")]
    [HasPermission(PermissionCodes.Quotation.View)]
    [ProducesResponseType(typeof(ApiResponse<List<QuotationItemRateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItemRates([FromQuery] int inventoryItemId,
        [FromQuery] int take = 5, CancellationToken ct = default) =>
        OkEnvelope(await quotationService.GetItemRatesAsync(inventoryItemId, take, ct));

    /// <summary>How many quotations each status tab holds under the list's current
    /// filters (search, customer, service and date). The status itself is ignored.</summary>
    [HttpGet("status-counts")]
    [HasPermission(PermissionCodes.Quotation.View)]
    [ProducesResponseType(typeof(ApiResponse<QuotationStatusCountsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatusCounts([FromQuery] QuotationListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.GetStatusCountsAsync(request, ct));

    /// <summary>The four summary cards and the status tab counts (document §3.1).</summary>
    [HttpGet("summary")]
    [HasPermission(PermissionCodes.Quotation.View)]
    [ProducesResponseType(typeof(ApiResponse<QuotationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(CancellationToken ct) =>
        OkEnvelope(await quotationService.GetSummaryAsync(ct));

    /// <summary>Services, tax options and defaults for the new-quotation form (§3.2).</summary>
    [HttpGet("form-options")]
    [HasPermission(PermissionCodes.Quotation.Create)]
    [ProducesResponseType(typeof(ApiResponse<QuotationFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFormOptions(CancellationToken ct) =>
        OkEnvelope(await quotationService.GetFormOptionsAsync(ct));

    /// <summary>The filtered list as a CSV download (document §3.1).</summary>
    [HttpGet("export")]
    [HasPermission(PermissionCodes.Quotation.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] QuotationListRequest request,
        CancellationToken ct) =>
        File(await quotationService.ExportAsync(request, ct), "text/csv",
            $"quotations-{DateTime.UtcNow:yyyyMMdd}.csv");

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
            "Quotation saved as a draft.");
    }

    /// <summary>Draft only — a quotation the customer has seen is frozen.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.Quotation.Edit)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, SaveQuotationRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.UpdateAsync(id, request, ct), "Quotation updated.");

    /// <summary>Draft only. The number it used is not handed out again (§5.3).</summary>
    [HttpDelete("{id:long}")]
    [HasPermission(PermissionCodes.Quotation.Delete)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await quotationService.DeleteAsync(id, ct);
        return OkEnvelope("Quotation deleted.");
    }

    /// <summary>Snapshots the customer, company and tax details, freezes the PDF and
    /// delivers it by Email and/or WhatsApp (document §3.5).</summary>
    [HttpPost("{id:long}/send")]
    [HasPermission(PermissionCodes.Quotation.Edit)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(long id, SendQuotationRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.SendAsync(id, request, ct),
            "Quotation sent to the customer.");

    /// <summary>Opens a new draft …-R1 and marks this one Revised (document §1).</summary>
    [HttpPost("{id:long}/revise")]
    [HasPermission(PermissionCodes.Quotation.Create)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Revise(long id, ReviseQuotationRequest request,
        CancellationToken ct)
    {
        var revision = await quotationService.ReviseAsync(id, request, ct);
        return CreatedEnvelope($"/api/quotations/{revision.QuotationId}", revision,
            $"Revision {revision.QuotationNumber} created as a draft.");
    }

    [HttpPost("{id:long}/clone")]
    [HasPermission(PermissionCodes.Quotation.Create)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Clone(long id, CancellationToken ct)
    {
        var clone = await quotationService.CloneAsync(id, ct);
        return CreatedEnvelope($"/api/quotations/{clone.QuotationId}", clone,
            $"Cloned as {clone.QuotationNumber}.");
    }

    /// <summary>An admin recording the customer's decision from a call or a visit
    /// (document §3.6). The record says it was taken on their behalf.</summary>
    [HttpPost("{id:long}/record-decision")]
    [HasPermission(PermissionCodes.Quotation.Edit)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RecordDecision(long id,
        RecordQuotationDecisionRequest request, CancellationToken ct) =>
        OkEnvelope(await quotationService.RecordDecisionAsync(id, request, ct),
            request.Approved ? "Recorded as approved." : "Recorded as rejected.");

    [HttpPost("{id:long}/cancel")]
    [HasPermission(PermissionCodes.Quotation.Delete)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long id, CancelQuotationRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.CancelAsync(id, request.Reason, ct),
            "Quotation cancelled.");

    /// <summary>Admin-only note. Never shown to the customer and never printed.</summary>
    [HttpPut("{id:long}/internal-note")]
    [HasPermission(PermissionCodes.Quotation.Edit)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetInternalNote(long id, QuotationNoteRequest request,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.AddInternalNoteAsync(id, request.Note, ct),
            "Note saved.");

    // ── Attachments (document §3.4) ─────────────────────────────────────────

    /// <summary>Multipart upload, ≤10 MB, .pdf .jpg .jpeg .png .docx .xlsx, 5 per
    /// quotation — content-sniffed, as ticket attachments are.</summary>
    [HttpPost("{id:long}/attachments")]
    [HasPermission(PermissionCodes.Quotation.Edit)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddAttachment(long id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var quotation = await quotationService.AddAttachmentAsync(id,
            new AttachmentUpload(file.FileName, file.ContentType, file.Length, stream), ct);
        return OkEnvelope(quotation, "Attachment added.");
    }

    /// <summary>Toggles the chip's "Send with email" checkbox.</summary>
    [HttpPatch("{id:long}/attachments/{attachmentId:long}")]
    [HasPermission(PermissionCodes.Quotation.Edit)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAttachment(long id, long attachmentId,
        UpdateQuotationAttachmentRequest request, CancellationToken ct) =>
        OkEnvelope(await quotationService.UpdateAttachmentAsync(id, attachmentId,
            request.SendWithEmail, ct), "Attachment updated.");

    [HttpDelete("{id:long}/attachments/{attachmentId:long}")]
    [HasPermission(PermissionCodes.Quotation.Edit)]
    [ProducesResponseType(typeof(ApiResponse<QuotationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveAttachment(long id, long attachmentId,
        CancellationToken ct) =>
        OkEnvelope(await quotationService.RemoveAttachmentAsync(id, attachmentId, ct),
            "Attachment removed.");

    /// <summary>Scoped inside the service: a customer can only read their own.</summary>
    [HttpGet("{id:long}/attachments/{attachmentId:long}")]
    [HasPermission(PermissionCodes.Quotation.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadAttachment(long id, long attachmentId,
        CancellationToken ct)
    {
        var attachment = await quotationService.GetAttachmentAsync(id, attachmentId, ct);
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    // ── Both portals ────────────────────────────────────────────────────────

    /// <summary>Scoped inside the service: a customer can only print their own. Once
    /// sent, this is the frozen file, not a fresh render (document §3.8).</summary>
    [HttpGet("{id:long}/pdf")]
    [HasPermission(PermissionCodes.Quotation.Print)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPdf(long id, CancellationToken ct)
    {
        var pdf = await quotationService.GetQuotationPdfAsync(id, ct);
        return File(pdf, "application/pdf", $"quotation-{id}.pdf");
    }
}
