using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Bookings;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>The single calendar serving both portals (API_PLAN §6, Invariant 1).</summary>
[Route("api/calendar")]
public class CalendarController(ICalendarService calendarService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Calendar.View)]
    [ProducesResponseType(typeof(ApiResponse<CalendarResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] string view, [FromQuery] DateOnly date,
        [FromQuery] int? locationId, [FromQuery] int? workspaceTypeId, [FromQuery] int? workspaceId,
        CancellationToken ct) =>
        OkEnvelope(await calendarService.GetCalendarAsync(
            view ?? "day", date, locationId, workspaceTypeId, workspaceId, ct));

    /// <summary>One workspace's timeline (convenience over the same data source).</summary>
    [HttpGet("workspace/{id:int}")]
    [HasPermission(PermissionCodes.Calendar.View)]
    [ProducesResponseType(typeof(ApiResponse<CalendarResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkspace(int id, [FromQuery] string view,
        [FromQuery] DateOnly date, CancellationToken ct) =>
        OkEnvelope(await calendarService.GetCalendarAsync(view ?? "week", date, null, null, id, ct));

    /// <summary>Blocks a workspace (maintenance, internal use…). Refused when
    /// confirmed bookings overlap the window.</summary>
    [HttpPost("block")]
    [HasPermission(PermissionCodes.Calendar.Create)]
    [ProducesResponseType(typeof(ApiResponse<BlockDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Block(CreateBlockRequest request, CancellationToken ct)
    {
        var block = await calendarService.BlockAsync(request, ct);
        return CreatedEnvelope($"/api/calendar/block/{block.WorkspaceBlockId}", block,
            "Workspace blocked. Customer availability updates immediately.");
    }

    /// <summary>Releases a block; the row is kept for the audit trail.</summary>
    [HttpPost("unblock/{blockId:long}")]
    [HasPermission(PermissionCodes.Calendar.Edit)]
    [ProducesResponseType(typeof(ApiResponse<BlockDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unblock(long blockId, CancellationToken ct) =>
        OkEnvelope(await calendarService.UnblockAsync(blockId, ct), "Block released.");
}
