using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Administration;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>The audit trail (API_PLAN §13). Read-only — TRN_AuditLog has no update
/// or delete path anywhere in the system.</summary>
[Route("api/audit")]
public class AuditController(IAuditService auditService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Audit.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AuditListRequest request, CancellationToken ct) =>
        OkEnvelope(await auditService.GetLogsAsync(request, ct));

    /// <summary>Distinct module names for the filter dropdown.</summary>
    [HttpGet("modules")]
    [HasPermission(PermissionCodes.Audit.View)]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModules(CancellationToken ct) =>
        OkEnvelope(await auditService.GetModulesAsync(ct));

    [HttpGet("export")]
    [HasPermission(PermissionCodes.Audit.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] AuditListRequest request, CancellationToken ct)
    {
        var export = await auditService.ExportAsync(request, ct);
        return File(export.Content, export.ContentType, export.FileName);
    }
}
