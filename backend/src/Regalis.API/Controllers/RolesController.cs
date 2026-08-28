using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Administration;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Roles and the permission-matrix editor (API_PLAN §3).</summary>
[Route("api/roles")]
public class RolesController(IRoleAdminService roleAdmin) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Role.View)]
    [ProducesResponseType(typeof(ApiResponse<List<RoleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        OkEnvelope(await roleAdmin.GetRolesAsync(ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Role.Create)]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateRoleRequest request, CancellationToken ct)
    {
        var role = await roleAdmin.CreateRoleAsync(request, ct);
        return CreatedEnvelope($"/api/roles/{role.RoleId}", role, "Role created.");
    }

    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.Role.Edit)]
    [ProducesResponseType(typeof(ApiResponse<RoleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, UpdateRoleRequest request, CancellationToken ct) =>
        OkEnvelope(await roleAdmin.UpdateRoleAsync(id, request, ct), "Role updated.");

    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.Role.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await roleAdmin.DeleteRoleAsync(id, ct);
        return OkEnvelope("Role deleted.");
    }

    /// <summary>The permission matrix for one role: full catalogue with grant flags.</summary>
    [HttpGet("{id:int}/permissions")]
    [HasPermission(PermissionCodes.Permission.View)]
    [ProducesResponseType(typeof(ApiResponse<List<RolePermissionRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPermissions(int id, CancellationToken ct) =>
        OkEnvelope(await roleAdmin.GetRolePermissionsAsync(id, ct));

    [HttpPut("{id:int}/permissions")]
    [HasPermission(PermissionCodes.Permission.Edit)]
    public async Task<IActionResult> UpdatePermissions(int id, UpdateRolePermissionsRequest request,
        CancellationToken ct)
    {
        await roleAdmin.UpdateRolePermissionsAsync(id, request, ct);
        return OkEnvelope("Role permissions updated. They apply on the next token refresh.");
    }

    [HttpGet("{id:int}/menus")]
    [HasPermission(PermissionCodes.Role.View)]
    [ProducesResponseType(typeof(ApiResponse<List<RoleMenuRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMenus(int id, CancellationToken ct) =>
        OkEnvelope(await roleAdmin.GetRoleMenusAsync(id, ct));

    [HttpPut("{id:int}/menus")]
    [HasPermission(PermissionCodes.Role.Edit)]
    public async Task<IActionResult> UpdateMenus(int id, UpdateRoleMenusRequest request, CancellationToken ct)
    {
        await roleAdmin.UpdateRoleMenusAsync(id, request, ct);
        return OkEnvelope("Role menus updated.");
    }
}

/// <summary>The permission catalogue (read-only reference data).</summary>
[Route("api/permissions")]
public class PermissionsController(IRoleAdminService roleAdmin) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Permission.View)]
    [ProducesResponseType(typeof(ApiResponse<List<PermissionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        OkEnvelope(await roleAdmin.GetPermissionCatalogueAsync(ct));
}
