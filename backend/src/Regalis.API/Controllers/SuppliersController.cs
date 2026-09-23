using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Supplier master for the internal finance chain (document §21).</summary>
[Route("api/suppliers")]
public class SuppliersController(ISupplierService supplierService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Supplier.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<SupplierDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] SupplierListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await supplierService.GetSuppliersAsync(request, ct));

    [HttpGet("{id:int}")]
    [HasPermission(PermissionCodes.Supplier.View)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct) =>
        OkEnvelope(await supplierService.GetSupplierAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Supplier.Create)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveSupplierRequest request, CancellationToken ct)
    {
        var supplier = await supplierService.CreateAsync(request, ct);
        return CreatedEnvelope($"/api/suppliers/{supplier.SupplierId}", supplier, "Supplier created.");
    }

    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.Supplier.Edit)]
    [ProducesResponseType(typeof(ApiResponse<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, SaveSupplierRequest request,
        CancellationToken ct) =>
        OkEnvelope(await supplierService.UpdateAsync(id, request, ct), "Supplier updated.");

    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.Supplier.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await supplierService.DeleteAsync(id, ct);
        return OkEnvelope("Supplier deleted.");
    }
}
