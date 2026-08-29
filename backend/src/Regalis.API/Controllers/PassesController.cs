using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Pass plan management + browsing (API_PLAN §8).</summary>
[Route("api/pass-plans")]
public class PassPlansController(IPassService passService) : ApiControllerBase
{
    /// <summary>Active plans for the purchase pages: ?type=1 (day) | 2 (monthly).</summary>
    [HttpGet("active")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<List<PassPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive([FromQuery] byte? type, CancellationToken ct) =>
        OkEnvelope(await passService.GetActivePlansAsync(type, ct));

    [HttpGet]
    [HasPermission(PermissionCodes.Pass.View)]
    [ProducesResponseType(typeof(ApiResponse<List<PassPlanDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        OkEnvelope(await passService.GetAllPlansAsync(ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Pass.Create)]
    [ProducesResponseType(typeof(ApiResponse<PassPlanDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SavePassPlanRequest request, CancellationToken ct)
    {
        var plan = await passService.CreatePlanAsync(request, ct);
        return CreatedEnvelope($"/api/pass-plans/{plan.PassPlanId}", plan, "Plan created.");
    }

    [HttpPut("{id:int}")]
    [HasPermission(PermissionCodes.Pass.Edit)]
    [ProducesResponseType(typeof(ApiResponse<PassPlanDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(int id, SavePassPlanRequest request, CancellationToken ct) =>
        OkEnvelope(await passService.UpdatePlanAsync(id, request, ct), "Plan updated.");

    [HttpDelete("{id:int}")]
    [HasPermission(PermissionCodes.Pass.Delete)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await passService.DeletePlanAsync(id, ct);
        return OkEnvelope("Plan deleted.");
    }
}

/// <summary>Pass purchases (API_PLAN §8).</summary>
[Route("api/passes")]
public class PassesController(IPassService passService) : ApiControllerBase
{
    /// <summary>Gateway order for a pass purchase (wallet purchases skip this).</summary>
    [HttpPost("order")]
    [HasPermission(PermissionCodes.Pass.Create)]
    [ProducesResponseType(typeof(ApiResponse<PaymentOrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateOrder(PassOrderRequest request, CancellationToken ct) =>
        OkEnvelope(await passService.CreatePurchaseOrderAsync(request, ct));

    /// <summary>The atomic purchase: pass + payment + invoice + QR + notification.</summary>
    [HttpPost("purchase")]
    [HasPermission(PermissionCodes.Pass.Create)]
    [ProducesResponseType(typeof(ApiResponse<PurchasePassResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Purchase(PurchasePassRequest request, CancellationToken ct) =>
        OkEnvelope(await passService.PurchaseAsync(request, ct),
            "Pass activated. Your QR access pass is ready.");

    [HttpGet("my")]
    [HasPermission(PermissionCodes.Pass.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerPassDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy([FromQuery] PassListRequest request, CancellationToken ct) =>
        OkEnvelope(await passService.GetMyPassesAsync(request, ct));

    /// <summary>All purchases — the admin grid.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.Pass.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CustomerPassDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] PassListRequest request, CancellationToken ct)
    {
        if (User.FindFirst("user_type")?.Value == "1")
        {
            return OkEnvelope(await passService.GetMyPassesAsync(request, ct));
        }
        return OkEnvelope(await passService.GetPassesAsync(request, ct));
    }

    /// <summary>Cancel — full wallet refund before the pass starts, none after.</summary>
    [HttpPost("{id:long}/cancel")]
    [HasPermission(PermissionCodes.Pass.Edit)]
    [ProducesResponseType(typeof(ApiResponse<CustomerPassDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long id, CancellationToken ct) =>
        OkEnvelope(await passService.CancelAsync(id, ct), "Pass cancelled.");
}
