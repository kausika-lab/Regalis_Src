using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Masters;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>App settings (API_PLAN §5, M23). Secrets are masked on read and locked on
/// write; /public is anonymous and returns only support and company basics — it feeds
/// the Contact Us screen.</summary>
[Route("api/settings")]
public class SettingsController(ISettingAdminService settingAdminService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Setting.View)]
    [ProducesResponseType(typeof(ApiResponse<List<AppSettingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        OkEnvelope(await settingAdminService.GetSettingsAsync(ct));

    [HttpPut]
    [HasPermission(PermissionCodes.Setting.Edit)]
    [ProducesResponseType(typeof(ApiResponse<List<AppSettingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(UpdateAppSettingsRequest request, CancellationToken ct) =>
        OkEnvelope(await settingAdminService.UpdateSettingsAsync(request, ct),
            "Settings saved and applied.");

    /// <summary>Anonymous — support number, hours and company name only (Contact Us).</summary>
    [HttpGet("public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<PublicSettingsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPublic(CancellationToken ct) =>
        OkEnvelope(await settingAdminService.GetPublicSettingsAsync(ct));
}
