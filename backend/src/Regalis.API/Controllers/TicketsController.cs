using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Operations;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Ticket masters (API_PLAN §5): categories with their issue types, for the
/// guided raise flow.</summary>
[Route("api/ticket-categories")]
public class TicketCategoriesController(ITicketService ticketService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Ticket.View)]
    [ProducesResponseType(typeof(ApiResponse<List<TicketCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        OkEnvelope(await ticketService.GetCategoriesAsync(ct));
}

/// <summary>Support tickets (API_PLAN §11, M21). Customers are scoped to their own
/// tickets by the service; internal notes never reach them.</summary>
[Route("api/tickets")]
public class TicketsController(ITicketService ticketService) : ApiControllerBase
{
    [HttpPost]
    [HasPermission(PermissionCodes.Ticket.Create)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateTicketRequest request, CancellationToken ct)
    {
        var ticket = await ticketService.CreateAsync(request, ct);
        return CreatedEnvelope($"/api/tickets/{ticket.TicketId}", ticket,
            $"Ticket {ticket.TicketNumber} raised. We usually respond within one business day.");
    }

    [HttpGet("my")]
    [HasPermission(PermissionCodes.Ticket.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TicketListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy([FromQuery] PagedRequest request, CancellationToken ct) =>
        OkEnvelope(await ticketService.GetMyAsync(request, ct));

    [HttpGet]
    [HasPermission(PermissionCodes.Ticket.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<TicketListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] TicketListRequest request, CancellationToken ct) =>
        OkEnvelope(await ticketService.GetTicketsAsync(request, ct));

    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Ticket.View)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) =>
        OkEnvelope(await ticketService.GetTicketAsync(id, ct));

    [HttpPost("{id:long}/messages")]
    [HasPermission(PermissionCodes.Ticket.Edit)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddMessage(long id, AddTicketMessageRequest request,
        CancellationToken ct) =>
        OkEnvelope(await ticketService.AddMessageAsync(id, request, ct), "Reply posted.");

    /// <summary>Multipart upload, ≤10 MB, .png .jpg .jpeg .webp .pdf — content-sniffed.</summary>
    [HttpPost("{id:long}/attachments")]
    [HasPermission(PermissionCodes.Ticket.Edit)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddAttachment(long id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var ticket = await ticketService.AddAttachmentAsync(id,
            new AttachmentUpload(file.FileName, file.ContentType, file.Length, stream), ct);
        return OkEnvelope(ticket, "Attachment added.");
    }

    [HttpGet("{id:long}/attachments/{attachmentId:long}")]
    [HasPermission(PermissionCodes.Ticket.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadAttachment(long id, long attachmentId,
        CancellationToken ct)
    {
        var attachment = await ticketService.GetAttachmentAsync(id, attachmentId, ct);
        return File(attachment.Content, attachment.ContentType, attachment.FileName);
    }

    [HttpPatch("{id:long}/assign")]
    [HasPermission(PermissionCodes.Ticket.Edit)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Assign(long id, AssignTicketRequest request, CancellationToken ct) =>
        OkEnvelope(await ticketService.AssignAsync(id, request, ct), "Ticket assigned.");

    [HttpPatch("{id:long}/status")]
    [HasPermission(PermissionCodes.Ticket.Edit)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangeStatus(long id, ChangeTicketStatusRequest request,
        CancellationToken ct) =>
        OkEnvelope(await ticketService.ChangeStatusAsync(id, request, ct), "Status updated.");

    [HttpPatch("{id:long}/priority")]
    [HasPermission(PermissionCodes.Ticket.Edit)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ChangePriority(long id, ChangeTicketPriorityRequest request,
        CancellationToken ct) =>
        OkEnvelope(await ticketService.ChangePriorityAsync(id, request, ct), "Priority updated.");

    [HttpPost("{id:long}/close")]
    [HasPermission(PermissionCodes.Ticket.Edit)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Close(long id, CancellationToken ct) =>
        OkEnvelope(await ticketService.CloseAsync(id, ct), "Ticket closed.");

    [HttpPost("{id:long}/reopen")]
    [HasPermission(PermissionCodes.Ticket.Edit)]
    [ProducesResponseType(typeof(ApiResponse<TicketDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reopen(long id, CancellationToken ct) =>
        OkEnvelope(await ticketService.ReopenAsync(id, ct), "Ticket reopened.");

    [HttpGet("export")]
    [HasPermission(PermissionCodes.Ticket.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] TicketListRequest request, CancellationToken ct)
    {
        var export = await ticketService.ExportAsync(request, ct);
        return File(export.Content, export.ContentType, export.FileName);
    }
}
