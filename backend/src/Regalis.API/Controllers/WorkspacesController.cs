using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Masters;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Workspaces — Desk / Cabin / Meeting Room masters (API_PLAN §5).</summary>
[Route("api/workspaces")]
public class WorkspacesController(IWorkspaceService workspaceService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Workspace.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WorkspaceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] WorkspaceListRequest request, CancellationToken ct) =>
        OkEnvelope(await workspaceService.GetWorkspacesAsync(request, ct));

    [HttpGet("{id:int}")]
    [HasPermission(PermissionCodes.Workspace.View)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        OkEnvelope(await workspaceService.GetWorkspaceAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Workspace.Create)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveWorkspaceRequest request, CancellationToken ct)
    {
        var workspace = await workspaceService.CreateWorkspaceAsync(request, ct);
        return CreatedEnvelope($"/api/workspaces/{workspace.WorkspaceId}", workspace, "Workspace created.");
    }

    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.Workspace.Edit)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, SaveWorkspaceRequest request, CancellationToken ct) =>
        OkEnvelope(await workspaceService.UpdateWorkspaceAsync(id, request, ct), "Workspace updated.");

    [HttpPatch("{id:int}/status")]
    [HasPermission(PermissionCodes.Workspace.Edit)]
    [ProducesResponseType(typeof(ApiResponse<WorkspaceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateStatus(int id, UpdateWorkspaceStatusRequest request,
        CancellationToken ct) =>
        OkEnvelope(await workspaceService.UpdateWorkspaceStatusAsync(id, request, ct),
            "Workspace status updated.");

    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.Workspace.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await workspaceService.DeleteWorkspaceAsync(id, ct);
        return OkEnvelope("Workspace deleted.");
    }
}

/// <summary>Workspace types are seeded reference data; reads for every signed-in user.</summary>
[Route("api/workspace-types")]
public class WorkspaceTypesController(IWorkspaceService workspaceService) : ApiControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<WorkspaceTypeDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        OkEnvelope(await workspaceService.GetWorkspaceTypesAsync(ct));
}

/// <summary>Amenities master (API_PLAN §5).</summary>
[Route("api/amenities")]
public class AmenitiesController(IWorkspaceService workspaceService) : ApiControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<AmenityDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        OkEnvelope(await workspaceService.GetAmenitiesAsync(ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Workspace.Create)]
    [ProducesResponseType(typeof(ApiResponse<AmenityDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveAmenityRequest request, CancellationToken ct)
    {
        var amenity = await workspaceService.CreateAmenityAsync(request, ct);
        return CreatedEnvelope($"/api/amenities/{amenity.AmenityId}", amenity, "Amenity created.");
    }
}
