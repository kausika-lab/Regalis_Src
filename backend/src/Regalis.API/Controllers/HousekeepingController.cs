using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Operations;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Housekeeping / maintenance scheduling (API_PLAN §10, M18). Creating a
/// blocking schedule writes the calendar block in the same transaction, so customer
/// availability drops the slot with no second code path (Invariant 12).</summary>
[Route("api/housekeeping")]
public class HousekeepingController(IHousekeepingService housekeepingService, IMaintenanceService maintenance)
    : ApiControllerBase
{
    /// <summary>The admin grid. Staff callers are scoped to their own rows.</summary>
    [HttpGet("schedules")]
    [HasPermission(PermissionCodes.Housekeeping.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<HousekeepingScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSchedules([FromQuery] HousekeepingListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await housekeepingService.GetSchedulesAsync(request, ct));

    /// <summary>Own rows for the signed-in staff member (My Schedule).</summary>
    [HttpGet("my-schedules")]
    [HasPermission(PermissionCodes.Housekeeping.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<HousekeepingScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySchedules([FromQuery] PagedRequest request,
        CancellationToken ct) =>
        OkEnvelope(await housekeepingService.GetMySchedulesAsync(request, ct));

    [HttpPost("schedules")]
    [HasPermission(PermissionCodes.Housekeeping.Create)]
    [ProducesResponseType(typeof(ApiResponse<HousekeepingScheduleDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveHousekeepingScheduleRequest request, CancellationToken ct)
    {
        var schedule = await housekeepingService.CreateAsync(request, ct);
        // Housekeeping and cleaning schedules also appear on the Maintenance checklist.
        await maintenance.CreateForScheduleAsync(schedule.HousekeepingScheduleId, ct);
        return CreatedEnvelope($"/api/housekeeping/schedules/{schedule.HousekeepingScheduleId}",
            schedule, "Schedule created. The staff access QR is valid for this window.");
    }

    [HttpPut("schedules/{id:long}")]
    [HasPermission(PermissionCodes.Housekeeping.Edit)]
    [ProducesResponseType(typeof(ApiResponse<HousekeepingScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, SaveHousekeepingScheduleRequest request,
        CancellationToken ct) =>
        OkEnvelope(await housekeepingService.UpdateAsync(id, request, ct),
            "Schedule updated. The calendar block and staff QR follow the new window.");

    /// <summary>Cancel releases the calendar block — the slot returns to customers.</summary>
    [HttpPost("schedules/{id:long}/cancel")]
    [HasPermission(PermissionCodes.Housekeeping.Edit)]
    [ProducesResponseType(typeof(ApiResponse<HousekeepingScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct)
    {
        var schedule = await housekeepingService.CancelAsync(id, ct);
        await maintenance.CancelForScheduleAsync(id, ct);
        return OkEnvelope(schedule, "Schedule cancelled and the workspace released.");
    }

    [HttpPost("schedules/{id:long}/check-in")]
    [HasPermission(PermissionCodes.Housekeeping.Edit)]
    [ProducesResponseType(typeof(ApiResponse<HousekeepingScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckIn(long id, CancellationToken ct) =>
        OkEnvelope(await housekeepingService.CheckInAsync(id, ct), "Task started.");

    [HttpPost("schedules/{id:long}/check-out")]
    [HasPermission(PermissionCodes.Housekeeping.Edit)]
    [ProducesResponseType(typeof(ApiResponse<HousekeepingScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckOut(long id, CompleteHousekeepingRequest request,
        CancellationToken ct) =>
        OkEnvelope(await housekeepingService.CheckOutAsync(id, request, ct),
            "Task completed. Any remaining calendar block was released.");
}
