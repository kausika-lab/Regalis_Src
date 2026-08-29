using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Reporting;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Both portals' summary cards in one round trip (API_PLAN §12).</summary>
[Route("api/dashboard")]
public class DashboardController(IDashboardService dashboardService) : ApiControllerBase
{
    [HttpGet("admin")]
    [HasPermission(PermissionCodes.Dashboard.View)]
    [ProducesResponseType(typeof(ApiResponse<AdminDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdmin(CancellationToken ct) =>
        OkEnvelope(await dashboardService.GetAdminAsync(ct));

    /// <summary>Own-record aggregates — safe for any signed-in user.</summary>
    [HttpGet("customer")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CustomerDashboardDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomer(CancellationToken ct) =>
        OkEnvelope(await dashboardService.GetCustomerAsync(ct));
}
