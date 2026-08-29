using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Masters;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Promotional banners (API_PLAN §5, M23). The active list is open to any
/// signed-in user — it feeds the home-screen carousel.</summary>
[Route("api/banners")]
public class BannersController(IBannerService bannerService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Banner.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BannerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, CancellationToken ct) =>
        OkEnvelope(await bannerService.GetBannersAsync(request, ct));

    /// <summary>Banners inside their validity window for the caller's portal.</summary>
    [HttpGet("active")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<BannerDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive([FromQuery] int? locationId, CancellationToken ct) =>
        OkEnvelope(await bannerService.GetActiveBannersAsync(locationId, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Banner.Create)]
    [ProducesResponseType(typeof(ApiResponse<BannerDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveBannerRequest request, CancellationToken ct)
    {
        var banner = await bannerService.CreateBannerAsync(request, ct);
        return CreatedEnvelope($"/api/banners/{banner.BannerId}", banner, "Banner created.");
    }

    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.Banner.Edit)]
    [ProducesResponseType(typeof(ApiResponse<BannerDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, SaveBannerRequest request, CancellationToken ct) =>
        OkEnvelope(await bannerService.UpdateBannerAsync(id, request, ct), "Banner updated.");

    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.Banner.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await bannerService.DeleteBannerAsync(id, ct);
        return OkEnvelope("Banner deleted.");
    }
}
