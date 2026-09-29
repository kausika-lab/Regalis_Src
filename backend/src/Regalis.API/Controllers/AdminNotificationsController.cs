using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Operations;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>
/// The admin portal's Notifications page: the incoming feed, read state (per admin) and
/// each admin's notification settings. Staff-only — the feed spans every customer.
/// Customers keep /api/notifications/my for their own notification centre.
/// </summary>
[Route("api/admin/notifications")]
[Authorize]
[StaffOnly]
[HasPermission(PermissionCodes.Notification.View)]
public class AdminNotificationsController(IAdminNotificationService service) : ApiControllerBase
{
    /// <summary>Feed page + per-type counts + unread count + the four summary cards.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<AdminNotificationFeedDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetFeed([FromQuery] AdminNotificationListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await service.GetFeedAsync(request, ct));

    /// <summary>Unread count for the logged-in admin (sidebar badge and bell).</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct) =>
        OkEnvelope(await service.GetUnreadCountAsync(ct));

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        await service.MarkReadAsync(id, ct);
        return OkEnvelope("Marked as read.");
    }

    [HttpPost("read-all")]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var count = await service.MarkAllReadAsync(ct);
        return OkEnvelope(count, "All notifications marked as read.");
    }
}

/// <summary>The logged-in admin's own channels per event type (in-app is always on).</summary>
[Route("api/admin/notification-settings")]
[Authorize]
[StaffOnly]
public class AdminNotificationSettingsController(IAdminNotificationService service) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<List<AdminNotificationSettingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct) =>
        OkEnvelope(await service.GetSettingsAsync(ct));

    [HttpPut]
    [ProducesResponseType(typeof(ApiResponse<List<AdminNotificationSettingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Save(SaveAdminNotificationSettingsRequest request, CancellationToken ct) =>
        OkEnvelope(await service.SaveSettingsAsync(request, ct), "Notification settings saved.");
}
