using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Masters;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Services &amp; Coffee catalogue (API_PLAN §5, M22). Customers hold
/// SERVICE.VIEW, so the same read route serves both portals; the admin grid passes
/// includeUnavailable=true.</summary>
[Route("api/services")]
public class ServicesController(IServiceCatalogueService catalogueService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Service.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ServiceItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] ServiceItemListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await catalogueService.GetItemsAsync(request, ct));

    /// <summary>The customer catalogue's category chips.</summary>
    [HttpGet("categories")]
    [HasPermission(PermissionCodes.Service.View)]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken ct) =>
        OkEnvelope(await catalogueService.GetCategoriesAsync(ct));

    [HttpGet("{id:int}")]
    [HasPermission(PermissionCodes.Service.View)]
    [ProducesResponseType(typeof(ApiResponse<ServiceItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        OkEnvelope(await catalogueService.GetItemAsync(id, ct));

    // SERVICE.CREATE belongs to customers for placing service ORDERS (matrix §4);
    // writing the catalogue itself is SERVICE.EDIT — an admin-portal privilege.
    [HttpPost]
    [HasPermission(PermissionCodes.Service.Edit)]
    [ProducesResponseType(typeof(ApiResponse<ServiceItemDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveServiceItemRequest request, CancellationToken ct)
    {
        var item = await catalogueService.CreateItemAsync(request, ct);
        return CreatedEnvelope($"/api/services/{item.ServiceItemId}", item, "Service item created.");
    }

    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.Service.Edit)]
    [ProducesResponseType(typeof(ApiResponse<ServiceItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, SaveServiceItemRequest request, CancellationToken ct) =>
        OkEnvelope(await catalogueService.UpdateItemAsync(id, request, ct), "Service item updated.");

    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.Service.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await catalogueService.DeleteItemAsync(id, ct);
        return OkEnvelope("Service item deleted.");
    }
}
