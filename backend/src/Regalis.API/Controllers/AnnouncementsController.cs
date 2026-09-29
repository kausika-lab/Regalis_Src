using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Operations;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>
/// Announcements to customers (Notifications → Announcements sent). Drafts and scheduled
/// announcements can be edited or deleted; a sent one cannot be edited or unsent.
/// </summary>
[Route("api/admin/announcements")]
[Authorize]
[StaffOnly]
public class AnnouncementsController(IAnnouncementService service) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Notification.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AnnouncementDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] AnnouncementListRequest request, CancellationToken ct) =>
        OkEnvelope(await service.ListAsync(request, ct));

    /// <summary>Count per status (1 Draft, 2 Scheduled, 3 Sent) for the filter.</summary>
    [HttpGet("status-counts")]
    [HasPermission(PermissionCodes.Notification.View)]
    [ProducesResponseType(typeof(ApiResponse<Dictionary<byte, int>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StatusCounts(CancellationToken ct) =>
        OkEnvelope(await service.StatusCountsAsync(ct));

    /// <summary>Whether email and WhatsApp can really deliver on this server.</summary>
    [HttpGet("channels")]
    [HasPermission(PermissionCodes.Notification.View)]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementChannelsDto>), StatusCodes.Status200OK)]
    public IActionResult Channels() => OkEnvelope(service.Channels());

    /// <summary>How many customers an audience reaches ("It reaches 214 customers").</summary>
    [HttpGet("audience-count")]
    [HasPermission(PermissionCodes.Notification.View)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AudienceCount([FromQuery] byte audience, [FromQuery] long? customerId,
        CancellationToken ct) =>
        OkEnvelope(await service.AudienceCountAsync(audience, customerId, ct));

    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Notification.View)]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        OkEnvelope(await service.GetAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Notification.Create)]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Create(SaveAnnouncementRequest request, CancellationToken ct)
    {
        var saved = await service.CreateAsync(request, ct);
        return OkEnvelope(saved, Message(saved));
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.Notification.Create)]
    [ProducesResponseType(typeof(ApiResponse<AnnouncementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, SaveAnnouncementRequest request, CancellationToken ct)
    {
        var saved = await service.UpdateAsync(id, request, ct);
        return OkEnvelope(saved, Message(saved));
    }

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionCodes.Notification.Create)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return OkEnvelope("Announcement deleted.");
    }

    private static string Message(AnnouncementDto a) => a.Status switch
    {
        3 => $"Sent in-app to {a.RecipientCount} customer{(a.RecipientCount == 1 ? "" : "s")}.",
        2 => "Announcement scheduled.",
        _ => "Draft saved."
    };
}
