using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Invoices (API_PLAN §7). Manual invoices, cancel and resend arrive with
/// the finance screens on Day 6.</summary>
[Route("api/invoices")]
public class InvoicesController(IInvoiceService invoiceService) : ApiControllerBase
{
    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Invoice.View)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) =>
        OkEnvelope(await invoiceService.GetInvoiceAsync(id, ct));

    /// <summary>The caller's invoices: ?status=pending|paid.</summary>
    [HttpGet("my")]
    [HasPermission(PermissionCodes.Invoice.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy([FromQuery] InvoiceListRequest request, CancellationToken ct) =>
        OkEnvelope(await invoiceService.GetMyInvoicesAsync(request, ct));

    /// <summary>The GST invoice PDF, generated on demand from the snapshot.</summary>
    [HttpGet("{id:long}/pdf")]
    [HasPermission(PermissionCodes.Invoice.Print)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPdf(long id, CancellationToken ct)
    {
        var invoice = await invoiceService.GetInvoiceAsync(id, ct);
        var pdf = await invoiceService.GetInvoicePdfAsync(id, ct);
        var fileName = $"{invoice.InvoiceNumber.Replace('/', '-')}.pdf";
        return File(pdf, "application/pdf", fileName);
    }
}
