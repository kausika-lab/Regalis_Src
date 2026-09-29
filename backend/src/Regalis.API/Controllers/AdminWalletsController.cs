using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Admin wallet screens (Billing &amp; Finance → Wallet). Read-only aggregates
/// over the append-only ledger; money-moving actions stay on WalletController.</summary>
[Route("api/admin/wallets")]
[StaffOnly]
public class AdminWalletsController(IWalletLedgerQueries ledger, IWalletDeskService desk) : ApiControllerBase
{
    /// <summary>Every customer wallet with totals summed from the ledger, the summary
    /// cards and the filtered totals row. Filter: all | with-balance | zero | low.
    /// Sort: balance-desc | balance-asc | name | last-activity | spent.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.Wallet.View)]
    [ProducesResponseType(typeof(ApiResponse<WalletListResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWallets([FromQuery] WalletListRequest request, CancellationToken ct) =>
        OkEnvelope(await ledger.GetWalletsAsync(request, ct));

    /// <summary>The filtered list as CSV.</summary>
    [HttpGet("export")]
    [HasPermission(PermissionCodes.Wallet.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] WalletListRequest request, CancellationToken ct) =>
        File(await ledger.ExportWalletsAsync(request, ct), "text/csv",
            $"customer-wallets-{DateTime.UtcNow:yyyyMMdd}.csv");

    /// <summary>One customer: profile, active membership, ledger totals and the filtered,
    /// paginated transactions (newest first).</summary>
    [HttpGet("{customerId:long}")]
    [HasPermission(PermissionCodes.Wallet.View)]
    [ProducesResponseType(typeof(ApiResponse<WalletDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWallet(long customerId, [FromQuery] WalletLedgerRequest request,
        CancellationToken ct) =>
        OkEnvelope(await desk.GetWalletAsync(customerId, request, ct));

    /// <summary>Statement for a period (from/to, IST). format = pdf (default) or csv.</summary>
    [HttpGet("{customerId:long}/statement")]
    [HasPermission(PermissionCodes.Wallet.View)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Statement(long customerId, [FromQuery] WalletLedgerRequest request,
        [FromQuery] string? format, CancellationToken ct)
    {
        var (content, contentType, fileName) = await desk.ExportStatementAsync(customerId, request, format ?? "pdf", ct);
        return File(content, contentType, fileName);
    }

    /// <summary>Money collected at the desk: the PAY- payment and the Recharge credit in
    /// one transaction.</summary>
    [HttpPost("{customerId:long}/add-money")]
    [HasPermission(PermissionCodes.Wallet.Edit)]
    [ProducesResponseType(typeof(ApiResponse<AdminAddMoneyResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddMoney(long customerId, AdminAddMoneyRequest request, CancellationToken ct) =>
        OkEnvelope(await desk.AddMoneyAsync(customerId, request, ct), "Money added to the wallet.");

    /// <summary>A credit or debit correction entry; a debit can never take the balance below zero.</summary>
    [HttpPost("{customerId:long}/adjust")]
    [HasPermission(PermissionCodes.Wallet.Edit)]
    [ProducesResponseType(typeof(ApiResponse<WalletDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Adjust(long customerId, AdminAdjustRequest request, CancellationToken ct) =>
        OkEnvelope(await desk.AdjustAsync(customerId, request, ct), "Adjustment posted.");
}
