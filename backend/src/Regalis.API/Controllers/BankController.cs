using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>
/// The bank book (document §13–§14). Admin-only throughout: no customer role holds
/// BANK.*, so the customer portal cannot reach any of these routes. Matching points at
/// an existing payment or expense and never creates one.
/// </summary>
[Route("api/bank")]
public class BankController(IBankService bankService) : ApiControllerBase
{
    // ── Accounts ────────────────────────────────────────────────────────────

    [HttpGet("accounts")]
    [HasPermission(PermissionCodes.Bank.View)]
    [ProducesResponseType(typeof(ApiResponse<List<BankAccountDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccounts(CancellationToken ct) =>
        OkEnvelope(await bankService.GetAccountsAsync(ct));

    [HttpGet("accounts/{id:int}")]
    [HasPermission(PermissionCodes.Bank.View)]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccount(int id, CancellationToken ct) =>
        OkEnvelope(await bankService.GetAccountAsync(id, ct));

    [HttpPost("accounts")]
    [HasPermission(PermissionCodes.Bank.Create)]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAccount(SaveBankAccountRequest request,
        CancellationToken ct)
    {
        var account = await bankService.CreateAccountAsync(request, ct);
        return CreatedEnvelope($"/api/bank/accounts/{account.BankAccountId}", account,
            "Bank account created.");
    }

    [HttpPut("accounts/{id:int}")]
    [HasPermission(PermissionCodes.Bank.Edit)]
    [ProducesResponseType(typeof(ApiResponse<BankAccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAccount(int id, SaveBankAccountRequest request,
        CancellationToken ct) =>
        OkEnvelope(await bankService.UpdateAccountAsync(id, request, ct), "Bank account updated.");

    [HttpDelete("accounts/{id:int}")]
    [HasPermission(PermissionCodes.Bank.Delete)]
    public async Task<IActionResult> DeleteAccount(int id, CancellationToken ct)
    {
        await bankService.DeleteAccountAsync(id, ct);
        return OkEnvelope("Bank account deleted.");
    }

    // ── Transactions ────────────────────────────────────────────────────────

    [HttpGet("transactions")]
    [HasPermission(PermissionCodes.Bank.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BankTransactionDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransactions([FromQuery] BankTransactionListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await bankService.GetTransactionsAsync(request, ct));

    [HttpGet("transactions/{id:long}")]
    [HasPermission(PermissionCodes.Bank.View)]
    [ProducesResponseType(typeof(ApiResponse<BankTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTransaction(long id, CancellationToken ct) =>
        OkEnvelope(await bankService.GetTransactionAsync(id, ct));

    [HttpPost("transactions")]
    [HasPermission(PermissionCodes.Bank.Create)]
    [ProducesResponseType(typeof(ApiResponse<BankTransactionDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateTransaction(SaveBankTransactionRequest request,
        CancellationToken ct)
    {
        var entry = await bankService.CreateTransactionAsync(request, ct);
        return CreatedEnvelope($"/api/bank/transactions/{entry.BankTransactionId}", entry,
            "Bank transaction recorded.");
    }

    [HttpPut("transactions/{id:long}")]
    [HasPermission(PermissionCodes.Bank.Edit)]
    [ProducesResponseType(typeof(ApiResponse<BankTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateTransaction(long id, SaveBankTransactionRequest request,
        CancellationToken ct) =>
        OkEnvelope(await bankService.UpdateTransactionAsync(id, request, ct),
            "Bank transaction updated.");

    [HttpDelete("transactions/{id:long}")]
    [HasPermission(PermissionCodes.Bank.Delete)]
    public async Task<IActionResult> DeleteTransaction(long id, CancellationToken ct)
    {
        await bankService.DeleteTransactionAsync(id, ct);
        return OkEnvelope("Bank transaction deleted.");
    }

    // ── Reconciliation ──────────────────────────────────────────────────────

    /// <summary>Payments or expenses that plausibly correspond to this line.</summary>
    [HttpGet("transactions/{id:long}/candidates")]
    [HasPermission(PermissionCodes.Bank.View)]
    [ProducesResponseType(typeof(ApiResponse<List<MatchCandidateDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCandidates(long id, CancellationToken ct) =>
        OkEnvelope(await bankService.GetMatchCandidatesAsync(id, ct));

    /// <summary>Links the line to an existing payment or expense. Nothing is created,
    /// and neither the payment nor the expense is written to.</summary>
    [HttpPost("transactions/{id:long}/match")]
    [HasPermission(PermissionCodes.Bank.Edit)]
    [ProducesResponseType(typeof(ApiResponse<BankTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Match(long id, MatchBankTransactionRequest request,
        CancellationToken ct) =>
        OkEnvelope(await bankService.MatchAsync(id, request, ct), "Bank line matched.");

    [HttpPost("transactions/{id:long}/unmatch")]
    [HasPermission(PermissionCodes.Bank.Edit)]
    [ProducesResponseType(typeof(ApiResponse<BankTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Unmatch(long id, CancellationToken ct) =>
        OkEnvelope(await bankService.UnmatchAsync(id, ct), "Match removed.");

    [HttpPost("transactions/{id:long}/reconcile")]
    [HasPermission(PermissionCodes.Bank.Reconcile)]
    [ProducesResponseType(typeof(ApiResponse<BankTransactionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Reconcile(long id, CancellationToken ct) =>
        OkEnvelope(await bankService.ReconcileAsync(id, ct), "Bank line reconciled.");

    [HttpGet("export")]
    [HasPermission(PermissionCodes.Bank.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] BankTransactionListRequest request,
        CancellationToken ct)
    {
        var export = await bankService.ExportAsync(request, ct);
        return File(export.Content, export.ContentType, export.FileName);
    }
}
