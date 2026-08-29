using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Access;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Attendance + My Work Status (API_PLAN §9).</summary>
[Route("api/attendance")]
public class AttendanceController(IAttendanceService attendanceService) : ApiControllerBase
{
    /// <summary>Self check-in from the app — needs a live booking or a pass today.</summary>
    [HttpPost("check-in")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckIn(SelfCheckRequest request, CancellationToken ct) =>
        OkEnvelope(await attendanceService.SelfCheckInAsync(request, ct), "Checked in. Have a productive day!");

    [HttpPost("check-out")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CheckOut(CancellationToken ct) =>
        OkEnvelope(await attendanceService.SelfCheckOutAsync(ct), "Checked out. See you soon!");

    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AttendanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy([FromQuery] PagedRequest request, CancellationToken ct) =>
        OkEnvelope(await attendanceService.GetMyAsync(request, ct));

    /// <summary>The caller's open session today, if any (My Work Status header).</summary>
    [HttpGet("my/open")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyOpen(CancellationToken ct) =>
        OkEnvelope(await attendanceService.GetMyOpenAsync(ct));

    /// <summary>The admin grid (ATTENDANCE.VIEW; own-rows scoping applies to customers
    /// via /my).</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.Attendance.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AttendanceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AttendanceListRequest request, CancellationToken ct)
    {
        if (User.FindFirst("user_type")?.Value == "1")
        {
            return OkEnvelope(await attendanceService.GetMyAsync(request, ct));
        }
        return OkEnvelope(await attendanceService.GetAttendanceAsync(request, ct));
    }

    /// <summary>Correction keeps the original values — payroll stays auditable.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.Attendance.Edit)]
    [ProducesResponseType(typeof(ApiResponse<AttendanceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Correct(long id, CorrectAttendanceRequest request, CancellationToken ct) =>
        OkEnvelope(await attendanceService.CorrectAsync(id, request, ct),
            "Attendance corrected. The original times are preserved.");
}

/// <summary>The payroll hand-off (A03): an export file, no direct integration.</summary>
[Route("api/payroll")]
public class PayrollController(IAttendanceService attendanceService) : ApiControllerBase
{
    [HttpGet("export")]
    [HasPermission(PermissionCodes.Payroll.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] DateOnly from, [FromQuery] DateOnly to,
        [FromQuery] int? locationId, CancellationToken ct)
    {
        var export = await attendanceService.ExportPayrollAsync(from, to, locationId, ct);
        return File(export.Content, export.ContentType, export.FileName);
    }
}
