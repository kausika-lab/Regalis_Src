using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Finance.Billing;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>Ledger, outstanding and ageing for customers and suppliers (build doc §7). The
/// customer side needs INVOICE.VIEW and the supplier side BILL.VIEW.</summary>
[Route("api/parties/{type}")]
[StaffOnly]
public class PartiesController(IPartyLedgerService ledgers, ICurrentUser currentUser) : ApiControllerBase
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private void Check(string type)
    {
        string permission = string.Equals(type, "supplier", StringComparison.OrdinalIgnoreCase) ? "BILL.VIEW" : "INVOICE.VIEW";
        if (!currentUser.HasPermission(permission)) throw new ForbiddenException("You do not have access to these ledgers.");
    }

    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<PartyOptionDto[]>), 200)]
    public async Task<IActionResult> Search(string type, [FromQuery] string? q, CancellationToken ct)
    {
        Check(type);
        return OkEnvelope(await ledgers.SearchPartiesAsync(type, q, ct));
    }

    [HttpGet("{id:long}/ledger")]
    [ProducesResponseType(typeof(ApiResponse<PartyLedgerDto>), 200)]
    public async Task<IActionResult> GetLedger(string type, long id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        Check(type);
        return OkEnvelope(await ledgers.GetLedgerAsync(type, id, from, to, ct));
    }

    /// <summary>format = xlsx or pdf.</summary>
    [HttpGet("{id:long}/ledger/export")]
    public async Task<IActionResult> ExportLedger(string type, long id, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string format, CancellationToken ct)
    {
        Check(type);
        var (content, contentType, fileName) = await ledgers.ExportLedgerAsync(type, id, from, to, format ?? "xlsx", ct);
        return File(content, contentType, fileName);
    }

    [HttpPost("{id:long}/statement/email")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> EmailStatement(string type, long id, EmailStatementRequest request, CancellationToken ct)
    {
        Check(type);
        string message = await ledgers.EmailStatementAsync(type, id, request, ct);
        return OkEnvelope(message, message);
    }

    [HttpGet("outstanding")]
    [ProducesResponseType(typeof(ApiResponse<OutstandingPartyDto[]>), 200)]
    public async Task<IActionResult> GetOutstanding(string type, CancellationToken ct)
    {
        Check(type);
        return OkEnvelope(await ledgers.GetOutstandingAsync(type, ct));
    }

    /// <summary>Bucketed from each document's own due date.</summary>
    [HttpGet("ageing")]
    [ProducesResponseType(typeof(ApiResponse<AgeingDto>), 200)]
    public async Task<IActionResult> GetAgeing(string type, CancellationToken ct)
    {
        Check(type);
        return OkEnvelope(await ledgers.GetAgeingAsync(type, ct));
    }

    [HttpGet("ageing/export")]
    public async Task<IActionResult> ExportAgeing(string type, CancellationToken ct)
    {
        Check(type);
        return File(await ledgers.ExportAgeingAsync(type, ct), Xlsx, $"ageing-{type}.xlsx");
    }

    /// <summary>The documents behind an ageing figure. bucket = not-due, 1-30, 31-60, 61-90, 90+ or total.</summary>
    [HttpGet("ageing/documents")]
    [ProducesResponseType(typeof(ApiResponse<AgeingDocumentDto[]>), 200)]
    public async Task<IActionResult> GetAgeingDocuments(string type, [FromQuery] long? partyId, [FromQuery] string? bucket, CancellationToken ct)
    {
        Check(type);
        return OkEnvelope(await ledgers.GetAgeingDocumentsAsync(type, partyId, bucket, ct));
    }
}
