using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>
/// Assets (Regalis_WorkSpace_Assets_Feature_Specification §11.4): Items, Incoming Goods,
/// Outgoing Goods, Current Stock, stock counts and QR labels. Staff only, whatever a role
/// mapping says; every permission is checked here again, hiding a button is not security
/// (§13). There is no route anywhere that accepts a stock figure.
/// </summary>
[Route("api/admin")]
[StaffOnly]
public class AdminAssetsController(IAssetService assets) : ApiControllerBase
{
    // ── Items ───────────────────────────────────────────────────────────────

    /// <summary>The master list: one page of rows (50, set by the server), the total, and
    /// the five summary figures counted across all records.</summary>
    [HttpGet("items")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<AssetItemListResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItems([FromQuery] AssetItemListRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.GetItemsAsync(request, ct));

    [HttpGet("items/options")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<AssetFormOptionsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOptions(CancellationToken ct) =>
        OkEnvelope(await assets.GetFormOptionsAsync(ct));

    /// <summary>Same filters as the list, every row, in the team's column order. 204 when empty.</summary>
    [HttpGet("items/export")]
    [HasPermission(PermissionCodes.Inventory.Export)]
    public async Task<IActionResult> ExportItems([FromQuery] AssetItemListRequest request, CancellationToken ct) =>
        FileOrNoContent(await assets.ExportItemsAsync(request, ct));

    /// <summary>The next free codes for a prefix, without taking them.</summary>
    [HttpGet("items/next-codes")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<NextCodesDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> NextCodes([FromQuery] string prefix, [FromQuery] int count, CancellationToken ct) =>
        OkEnvelope(await assets.NextCodesAsync(prefix, count < 1 ? 1 : count, ct));

    [HttpGet("items/serial-check")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<SerialCheckDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SerialCheck([FromQuery] string serial, [FromQuery] int? excludeItemId,
        CancellationToken ct) =>
        OkEnvelope(await assets.CheckSerialAsync(serial, excludeItemId, ct));

    /// <summary>A QR sticker scanned inside the app: the code, or the whole text the
    /// sticker carries. 404 when no item has that code.</summary>
    [HttpGet("items/by-code")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<AssetItemDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ByCode([FromQuery] string code, CancellationToken ct) =>
        OkEnvelope(await assets.FindByCodeAsync(code, ct) ?? throw new NotFoundException("InventoryItem", code));

    [HttpGet("items/by-code/{code}")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<AssetItemDetailDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> ByCodePath(string code, CancellationToken ct) => ByCode(code, ct);

    /// <summary>The label sheet for a set of items: ids, or the list's filters.</summary>
    [HttpGet("items/labels")]
    [HasPermission(PermissionCodes.Inventory.Labels)]
    [ProducesResponseType(typeof(ApiResponse<AssetLabelSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Labels([FromQuery] AssetItemListRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.GetLabelsAsync(request, ct));

    [HttpPost("items/labels/printed")]
    [HasPermission(PermissionCodes.Inventory.Labels)]
    public async Task<IActionResult> LabelsPrinted(MarkLabelsPrintedRequest request, CancellationToken ct)
    {
        var count = await assets.MarkLabelsPrintedAsync(request, ct);
        return OkEnvelope(new { marked = count }, $"{count} label(s) recorded as printed.");
    }

    [HttpGet("items/import/template")]
    [HasPermission(PermissionCodes.Inventory.Create)]
    public async Task<IActionResult> ImportTemplate(CancellationToken ct)
    {
        var file = await assets.ImportTemplateAsync(ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Checks the team's sheet and shows what would be created. Nothing is saved.</summary>
    [HttpPost("items/import/preview")]
    [HasPermission(PermissionCodes.Inventory.Create)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<AssetImportResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ImportPreview(IFormFile file, [FromForm] DateOnly? receiptDate, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return OkEnvelope(await assets.ImportAsync(new InventoryImportUpload(file.FileName, file.Length, stream),
            receiptDate, save: false, ct));
    }

    /// <summary>Checks the sheet again and, when every row is clean, posts it as one
    /// opening-balance receipt.</summary>
    [HttpPost("items/import")]
    [HasPermission(PermissionCodes.Inventory.Create)]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<AssetImportResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Import(IFormFile file, [FromForm] DateOnly? receiptDate, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await assets.ImportAsync(new InventoryImportUpload(file.FileName, file.Length, stream),
            receiptDate, save: true, ct);
        return OkEnvelope(result, $"Imported {result.Units} asset(s) on {result.ReceiptNumber}.");
    }

    [HttpGet("items/{id:int}")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<AssetItemDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetItem(int id, CancellationToken ct) =>
        OkEnvelope(await assets.GetItemAsync(id, ct));

    /// <summary>One consumable, or N asset records through a posted receipt.</summary>
    [HttpPost("items")]
    [HasPermission(PermissionCodes.Inventory.Create)]
    [ProducesResponseType(typeof(ApiResponse<CreateAssetItemResult>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateItem(CreateAssetItemRequest request, CancellationToken ct)
    {
        var result = await assets.CreateItemAsync(request, ct);
        var message = result.Codes.Count == 1
            ? $"{result.Codes[0]} created."
            : $"{result.Codes.Count} assets created: {AssetRules.CodeRange([result.Codes[0], result.Codes[^1]])}.";
        return CreatedEnvelope($"/api/admin/items/{result.InventoryItemIds[0]}", result, message);
    }

    [HttpPut("items/{id:int}")]
    [HasPermission(PermissionCodes.Inventory.Edit)]
    [ProducesResponseType(typeof(ApiResponse<AssetItemSaveResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateItem(int id, UpdateAssetItemRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.UpdateItemAsync(id, request, ct), "Item saved.");

    [HttpPost("items/{id:int}/deactivate")]
    [HasPermission(PermissionCodes.Inventory.Edit)]
    [ProducesResponseType(typeof(ApiResponse<AssetItemDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Deactivate(int id, DeactivateItemRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.SetActiveAsync(id, false, request.Reason, ct), "Item made inactive.");

    [HttpPost("items/{id:int}/activate")]
    [HasPermission(PermissionCodes.Inventory.Edit)]
    [ProducesResponseType(typeof(ApiResponse<AssetItemDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Activate(int id, CancellationToken ct) =>
        OkEnvelope(await assets.SetActiveAsync(id, true, null, ct), "Item made active.");

    [HttpGet("items/{id:int}/events")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<List<ItemEventDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Events(int id, CancellationToken ct) =>
        OkEnvelope(await assets.GetEventsAsync(id, ct));

    [HttpGet("items/{id:int}/repairs")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<List<AssetRepairDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Repairs(int id, CancellationToken ct) =>
        OkEnvelope(await assets.GetRepairsAsync(id, ct));

    /// <summary>A finished repair: the asset comes back to In use / In store.</summary>
    [HttpPost("items/{id:int}/repairs/{repairId:long}/close")]
    [HasPermission(PermissionCodes.Inventory.Issue)]
    [ProducesResponseType(typeof(ApiResponse<AssetItemDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CloseRepair(int id, long repairId, CloseRepairRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.CloseRepairAsync(id, repairId, request, ct), "Repair closed.");

    [HttpGet("items/{id:int}/documents")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<List<ItemDocumentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Documents(int id, CancellationToken ct) =>
        OkEnvelope(await assets.GetDocumentsAsync(id, ct));

    /// <summary>Purchase invoice, warranty card or photo: .pdf .jpg .png, up to 10 MB.</summary>
    [HttpPost("items/{id:int}/documents")]
    [HasPermission(PermissionCodes.Inventory.Edit)]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [ProducesResponseType(typeof(ApiResponse<ItemDocumentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddDocument(int id, IFormFile file, [FromForm] byte documentType, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return OkEnvelope(await assets.AddDocumentAsync(id, documentType,
            new AttachmentUpload(file.FileName, file.ContentType, file.Length, stream), ct), "Document added.");
    }

    [HttpGet("items/{id:int}/documents/{documentId:long}")]
    [HasPermission(PermissionCodes.Inventory.View)]
    public async Task<IActionResult> Document(int id, long documentId, CancellationToken ct)
    {
        var file = await assets.GetDocumentAsync(id, documentId, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    // ── Categories and rooms ────────────────────────────────────────────────

    [HttpGet("asset-categories")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<List<AssetCategoryDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Categories(CancellationToken ct) => OkEnvelope(await assets.GetCategoriesAsync(ct));

    [HttpPost("asset-categories")]
    [HasPermission(PermissionCodes.Inventory.Edit)]
    [ProducesResponseType(typeof(ApiResponse<AssetCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddCategory(SaveAssetCategoryRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.SaveCategoryAsync(null, request, ct), "Category added.");

    [HttpPut("asset-categories/{id:int}")]
    [HasPermission(PermissionCodes.Inventory.Edit)]
    [ProducesResponseType(typeof(ApiResponse<AssetCategoryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveCategory(int id, SaveAssetCategoryRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.SaveCategoryAsync(id, request, ct), "Category saved.");

    [HttpGet("asset-locations")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<List<AssetLocationDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Locations(CancellationToken ct) => OkEnvelope(await assets.GetLocationsAsync(ct));

    [HttpPost("asset-locations")]
    [HasPermission(PermissionCodes.Inventory.Edit)]
    [ProducesResponseType(typeof(ApiResponse<AssetLocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddLocation(SaveAssetLocationRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.SaveLocationAsync(null, request, ct), "Room added.");

    [HttpPut("asset-locations/{id:int}")]
    [HasPermission(PermissionCodes.Inventory.Edit)]
    [ProducesResponseType(typeof(ApiResponse<AssetLocationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveLocation(int id, SaveAssetLocationRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.SaveLocationAsync(id, request, ct), "Room saved.");

    // ── Incoming Goods ──────────────────────────────────────────────────────

    [HttpGet("goods-receipts")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<GoodsReceiptRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Receipts([FromQuery] GoodsReceiptListRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.GetReceiptsAsync(request, ct));

    [HttpGet("goods-receipts/export")]
    [HasPermission(PermissionCodes.Inventory.Export)]
    public async Task<IActionResult> ExportReceipts([FromQuery] GoodsReceiptListRequest request, CancellationToken ct) =>
        FileOrNoContent(await assets.ExportReceiptsAsync(request, ct));

    /// <summary>Purchase orders still waiting for goods — "Against purchase order".</summary>
    [HttpGet("goods-receipts/open-purchase-orders")]
    [HasPermission(PermissionCodes.Inventory.Receive)]
    [ProducesResponseType(typeof(ApiResponse<List<OpenPurchaseOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> OpenPurchaseOrders([FromQuery] int? supplierId, CancellationToken ct) =>
        OkEnvelope(await assets.GetOpenPurchaseOrdersAsync(supplierId, ct));

    /// <summary>What posting each line will do — the codes it will take, the stock it will
    /// leave. Nothing is saved or reserved.</summary>
    [HttpPost("goods-receipts/preview")]
    [HasPermission(PermissionCodes.Inventory.Receive)]
    [ProducesResponseType(typeof(ApiResponse<GoodsReceiptPreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewReceipt(SaveGoodsReceiptRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.PreviewReceiptAsync(request, ct));

    [HttpGet("goods-receipts/{id:long}")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<GoodsReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Receipt(long id, CancellationToken ct) =>
        OkEnvelope(await assets.GetReceiptAsync(id, ct));

    /// <summary>Creates a draft; with Post = true, creates and posts it in one step.</summary>
    [HttpPost("goods-receipts")]
    [HasPermission(PermissionCodes.Inventory.Receive)]
    [ProducesResponseType(typeof(ApiResponse<GoodsReceiptDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateReceipt(SaveGoodsReceiptRequest request, CancellationToken ct)
    {
        var receipt = await assets.CreateReceiptAsync(request, ct);
        return CreatedEnvelope($"/api/admin/goods-receipts/{receipt.GoodsReceiptId}", receipt,
            request.Post ? $"{receipt.ReceiptNumber} posted." : $"{receipt.ReceiptNumber} saved as a draft.");
    }

    [HttpPut("goods-receipts/{id:long}")]
    [HasPermission(PermissionCodes.Inventory.Receive)]
    [ProducesResponseType(typeof(ApiResponse<GoodsReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateReceipt(long id, SaveGoodsReceiptRequest request, CancellationToken ct)
    {
        var receipt = await assets.UpdateReceiptAsync(id, request, ct);
        return OkEnvelope(receipt, request.Post ? $"{receipt.ReceiptNumber} posted." : "Draft saved.");
    }

    [HttpDelete("goods-receipts/{id:long}")]
    [HasPermission(PermissionCodes.Inventory.Receive)]
    public async Task<IActionResult> DeleteReceipt(long id, CancellationToken ct)
    {
        await assets.DeleteDraftReceiptAsync(id, ct);
        return OkEnvelope("Draft deleted.");
    }

    /// <summary>Applies it: allocates codes, creates the assets, writes the movements. The
    /// irreversible step — a mistake afterwards is reversed.</summary>
    [HttpPost("goods-receipts/{id:long}/post")]
    [HasPermission(PermissionCodes.Inventory.Receive)]
    [ProducesResponseType(typeof(ApiResponse<GoodsReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PostReceipt(long id, PostGoodsReceiptRequest? request, CancellationToken ct)
    {
        var receipt = await assets.PostReceiptAsync(id, request?.ConfirmMissingCost ?? false, ct);
        return OkEnvelope(receipt, $"{receipt.ReceiptNumber} posted.");
    }

    [HttpPost("goods-receipts/{id:long}/reverse")]
    [HasPermission(PermissionCodes.Inventory.Reverse)]
    [ProducesResponseType(typeof(ApiResponse<GoodsReceiptDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReverseReceipt(long id, ReverseRequest request, CancellationToken ct)
    {
        var reversal = await assets.ReverseReceiptAsync(id, request.Reason, ct);
        return OkEnvelope(reversal, $"Reversed by {reversal.ReceiptNumber}.");
    }

    // ── Outgoing Goods ──────────────────────────────────────────────────────

    [HttpGet("goods-issues")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<GoodsIssueRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Issues([FromQuery] GoodsIssueListRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.GetIssuesAsync(request, ct));

    [HttpGet("goods-issues/export")]
    [HasPermission(PermissionCodes.Inventory.Export)]
    public async Task<IActionResult> ExportIssues([FromQuery] GoodsIssueListRequest request, CancellationToken ct) =>
        FileOrNoContent(await assets.ExportIssuesAsync(request, ct));

    [HttpPost("goods-issues/preview")]
    [HasPermission(PermissionCodes.Inventory.Issue)]
    [ProducesResponseType(typeof(ApiResponse<GoodsIssuePreviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewIssue(CreateGoodsIssueRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.PreviewIssueAsync(request, ct));

    [HttpGet("goods-issues/{id:long}")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<GoodsIssueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Issue(long id, CancellationToken ct) =>
        OkEnvelope(await assets.GetIssueAsync(id, ct));

    /// <summary>Posts a movement out. Stock never goes below zero; an asset in repair
    /// cannot move until the repair is closed.</summary>
    [HttpPost("goods-issues")]
    [HasPermission(PermissionCodes.Inventory.Issue)]
    [ProducesResponseType(typeof(ApiResponse<GoodsIssueResult>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateIssue(CreateGoodsIssueRequest request, CancellationToken ct)
    {
        var result = await assets.CreateIssueAsync(request, ct);
        return CreatedEnvelope($"/api/admin/goods-issues/{result.Issue.GoodsIssueId}", result,
            $"{result.Issue.IssueNumber} posted." + (result.Message is null ? "" : " " + result.Message));
    }

    [HttpPost("goods-issues/{id:long}/reverse")]
    [HasPermission(PermissionCodes.Inventory.Reverse)]
    [ProducesResponseType(typeof(ApiResponse<GoodsIssueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ReverseIssue(long id, ReverseRequest request, CancellationToken ct)
    {
        var reversal = await assets.ReverseIssueAsync(id, request.Reason, ct);
        return OkEnvelope(reversal, $"Reversed by {reversal.IssueNumber}.");
    }

    // ── Current Stock ───────────────────────────────────────────────────────

    /// <summary>Per consumable: opening, in, out, on hand, value, flag — for the period
    /// (default: this month so far). Nothing here is stored.</summary>
    [HttpGet("stock")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<StockSheetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Stock([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? locationId, [FromQuery] bool? belowReorder, CancellationToken ct) =>
        OkEnvelope(await assets.GetStockAsync(from, to, locationId, belowReorder, ct));

    [HttpGet("stock/assets-by-location")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<List<AssetsByLocationRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AssetsByLocation(CancellationToken ct) =>
        OkEnvelope(await assets.GetAssetsByLocationAsync(ct));

    /// <summary>Every movement, newest first, with the running balance beside each row.</summary>
    [HttpGet("stock/ledger")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<StockLedgerRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Ledger([FromQuery] StockLedgerRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.GetLedgerAsync(request, ct));

    [HttpGet("stock/export")]
    [HasPermission(PermissionCodes.Inventory.Export)]
    public async Task<IActionResult> ExportStock([FromQuery] DateOnly? from, [FromQuery] DateOnly? to,
        [FromQuery] int? locationId, CancellationToken ct) =>
        FileOrNoContent(await assets.ExportStockAsync(from, to, locationId, ct));

    // ── Stock counts ────────────────────────────────────────────────────────

    [HttpGet("stock-counts")]
    [HasPermission(PermissionCodes.Inventory.View)]
    [ProducesResponseType(typeof(ApiResponse<List<StockCountRowDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StockCounts(CancellationToken ct) => OkEnvelope(await assets.GetStockCountsAsync(ct));

    [HttpGet("stock-counts/{id:long}")]
    [HasPermission(PermissionCodes.Inventory.Count)]
    [ProducesResponseType(typeof(ApiResponse<StockCountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StockCount(long id, CancellationToken ct) =>
        OkEnvelope(await assets.GetStockCountAsync(id, ct));

    /// <summary>Starts a count: a sheet of every consumable with its recorded quantity hidden.</summary>
    [HttpPost("stock-counts")]
    [HasPermission(PermissionCodes.Inventory.Count)]
    [ProducesResponseType(typeof(ApiResponse<StockCountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> StartStockCount(StartStockCountRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.StartStockCountAsync(request, ct), "Stock count started.");

    [HttpPost("stock-counts/{id:long}/submit")]
    [HasPermission(PermissionCodes.Inventory.Count)]
    [ProducesResponseType(typeof(ApiResponse<StockCountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SubmitStockCount(long id, SubmitStockCountRequest request, CancellationToken ct) =>
        OkEnvelope(await assets.SubmitStockCountAsync(id, request, ct));

    [HttpPost("stock-counts/{id:long}/confirm")]
    [HasPermission(PermissionCodes.Inventory.Count)]
    [ProducesResponseType(typeof(ApiResponse<StockCountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmStockCount(long id, CancellationToken ct) =>
        OkEnvelope(await assets.ConfirmStockCountAsync(id, ct), "Adjustments written.");

    [HttpPost("stock-counts/{id:long}/cancel")]
    [HasPermission(PermissionCodes.Inventory.Count)]
    [ProducesResponseType(typeof(ApiResponse<StockCountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CancelStockCount(long id, CancellationToken ct) =>
        OkEnvelope(await assets.CancelStockCountAsync(id, ct), "Stock count cancelled.");

    private IActionResult FileOrNoContent(InventoryExportResult? export) =>
        export is null ? NoContent() : File(export.Content, export.ContentType, export.FileName);
}
