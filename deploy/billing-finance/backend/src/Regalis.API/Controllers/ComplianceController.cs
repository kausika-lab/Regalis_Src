using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance.Billing;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>The compliance calendar, reminders, the period lock (build doc §13) and the
/// accounting export (§20).</summary>
[StaffOnly]
public class ComplianceController(IComplianceService compliance, IFinanceReminderService reminders, IAccountingExportService export) : ApiControllerBase
{
    [HttpGet("api/compliance/calendar")]
    [HasPermission("COMPLIANCE.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<ComplianceItemDto[]>), 200)]
    public async Task<IActionResult> GetCalendar([FromQuery] string? period, [FromQuery] int months, CancellationToken ct) =>
        OkEnvelope(await compliance.GetCalendarAsync(period, months == 0 ? 3 : months, ct));

    [HttpPut("api/compliance/status")]
    [HasPermission("COMPLIANCE.MANAGE")]
    [ProducesResponseType(typeof(ApiResponse<ComplianceItemDto>), 200)]
    public async Task<IActionResult> SetStatus(ComplianceStatusRequest request, CancellationToken ct) => OkEnvelope(await compliance.SetStatusAsync(request, ct), "Saved.");

    [HttpGet("api/compliance/obligations")]
    [HasPermission("COMPLIANCE.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<ComplianceObligationDto[]>), 200)]
    public async Task<IActionResult> GetObligations(CancellationToken ct) => OkEnvelope(await compliance.GetObligationsAsync(ct));

    [HttpPost("api/compliance/obligations")]
    [HasPermission("COMPLIANCE.MANAGE")]
    [ProducesResponseType(typeof(ApiResponse<ComplianceObligationDto>), 200)]
    public async Task<IActionResult> CreateObligation(SaveComplianceObligationRequest request, CancellationToken ct) => OkEnvelope(await compliance.SaveObligationAsync(null, request, ct), "Saved.");

    [HttpPut("api/compliance/obligations/{id:int}")]
    [HasPermission("COMPLIANCE.MANAGE")]
    [ProducesResponseType(typeof(ApiResponse<ComplianceObligationDto>), 200)]
    public async Task<IActionResult> UpdateObligation(int id, SaveComplianceObligationRequest request, CancellationToken ct) => OkEnvelope(await compliance.SaveObligationAsync(id, request, ct), "Saved.");

    [HttpGet("api/period-locks")]
    [HasPermission("COMPLIANCE.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PeriodLockDto[]>), 200)]
    public async Task<IActionResult> GetLocks(CancellationToken ct) => OkEnvelope(await compliance.GetPeriodLocksAsync(ct));

    [HttpPost("api/period-locks/{period}/lock")]
    [HasPermission("FINANCE.LOCK")]
    [ProducesResponseType(typeof(ApiResponse<PeriodLockDto>), 200)]
    public async Task<IActionResult> Lock(string period, CancellationToken ct) => OkEnvelope(await compliance.LockAsync(period, ct), "Month locked.");

    /// <summary>Unlocking needs FINANCE.UNLOCK and a reason, which goes to the audit log.</summary>
    [HttpPost("api/period-locks/{period}/unlock")]
    [HasPermission("FINANCE.UNLOCK")]
    [ProducesResponseType(typeof(ApiResponse<PeriodLockDto>), 200)]
    public async Task<IActionResult> Unlock(string period, UnlockPeriodRequest request, CancellationToken ct) => OkEnvelope(await compliance.UnlockAsync(period, request, ct), "Month unlocked.");

    [HttpGet("api/reminders/log")]
    [HasPermission("COMPLIANCE.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<ReminderLogDto[]>), 200)]
    public async Task<IActionResult> GetReminderLog([FromQuery] string? documentType, [FromQuery] long? documentId, CancellationToken ct) =>
        OkEnvelope(await compliance.GetReminderLogAsync(documentType, documentId, ct));

    /// <summary>Sends whatever is due now. Does nothing while Reminders.Enabled is false.</summary>
    [HttpPost("api/reminders/run")]
    [HasPermission("COMPLIANCE.MANAGE")]
    [ProducesResponseType(typeof(ApiResponse<ReminderRunResultDto>), 200)]
    public async Task<IActionResult> RunReminders(CancellationToken ct) => OkEnvelope(await reminders.RunAsync(true, ct));

    /// <summary>The accounting export: one workbook, one sheet per list.</summary>
    [HttpGet("api/export/accounting")]
    [HasPermission("FINANCE.EXPORT")]
    public async Task<IActionResult> ExportAccounting([FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken ct) =>
        File(await export.ExportAsync(from, to, ct), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"regalis-accounts-{from:yyyyMMdd}-{to:yyyyMMdd}.xlsx");
}
