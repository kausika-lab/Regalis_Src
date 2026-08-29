using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Masters;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Locations (API_PLAN §5). Reads are open to any authenticated user —
/// customers browse locations when booking; writes are LOCATION.*.</summary>
[Route("api/locations")]
public class LocationsController(ILocationService locationService) : ApiControllerBase
{
    [HttpGet]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<LocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PagedRequest request, CancellationToken ct) =>
        OkEnvelope(await locationService.GetLocationsAsync(request, ct));

    [HttpGet("{id:int}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        OkEnvelope(await locationService.GetLocationAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Location.Create)]
    [ProducesResponseType(typeof(ApiResponse<LocationDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveLocationRequest request, CancellationToken ct)
    {
        var location = await locationService.CreateLocationAsync(request, ct);
        return CreatedEnvelope($"/api/locations/{location.LocationId}", location, "Location created.");
    }

    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.Location.Edit)]
    [ProducesResponseType(typeof(ApiResponse<LocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, SaveLocationRequest request, CancellationToken ct) =>
        OkEnvelope(await locationService.UpdateLocationAsync(id, request, ct), "Location updated.");

    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.Location.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await locationService.DeleteLocationAsync(id, ct);
        return OkEnvelope("Location deleted.");
    }
}
