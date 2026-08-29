using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Bookings;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Availability reads (API_PLAN §6) — advisory views over the single
/// booking/calendar data source. The engine re-verifies under locks on booking.</summary>
[Route("api/availability")]
public class AvailabilityController(IAvailabilityService availabilityService) : ApiControllerBase
{
    /// <summary>Slot grid for a date: available while any workspace of the type is free.</summary>
    [HttpGet("slots")]
    [HasPermission(PermissionCodes.Booking.View)]
    [ProducesResponseType(typeof(ApiResponse<List<SlotDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSlots([FromQuery] int locationId, [FromQuery] int workspaceTypeId,
        [FromQuery] DateOnly date, CancellationToken ct) =>
        OkEnvelope(await availabilityService.GetSlotsAsync(locationId, workspaceTypeId, date, ct));

    /// <summary>Free workspaces (with price) for a window.</summary>
    [HttpGet("workspaces")]
    [HasPermission(PermissionCodes.Booking.View)]
    [ProducesResponseType(typeof(ApiResponse<List<AvailableWorkspaceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkspaces([FromQuery] int locationId,
        [FromQuery] int workspaceTypeId, [FromQuery] DateTime startUtc,
        [FromQuery] int durationMinutes, CancellationToken ct) =>
        OkEnvelope(await availabilityService.GetAvailableWorkspacesAsync(
            locationId, workspaceTypeId, startUtc, durationMinutes, ct));

    /// <summary>Advisory single-workspace check.</summary>
    [HttpPost("check")]
    [HasPermission(PermissionCodes.Booking.View)]
    [ProducesResponseType(typeof(ApiResponse<AvailabilityCheckResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Check(AvailabilityCheckRequest request, CancellationToken ct) =>
        OkEnvelope(await availabilityService.CheckAsync(request, ct));
}
