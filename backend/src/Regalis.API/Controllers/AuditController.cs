using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Administration;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>The audit trail (API_PLAN §13; Audit Trail Enhancements v1.0 §5.2). Read-only —
/// TRN_AuditLog has no update or delete endpoint anywhere in the system. Served at
/// /api/admin/audit as the enhancement specifies; /api/audit remains for older clients.</summary>
[Route("api/admin/audit")]
[Route("api/audit")]
[StaffOnly]
public class AuditController(IAuditService auditService) : ApiControllerBase
{
    /// <summary>Filters: search, module, actions (comma list), userIds (comma list,
    /// "system" allowed), result, from, to, page, pageSize, sort (createdAt:desc|asc).</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.Audit.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AuditListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AuditListRequest request, CancellationToken ct) =>
        OkEnvelope(await auditService.GetLogsAsync(request, ct));

    /// <summary>Distinct actions, modules and users for the filter dropdowns.</summary>
    [HttpGet("lookups")]
    [HasPermission(PermissionCodes.Audit.View)]
    [ProducesResponseType(typeof(ApiResponse<AuditLookupsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Lookups([FromQuery] string? userSearch, CancellationToken ct) =>
        OkEnvelope(await auditService.GetLookupsAsync(userSearch, ct));

    /// <summary>Distinct module names (kept for older clients; lookups has them too).</summary>
    [HttpGet("modules")]
    [HasPermission(PermissionCodes.Audit.View)]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModules(CancellationToken ct) =>
        OkEnvelope(await auditService.GetModulesAsync(ct));

    /// <summary>Same filters as the list; streams every matching row, and records an
    /// EXPORT audit row.</summary>
    [HttpGet("export")]
    [HasPermission(PermissionCodes.Audit.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] AuditListRequest request, CancellationToken ct)
    {
        var export = await auditService.ExportAsync(request, ct);
        return File(export.Content, export.ContentType, export.FileName);
    }

    /// <summary>One record with full before/after values, device and changed fields.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Audit.View)]
    [ProducesResponseType(typeof(ApiResponse<AuditDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(long id, CancellationToken ct) =>
        OkEnvelope(await auditService.GetDetailAsync(id, ct));

    /// <summary>Other audit records for the same entity, newest first.</summary>
    [HttpGet("{id:long}/related")]
    [HasPermission(PermissionCodes.Audit.View)]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<AuditListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Related(long id, CancellationToken ct) =>
        OkEnvelope(await auditService.GetRelatedAsync(id, ct));
}
