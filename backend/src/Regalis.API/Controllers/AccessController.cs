using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Access;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>QR and access control (API_PLAN §9).</summary>
[Route("api/access")]
public class AccessController(IAccessService accessService) : ApiControllerBase
{
    /// <summary>The caller's active QR access passes.</summary>
    [HttpGet("my-tokens")]
    [HasPermission(PermissionCodes.Access.View)]
    [ProducesResponseType(typeof(ApiResponse<List<MyAccessTokenDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTokens(CancellationToken ct) =>
        OkEnvelope(await accessService.GetMyTokensAsync(ct));

    /// <summary>The device/reader endpoint: evaluates the rules, commands the door,
    /// always writes an access log row. Grant or deny lives in the response body —
    /// this endpoint returns 200 either way.</summary>
    [HttpPost("validate")]
    [HasPermission(PermissionCodes.Access.Create)]
    [ProducesResponseType(typeof(ApiResponse<AccessDecisionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Validate(ValidateAccessRequest request, CancellationToken ct)
    {
        var decision = await accessService.ValidateAsync(request, ct);
        return OkEnvelope(decision, decision.IsGranted
            ? "Access granted."
            : $"Access denied: {decision.DenyReason}.");
    }

    /// <summary>The device-down fallback. Requires the distinct ACCESS.OVERRIDE
    /// permission; every use writes an exception log row and an audit entry (A10).</summary>
    [HttpPost("manual-override")]
    [HasPermission(PermissionCodes.Access.Override)]
    [ProducesResponseType(typeof(ApiResponse<AccessDecisionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ManualOverride(ManualOverrideRequest request, CancellationToken ct) =>
        OkEnvelope(await accessService.ManualOverrideAsync(request, ct),
            "Access granted by manual override. The exception has been logged.");

    [HttpPost("revoke/{tokenId:long}")]
    [HasPermission(PermissionCodes.Access.Edit)]
    public async Task<IActionResult> Revoke(long tokenId, CancellationToken ct)
    {
        await accessService.RevokeTokenAsync(tokenId, ct);
        return OkEnvelope("Token revoked.");
    }

    [HttpGet("logs")]
    [HasPermission(PermissionCodes.Access.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<AccessLogDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetLogs([FromQuery] AccessLogListRequest request, CancellationToken ct) =>
        OkEnvelope(await accessService.GetLogsAsync(request, ct));
}
