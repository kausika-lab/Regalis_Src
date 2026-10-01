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

    /// <summary>The Items screen as an Excel (.xlsx) download, same filters, all rows.
    /// 204 when nothing matches.</summary>
    [HttpGet("items/export")]
    [HasPermission(PermissionCodes.Inventory.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ExportItems([FromQuery] InventoryItemListRequest request,
        CancellationToken ct) =>
        FileOrNoContent(await inventoryService.ExportItemsExcelAsync(request, ct));

    /// <summary>Incoming Goods as .xlsx: search, supplier and date range, all rows.</summary>
    [HttpGet("incoming/export")]
    [HasPermission(PermissionCodes.Inventory.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ExportIncoming([FromQuery] InventoryTransactionListRequest request,
        CancellationToken ct) =>
        FileOrNoContent(await inventoryService.ExportGoodsExcelAsync(request,
            (byte)Domain.Enums.InventoryTransactionType.In, ct));

    /// <summary>Outgoing Goods as .xlsx: search, item and date range, all rows.</summary>
    [HttpGet("outgoing/export")]
    [HasPermission(PermissionCodes.Inventory.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ExportOutgoing([FromQuery] InventoryTransactionListRequest request,
        CancellationToken ct) =>
        FileOrNoContent(await inventoryService.ExportGoodsExcelAsync(request,
            (byte)Domain.Enums.InventoryTransactionType.Out, ct));

    /// <summary>Current Stock as .xlsx. Export only — stock is derived, never imported.</summary>
    [HttpGet("stock/export")]
    [HasPermission(PermissionCodes.Inventory.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> ExportStock([FromQuery] int? locationId, [FromQuery] bool? belowReorder,
        CancellationToken ct) =>
        FileOrNoContent(await inventoryService.ExportStockExcelAsync(locationId, belowReorder, ct));

    // ── Excel import: items | incoming | outgoing ───────────────────────────
    // Create is the screen's own right to add rows; updating existing items also needs
    // Edit, which the service checks. Staff only, whatever a role mapping says.

    /// <summary>The blank workbook: headers, one example row, an Instructions sheet.</summary>
    [HttpGet("{kind:regex(^(items|incoming|outgoing)$)}/import/template")]
    [HasPermission(PermissionCodes.Inventory.Create)]
    [StaffOnly]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportTemplate(string kind, CancellationToken ct)
    {
        var template = await inventoryService.ImportTemplateAsync(kind, ct);
        return File(template.Content, template.ContentType, template.FileName);
    }

    /// <summary>Checks every row and returns the preview. Nothing is saved.</summary>
    [HttpPost("{kind:regex(^(items|incoming|outgoing)$)}/import/preview")]
    [HasPermission(PermissionCodes.Inventory.Create)]
    [StaffOnly]
    [ProducesResponseType(typeof(ApiResponse<InventoryImportResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportPreview(string kind, IFormFile file, [FromForm] string? existing,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return OkEnvelope(await inventoryService.ImportAsync(kind,
            new InventoryImportUpload(file.FileName, file.Length, stream), existing, save: false, ct));
    }

    /// <summary>Checks the file again and saves every valid row in one transaction.</summary>
    [HttpPost("{kind:regex(^(items|incoming|outgoing)$)}/import")]
    [HasPermission(PermissionCodes.Inventory.Create)]
    [StaffOnly]
    [ProducesResponseType(typeof(ApiResponse<InventoryImportResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(string kind, IFormFile file, [FromForm] string? existing,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await inventoryService.ImportAsync(kind,
            new InventoryImportUpload(file.FileName, file.Length, stream), existing, save: true, ct);
        return OkEnvelope(result, $"Imported {result.CreatedCount + result.UpdatedCount} row(s).");
    }

    /// <summary>The failing rows of a file, with an Error column, to fix and upload again.</summary>
    [HttpPost("{kind:regex(^(items|incoming|outgoing)$)}/import/errors")]
    [HasPermission(PermissionCodes.Inventory.Create)]
    [StaffOnly]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportErrors(string kind, IFormFile file, [FromForm] string? existing,
        CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var report = await inventoryService.ImportErrorReportAsync(kind,
            new InventoryImportUpload(file.FileName, file.Length, stream), existing, ct);
        return File(report.Content, report.ContentType, report.FileName);
    }

    private IActionResult FileOrNoContent(InventoryExportResult? export) =>
        export is null ? NoContent() : File(export.Content, export.ContentType, export.FileName);

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
