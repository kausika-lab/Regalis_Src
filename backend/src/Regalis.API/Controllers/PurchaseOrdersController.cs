using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Purchase orders (document §4). A PO is editable only while Draft, and
/// receiving goods against it writes the incoming inventory rows in the same
/// transaction — there is no separate goods-received endpoint that could drift.</summary>
[Route("api/purchase-orders")]
public class PurchaseOrdersController(IPurchaseOrderService purchaseOrderService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Purchase.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PurchaseOrderDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PurchaseOrderListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.GetPurchaseOrdersAsync(request, ct));

    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Purchase.View)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.GetPurchaseOrderAsync(id, ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Purchase.Create)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SavePurchaseOrderRequest request, CancellationToken ct)
    {
        var order = await purchaseOrderService.CreateAsync(request, ct);
        return CreatedEnvelope($"/api/purchase-orders/{order.PurchaseOrderId}", order,
            "Purchase order created.");
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.Purchase.Edit)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, SavePurchaseOrderRequest request,
        CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.UpdateAsync(id, request, ct), "Purchase order updated.");

    /// <summary>Draft to Sent — the point after which the PO is frozen.</summary>
    [HttpPost("{id:long}/send")]
    [HasPermission(PermissionCodes.Purchase.Approve)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(long id, CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.SendAsync(id, ct), "Purchase order sent.");

    /// <summary>Goods received: writes the incoming TRN_InventoryTransaction rows, the
    /// line receipts and the header status atomically.</summary>
    [HttpPost("{id:long}/receive")]
    [HasPermission(PermissionCodes.Purchase.Edit)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Receive(long id, ReceiveGoodsRequest request,
        CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.ReceiveAsync(id, request, ct), "Goods received.");

    [HttpPost("{id:long}/cancel")]
    [HasPermission(PermissionCodes.Purchase.Delete)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseOrderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long id, CancelPurchaseOrderRequest request,
        CancellationToken ct) =>
        OkEnvelope(await purchaseOrderService.CancelAsync(id, request.Reason, ct),
            "Purchase order cancelled.");
}

/// <summary>Body for the cancel route — a reason is mandatory, as with a booking.</summary>
public record CancelPurchaseOrderRequest(string Reason);
