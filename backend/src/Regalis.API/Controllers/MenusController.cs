using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Administration;
using Regalis.Application.Auth;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Navigation (API_PLAN §3): the caller's sidebar plus menu management.</summary>
[Route("api/menus")]
public class MenusController(IMenuQueryService menuQuery, IMenuAdminService menuAdmin) : ApiControllerBase
{
    /// <summary>The sidebar tree for the caller — both portals render from this.</summary>
    [HttpGet("my")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<MenuNodeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> My(CancellationToken ct) =>
        OkEnvelope(await menuQuery.GetMyMenusAsync(ct));

    [HttpGet]
    [HasPermission(PermissionCodes.Menu.View)]
    [ProducesResponseType(typeof(ApiResponse<List<MenuDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] byte? portalType, CancellationToken ct) =>
        OkEnvelope(await menuAdmin.GetMenusAsync(portalType, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Menu.Create)]
    [ProducesResponseType(typeof(ApiResponse<MenuDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateMenuRequest request, CancellationToken ct)
    {
        var menu = await menuAdmin.CreateMenuAsync(request, ct);
        return CreatedEnvelope($"/api/menus/{menu.MenuId}", menu, "Menu created.");
    }

    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.Menu.Edit)]
    [ProducesResponseType(typeof(ApiResponse<MenuDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateMenuRequest request, CancellationToken ct) =>
        OkEnvelope(await menuAdmin.UpdateMenuAsync(id, request, ct), "Menu updated.");

    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.Menu.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await menuAdmin.DeleteMenuAsync(id, ct);
        return OkEnvelope("Menu deleted.");
    }
}
