using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Operations;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>The in-app notification centre (API_PLAN §11).</summary>
[Route("api/notifications")]
public class NotificationsController(INotificationService notificationService) : ApiControllerBase
{
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<NotificationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy([FromQuery] NotificationListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await notificationService.GetMyAsync(request, ct));

    [HttpGet("my/unread-count")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUnreadCount(CancellationToken ct) =>
        OkEnvelope(await notificationService.GetMyUnreadCountAsync(ct));

    [HttpPatch("{id:long}/read")]
    [Authorize]
    public async Task<IActionResult> MarkRead(long id, CancellationToken ct)
    {
        await notificationService.MarkReadAsync(id, ct);
        return OkEnvelope("Marked as read.");
    }

    [HttpPost("read-all")]
    [Authorize]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await notificationService.MarkAllReadAsync(ct);
        return OkEnvelope("All notifications marked as read.");
    }

    [HttpDelete("{id:long}")]
    [Authorize]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await notificationService.DeleteAsync(id, ct);
        return OkEnvelope("Notification removed.");
    }

    /// <summary>Broadcast to all customers or all admin-portal users.</summary>
    [HttpPost("broadcast")]
    [HasPermission(PermissionCodes.Notification.Create)]
    [ProducesResponseType(typeof(ApiResponse<int>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Broadcast(BroadcastNotificationRequest request, CancellationToken ct)
    {
        var recipients = await notificationService.BroadcastAsync(request, ct);
        return OkEnvelope(recipients, $"Sent to {recipients} recipient(s).");
    }
}
