using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Operations;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Maintenance &amp; housekeeping checklist (Maintenance Feature Specification).
/// HOUSEKEEPING.VIEW opens the module; who may add, assign, verify, delete or manage
/// housekeepers is decided in <see cref="IMaintenanceService"/> from the caller's roles,
/// because the Housekeeping Staff role itself carries CREATE/EDIT/APPROVE.</summary>
[Route("api/maintenance")]
[StaffOnly]
[HasPermission(PermissionCodes.Housekeeping.View)]
public class MaintenanceController(IMaintenanceService maintenance) : ApiControllerBase
{
    /// <summary>What the caller may do — drives the tabs and buttons.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceMeDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct) => OkEnvelope(await maintenance.GetMeAsync(ct));

    [HttpGet("options")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceOptionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Options(CancellationToken ct) => OkEnvelope(await maintenance.GetOptionsAsync(ct));

    // ── Checklist ────────────────────────────────────────────────────────────

    [HttpGet("work")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkPageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWork([FromQuery] MaintenanceWorkListRequest request, CancellationToken ct) =>
        OkEnvelope(await maintenance.GetWorkAsync(request, ct));

    /// <summary>CSV of the filtered checklist (same filters as the list).</summary>
    [HttpGet("work/export")]
    public async Task<IActionResult> Export([FromQuery] MaintenanceWorkListRequest request, CancellationToken ct)
    {
        var label = request.All ? "all" : (request.Date ?? IstClock.IstDateOf(DateTime.UtcNow)).ToString("yyyy-MM-dd");
        return File(await maintenance.ExportWorkAsync(request, ct), "text/csv", $"maintenance-{label}.csv");
    }

    [HttpGet("work/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkItem(long id, CancellationToken ct) =>
        OkEnvelope(await maintenance.GetWorkItemAsync(id, ct));

    [HttpPost("work")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveMaintenanceWorkRequest request, CancellationToken ct)
    {
        var work = await maintenance.CreateAsync(request, ct);
        return CreatedEnvelope($"/api/maintenance/work/{work.WorkItemId}", work, $"{work.WorkNumber} added.");
    }

    [HttpPut("work/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, SaveMaintenanceWorkRequest request, CancellationToken ct) =>
        OkEnvelope(await maintenance.UpdateAsync(id, request, ct), "Work updated.");

    [HttpPost("work/{id:long}/assign")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Assign(long id, MaintenanceAssignRequest request, CancellationToken ct) =>
        OkEnvelope(await maintenance.AssignAsync(id, request.AssignedToUserId, ct), "Work reassigned.");

    [HttpDelete("work/{id:long}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await maintenance.DeleteAsync(id, ct);
        return OkEnvelope("Work deleted.");
    }

    [HttpPost("work/{id:long}/cancel")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long id, MaintenanceReasonRequest request, CancellationToken ct) =>
        OkEnvelope(await maintenance.CancelAsync(id, request.Reason, ct), "Work cancelled.");

    [HttpPost("work/{id:long}/start")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Start(long id, CancellationToken ct) =>
        OkEnvelope(await maintenance.StartAsync(id, ct), "Task started.");

    /// <summary>Multipart: <c>note</c> (required) and an optional <c>photo</c>.</summary>
    [HttpPost("work/{id:long}/done")]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MarkDone(long id, [FromForm] string? note, IFormFile? photo, CancellationToken ct)
    {
        const string message = "Marked done. A supervisor will verify it.";
        if (photo is null)
        {
            return OkEnvelope(await maintenance.MarkDoneAsync(id, note, null, ct), message);
        }
        await using var stream = photo.OpenReadStream();
        return OkEnvelope(await maintenance.MarkDoneAsync(id, note,
            new AttachmentUpload(photo.FileName, photo.ContentType, photo.Length, stream), ct), message);
    }

    [HttpPost("work/{id:long}/verify")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Verify(long id, CancellationToken ct) =>
        OkEnvelope(await maintenance.VerifyAsync(id, ct), "Work verified.");

    [HttpPost("work/{id:long}/reject")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceWorkDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reject(long id, MaintenanceReasonRequest request, CancellationToken ct) =>
        OkEnvelope(await maintenance.RejectAsync(id, request.Reason, ct), "Sent back for rework.");

    [HttpGet("work/{id:long}/photo")]
    public async Task<IActionResult> Photo(long id, CancellationToken ct)
    {
        var photo = await maintenance.GetPhotoAsync(id, ct);
        return File(photo.Content, photo.ContentType);
    }

    /// <summary>Housekeeper phone view. <c>tab</c>: todo (default), done, rework.</summary>
    [HttpGet("my-tasks")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceMyTasksDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> MyTasks([FromQuery] string? tab, CancellationToken ct) =>
        OkEnvelope(await maintenance.GetMyTasksAsync(tab, ct));

    // ── Housekeepers ─────────────────────────────────────────────────────────

    [HttpGet("housekeepers")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<MaintenanceHousekeeperDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Housekeepers([FromQuery] HousekeeperListRequest request, CancellationToken ct) =>
        OkEnvelope(await maintenance.GetHousekeepersAsync(request, ct));

    [HttpPost("housekeepers")]
    [ProducesResponseType(typeof(ApiResponse<HousekeeperCreatedDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateHousekeeper(SaveHousekeeperRequest request, CancellationToken ct)
    {
        var created = await maintenance.CreateHousekeeperAsync(request, ct);
        return CreatedEnvelope($"/api/maintenance/housekeepers/{created.Housekeeper.UserId}", created,
            $"{created.Housekeeper.FullName} added as {created.Housekeeper.HousekeeperCode}.");
    }

    [HttpPut("housekeepers/{userId:long}")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceHousekeeperDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateHousekeeper(long userId, SaveHousekeeperRequest request, CancellationToken ct) =>
        OkEnvelope(await maintenance.UpdateHousekeeperAsync(userId, request, ct), "Housekeeper updated.");

    [HttpPost("housekeepers/{userId:long}/deactivate")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceHousekeeperDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(long userId, DeactivateHousekeeperRequest request, CancellationToken ct) =>
        OkEnvelope(await maintenance.DeactivateHousekeeperAsync(userId, request.ReassignToUserId, ct), "Housekeeper deactivated.");

    [HttpPost("housekeepers/{userId:long}/activate")]
    [ProducesResponseType(typeof(ApiResponse<MaintenanceHousekeeperDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(long userId, CancellationToken ct) =>
        OkEnvelope(await maintenance.ActivateHousekeeperAsync(userId, ct), "Housekeeper reactivated.");

    [HttpGet("housekeepers/{userId:long}/log")]
    [ProducesResponseType(typeof(ApiResponse<HousekeeperWorkLogDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> WorkLog(long userId, CancellationToken ct) =>
        OkEnvelope(await maintenance.GetWorkLogAsync(userId, ct));
}
