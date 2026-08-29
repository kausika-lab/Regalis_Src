using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>The customer's wallet (API_PLAN §7). Recharge and admin adjustment
/// arrive with the payments module screens.</summary>
[Route("api/wallet")]
public class WalletController(IWalletService walletService) : ApiControllerBase
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
}
