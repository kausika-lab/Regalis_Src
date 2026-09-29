using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Application.Operations;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Purchase orders (document §4). Draft → PendingApproval → Approved → Sent →
/// (Partially) Received, Cancelled until goods arrive. A PO is editable only while
/// Draft, and receiving goods against it writes the incoming inventory rows in the same
/// transaction — there is no separate goods-received endpoint that could drift.
///
/// Permissions: VIEW reads, prints and downloads; CREATE raises, attaches files and
/// submits; EDIT changes a draft and records receipts; APPROVE approves, rejects and
/// sends; DELETE cancels.</summary>
[Route("api/purchase-orders")]
public class PurchaseOrdersController(IPurchaseOrderService purchaseOrderService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Purchase.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PurchaseOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PurchaseOrderListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.GetPurchaseOrdersAsync(request, ct));

    /// <summary>Settings-driven options for the form: GST rates, TDS sections, shipment
    /// methods, account heads, default terms and the next PO number.</summary>
    [HttpGet("form-options")]
    [HasPermission(PermissionCodes.Purchase.View)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFormOptions(CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.GetFormOptionsAsync(ct));

    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Purchase.View)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.GetPurchaseOrderAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Purchase.Create)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SavePurchaseOrderRequest request, CancellationToken ct)
    {
        var order = await purchaseOrderService.CreateAsync(request, ct);
        return CreatedEnvelope($"/api/purchase-orders/{order.PurchaseOrderId}", order,
            "Purchase order created.");
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.Purchase.Edit)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, SavePurchaseOrderRequest request,
        CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.UpdateAsync(id, request, ct), "Purchase order updated.");

    /// <summary>Draft to PendingApproval.</summary>
    [HttpPost("{id:long}/submit")]
    [HasPermission(PermissionCodes.Purchase.Create)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(long id, CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.SubmitAsync(id, ct), "Submitted for approval.");

    /// <summary>PendingApproval to Approved (or Draft to Approved when approval is off).</summary>
    [HttpPost("{id:long}/approve")]
    [HasPermission(PermissionCodes.Purchase.Approve)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(long id, PurchaseOrderDecisionRequest? request,
        CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.ApproveAsync(id, request?.Comment, ct),
            "Purchase order approved.");

    /// <summary>PendingApproval back to Draft; a reason is mandatory.</summary>
    [HttpPost("{id:long}/reject")]
    [HasPermission(PermissionCodes.Purchase.Approve)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(long id, PurchaseOrderDecisionRequest request,
        CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.RejectAsync(id, request.Comment ?? "", ct),
            "Purchase order sent back to draft.");

    /// <summary>Approved to Sent — the point after which the PO is frozen.</summary>
    [HttpPost("{id:long}/send")]
    [HasPermission(PermissionCodes.Purchase.Approve)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(long id, CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.SendAsync(id, ct), "Purchase order sent.");

    /// <summary>Goods received: writes the incoming TRN_InventoryTransaction rows, the
    /// line receipts and the header status atomically.</summary>
    [HttpPost("{id:long}/receive")]
    [HasPermission(PermissionCodes.Purchase.Edit)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Receive(long id, ReceiveGoodsRequest request,
        CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.ReceiveAsync(id, request, ct), "Goods received.");

    [HttpPost("{id:long}/cancel")]
    [HasPermission(PermissionCodes.Purchase.Delete)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long id, CancelPurchaseOrderRequest request,
        CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.CancelAsync(id, request.Reason, ct),
            "Purchase order cancelled.");

    // ── Attachments ─────────────────────────────────────────────────────────

    /// <summary>Multipart upload, ≤10 MB, .pdf .jpg .jpeg .png .docx .xlsx, 10 per PO,
    /// content-sniffed. Draft only.</summary>
    [HttpPost("{id:long}/attachments")]
    [HasPermission(PermissionCodes.Purchase.Create)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddAttachment(long id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var order = await purchaseOrderService.AddAttachmentAsync(id,
            new AttachmentUpload(file.FileName, file.ContentType, file.Length, stream), ct);
        return OkEnvelope(order, "Attachment added.");
    }

    [HttpDelete("{id:long}/attachments/{attachmentId:long}")]
    [HasPermission(PermissionCodes.Purchase.Create)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveAttachment(long id, long attachmentId, CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.RemoveAttachmentAsync(id, attachmentId, ct),
            "Attachment removed.");

    [HttpGet("{id:long}/attachments/{attachmentId:long}")]
    [HasPermission(PermissionCodes.Purchase.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadAttachment(long id, long attachmentId, CancellationToken ct)
    {
        var attachment = await purchaseOrderService.GetAttachmentAsync(id, attachmentId, ct);
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    // ── PDF ─────────────────────────────────────────────────────────────────

    /// <summary>The PO as the supplier sees it. Drafts print, but are stamped NOT FOR ISSUE.</summary>
    [HttpGet("{id:long}/pdf")]
    [HasPermission(PermissionCodes.Purchase.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPdf(long id, CancellationToken ct)
    {
        var pdf = await purchaseOrderService.GetPdfAsync(id, ct);
        return File(pdf, "application/pdf", $"purchase-order-{id}.pdf");
    }
}

/// <summary>Body for the cancel route — a reason is mandatory, as with a booking.</summary>
public record CancelPurchaseOrderRequest(string Reason);
