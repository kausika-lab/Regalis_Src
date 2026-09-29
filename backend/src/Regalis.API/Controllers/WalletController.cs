using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>The customer's wallet (API_PLAN §7). Recharge and admin adjustment
/// arrive with the payments module screens.</summary>
[Route("api/wallet")]
public class WalletController(
    IWalletService walletService,
    IWalletAdminService walletAdmin) : ApiControllerBase
{
    [HttpGet("me")]
    [HasPermission(PermissionCodes.Wallet.View)]
    [ProducesResponseType(typeof(ApiResponse<WalletDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyWallet(CancellationToken ct) =>
        OkEnvelope(await walletService.GetMyWalletAsync(ct));

    [HttpGet("me/transactions")]
    [HasPermission(PermissionCodes.Wallet.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WalletTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyTransactions([FromQuery] WalletTransactionListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await walletService.GetMyTransactionsAsync(request, ct));

    /// <summary>Creates the gateway order for a top-up (limits from settings).</summary>
    [HttpPost("recharge")]
    [HasPermission(PermissionCodes.Wallet.Create)]
    [ProducesResponseType(typeof(ApiResponse<PaymentOrderResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Recharge(RechargeOrderRequest request, CancellationToken ct) =>
        OkEnvelope(await walletAdmin.CreateRechargeOrderAsync(request, ct));

    /// <summary>Verifies the gateway signature and credits the wallet.</summary>
    [HttpPost("recharge/verify")]
    [HasPermission(PermissionCodes.Wallet.Create)]
    [ProducesResponseType(typeof(ApiResponse<RechargeResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> VerifyRecharge(RechargeVerifyRequest request, CancellationToken ct) =>
        OkEnvelope(await walletAdmin.VerifyRechargeAsync(request, ct), "Wallet recharged.");

    /// <summary>Admin view of a customer's wallet.</summary>
    [StaffOnly]
    [HttpGet("{customerId:long}")]
    [HasPermission(PermissionCodes.Wallet.View)]
    [ProducesResponseType(typeof(ApiResponse<WalletDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWallet(long customerId, CancellationToken ct) =>
        OkEnvelope(await walletAdmin.GetWalletAsync(customerId, ct));

    [StaffOnly]
    [HttpGet("{customerId:long}/transactions")]
    [HasPermission(PermissionCodes.Wallet.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<WalletTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions(long customerId,
        [FromQuery] WalletTransactionListRequest request, CancellationToken ct) =>
        OkEnvelope(await walletAdmin.GetTransactionsAsync(customerId, request, ct));

    /// <summary>Manual credit/debit with a mandatory reason — fully audited.</summary>
    [StaffOnly]
    [HttpPost("{customerId:long}/adjust")]
    [HasPermission(PermissionCodes.Wallet.Edit)]
    [ProducesResponseType(typeof(ApiResponse<WalletDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Adjust(long customerId, WalletAdjustRequest request,
        CancellationToken ct) =>
        OkEnvelope(await walletAdmin.AdjustAsync(customerId, request, ct), "Wallet adjusted.");
}
