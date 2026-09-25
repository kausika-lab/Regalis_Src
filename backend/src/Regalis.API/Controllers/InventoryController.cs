using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Operations;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Inventory (API_PLAN §10, M19). The ledger is append-only — there is no
/// update or delete route for a transaction, by design (Invariant 11).</summary>
[Route("api/inventory")]
public class InventoryController(IInventoryService inventoryService) : ApiControllerBase
{
    [HttpGet("items")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InventoryItemDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems([FromQuery] InventoryItemListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await inventoryService.GetItemsAsync(request, ct));

    /// <summary>The Items screen as an Excel (.xlsx) download, same filters, all rows.</summary>
    [HttpGet("items/export")]
    [HasPermission(PermissionCodes.Inventory.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportItems([FromQuery] InventoryItemListRequest request,
        CancellationToken ct)
    {
        var export = await inventoryService.ExportItemsExcelAsync(request, ct);
        return File(export.Content, export.ContentType, export.FileName);
    }

    [HttpGet("items/{id:int}")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<InventoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItem(int id, CancellationToken ct) =>
        OkEnvelope(await inventoryService.GetItemAsync(id, ct));

    [HttpPost("items")]
    [HasPermission(PermissionCodes.Inventory.Create)]
    [ProducesResponseType(typeof(ApiResponse<InventoryItemDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateItem(SaveInventoryItemRequest request, CancellationToken ct)
    {
        var item = await inventoryService.CreateItemAsync(request, ct);
        return CreatedEnvelope($"/api/inventory/items/{item.InventoryItemId}", item, "Item created.");
    }

    [HttpPut("items/{id:int}")]
    [HasPermission(PermissionCodes.Inventory.Edit)]
    [ProducesResponseType(typeof(ApiResponse<InventoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateItem(int id, SaveInventoryItemRequest request,
        CancellationToken ct) =>
        OkEnvelope(await inventoryService.UpdateItemAsync(id, request, ct), "Item updated.");

    [HttpDelete("items/{id:int}")]
    [HasPermission(PermissionCodes.Inventory.Delete)]
    public async Task<IActionResult> DeleteItem(int id, CancellationToken ct)
    {
        await inventoryService.DeleteItemAsync(id, ct);
        return OkEnvelope("Item deleted.");
    }

    /// <summary>Derived stock + reorder flags — computed from the ledger, never stored.</summary>
    [HttpGet("stock")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<List<InventoryStockDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStock([FromQuery] int? locationId,
        [FromQuery] bool? belowReorder, CancellationToken ct) =>
        OkEnvelope(await inventoryService.GetStockAsync(locationId, belowReorder, ct));

    /// <summary>In / Out / Damage / Adjustment. A mistake is corrected with a
    /// compensating Adjustment row.</summary>
    [HttpPost("transactions")]
    [HasPermission(PermissionCodes.Inventory.Create)]
    [ProducesResponseType(typeof(ApiResponse<InventoryTransactionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTransaction(CreateInventoryTransactionRequest request,
        CancellationToken ct)
    {
        var entry = await inventoryService.CreateTransactionAsync(request, ct);
        return CreatedEnvelope($"/api/inventory/transactions/{entry.InventoryTransactionId}",
            entry, "Transaction recorded.");
    }

    /// <summary>The audit ledger.</summary>
    [HttpGet("transactions")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InventoryTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions([FromQuery] InventoryTransactionListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await inventoryService.GetTransactionsAsync(request, ct));

    [HttpGet("export")]
    [HasPermission(PermissionCodes.Inventory.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] InventoryTransactionListRequest request,
        CancellationToken ct)
    {
        var export = await inventoryService.ExportAsync(request, ct);
        return File(export.Content, export.ContentType, export.FileName);
    }
}
