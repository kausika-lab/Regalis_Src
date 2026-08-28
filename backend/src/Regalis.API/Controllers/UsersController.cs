using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Administration;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>User administration (API_PLAN §4). Customers are read through the same
/// table; the /customers views land with module M03's remaining screens.</summary>
[Route("api/users")]
public class UsersController(IUserAdminService userAdmin) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.User.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<UserListItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] UserListRequest request, CancellationToken ct) =>
        OkEnvelope(await userAdmin.GetUsersAsync(request, ct));

    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.User.View)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) =>
        OkEnvelope(await userAdmin.GetUserAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.User.Create)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(CreateUserRequest request, CancellationToken ct)
    {
        var user = await userAdmin.CreateUserAsync(request, ct);
        return CreatedEnvelope($"/api/users/{user.UserId}", user, "User created.");
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.User.Edit)]
    [ProducesResponseType(typeof(ApiResponse<UserDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, UpdateUserRequest request, CancellationToken ct) =>
        OkEnvelope(await userAdmin.UpdateUserAsync(id, request, ct), "User updated.");

    [HttpPatch("{id:long}/activate")]
    [HasPermission(PermissionCodes.User.Edit)]
    public async Task<IActionResult> Activate(long id, CancellationToken ct)
    {
        await userAdmin.ActivateAsync(id, ct);
        return OkEnvelope("User activated.");
    }

    [HttpPatch("{id:long}/deactivate")]
    [HasPermission(PermissionCodes.User.Edit)]
    public async Task<IActionResult> Deactivate(long id, CancellationToken ct)
    {
        await userAdmin.DeactivateAsync(id, ct);
        return OkEnvelope("User deactivated.");
    }

    [HttpPatch("{id:long}/lock")]
    [HasPermission(PermissionCodes.User.Edit)]
    public async Task<IActionResult> Lock(long id, CancellationToken ct)
    {
        await userAdmin.LockAsync(id, ct);
        return OkEnvelope("User locked.");
    }

    [HttpPatch("{id:long}/unlock")]
    [HasPermission(PermissionCodes.User.Edit)]
    public async Task<IActionResult> Unlock(long id, CancellationToken ct)
    {
        await userAdmin.UnlockAsync(id, ct);
        return OkEnvelope("User unlocked.");
    }

    [HttpPost("{id:long}/reset-password")]
    [HasPermission(PermissionCodes.User.Edit)]
    public async Task<IActionResult> ResetPassword(long id, AdminResetPasswordRequest request,
        CancellationToken ct)
    {
        await userAdmin.ResetPasswordAsync(id, request, ct);
        return OkEnvelope("Password reset. The user must change it at next sign-in.");
    }

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionCodes.User.Delete)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await userAdmin.DeleteAsync(id, ct);
        return OkEnvelope("User deleted.");
    }
}
