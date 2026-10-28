using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>Pay later at the front desk: what is owed, the receipts that settle it, and
/// each collector's register session (open, close with a count, Admin approval).</summary>
[Route("api/cash")]
[StaffOnly]
public class CashController(ICashCollectionService collections, ICashRegisterService registers) : ApiControllerBase
{
    /// <summary>Confirmed pay-later bookings with a balance, oldest due first.</summary>
    [HttpGet("outstanding")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<OutstandingPayLaterDto>>), 200)]
    public async Task<IActionResult> GetOutstanding([FromQuery] OutstandingListRequest request, CancellationToken ct)
    {
        return OkEnvelope(await collections.GetOutstandingAsync(request, ct));
    }

    /// <summary>Records a desk payment (cash, UPI or card) against a pay-later booking.</summary>
    [HttpPost("collections")]
    [HasPermission("PAYMENT.CASH_COLLECT")]
    [ProducesResponseType(typeof(ApiResponse<CashCollectionDto>), 200)]
    public async Task<IActionResult> Record(RecordCollectionRequest request, CancellationToken ct)
    {
        return OkEnvelope(await collections.RecordAsync(request, ct));
    }

    [HttpGet("collections")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CashCollectionDto>>), 200)]
    public async Task<IActionResult> GetCollections([FromQuery] CashCollectionListRequest request, CancellationToken ct)
    {
        return OkEnvelope(await collections.GetCollectionsAsync(request, ct));
    }

    [HttpGet("collections/{id:long}")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<CashCollectionDto>), 200)]
    public async Task<IActionResult> GetCollection(long id, CancellationToken ct)
    {
        return OkEnvelope(await collections.GetCollectionAsync(id, ct));
    }

    /// <summary>The receipt as a PDF (a voided one prints with a VOID stamp).</summary>
    [HttpGet("collections/{id:long}/receipt")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetReceipt(long id, CancellationToken ct)
    {
        var (content, fileName) = await collections.GetReceiptPdfAsync(id, ct);
        return File(content, "application/pdf", fileName);
    }

    /// <summary>Voids a receipt (Admin only): the row stays, its totals are reversed.</summary>
    [HttpPost("collections/{id:long}/void")]
    [HasPermission("PAYMENT.CASH_VOID")]
    [ProducesResponseType(typeof(ApiResponse<CashCollectionDto>), 200)]
    public async Task<IActionResult> Void(long id, VoidCollectionRequest request, CancellationToken ct)
    {
        return OkEnvelope(await collections.VoidAsync(id, request, ct));
    }

    /// <summary>The caller's register session at a location today (null when none).</summary>
    [HttpGet("register/current")]
    [HasPermission("PAYMENT.CASH_COLLECT")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterSessionDto>), 200)]
    public async Task<IActionResult> GetCurrent([FromQuery] int locationId, CancellationToken ct)
    {
        return OkEnvelope(await registers.GetCurrentAsync(locationId, ct));
    }

    [HttpPost("register/open")]
    [HasPermission("PAYMENT.CASH_CLOSE")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterSessionDto>), 200)]
    public async Task<IActionResult> Open(OpenRegisterRequest request, CancellationToken ct)
    {
        return OkEnvelope(await registers.OpenAsync(request, ct));
    }

    /// <summary>Closes the session with the counted cash; a difference needs a reason.</summary>
    [HttpPost("register/{id:long}/close")]
    [HasPermission("PAYMENT.CASH_CLOSE")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterSessionDto>), 200)]
    public async Task<IActionResult> Close(long id, CloseRegisterRequest request, CancellationToken ct)
    {
        return OkEnvelope(await registers.CloseAsync(id, request, ct));
    }

    /// <summary>Admin approves the close and takes the cash.</summary>
    [HttpPost("register/{id:long}/approve")]
    [HasPermission("PAYMENT.CASH_APPROVE")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterSessionDto>), 200)]
    public async Task<IActionResult> Approve(long id, CancellationToken ct)
    {
        return OkEnvelope(await registers.ApproveAsync(id, ct));
    }

    [HttpGet("register/{id:long}")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<CashRegisterSessionDto>), 200)]
    public async Task<IActionResult> GetSession(long id, CancellationToken ct)
    {
        return OkEnvelope(await registers.GetAsync(id, ct));
    }

    [HttpGet("register")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<CashRegisterSessionDto>>), 200)]
    public async Task<IActionResult> GetSessions([FromQuery] CashRegisterListRequest request, CancellationToken ct)
    {
        return OkEnvelope(await registers.GetSessionsAsync(request, ct));
    }
}
