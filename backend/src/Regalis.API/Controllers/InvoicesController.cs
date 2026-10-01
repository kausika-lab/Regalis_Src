using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Abstractions;
using Regalis.Application.Finance;
using Regalis.Application.Masters;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Invoices (API_PLAN §7). Manual invoices, cancel and resend arrive with
/// the finance screens on Day 6.</summary>
[Route("api/invoices")]
public class InvoicesController(IInvoiceService invoiceService, ISettingService settings,
    ISettingAdminService settingAdmin) : ApiControllerBase
{
    /// <summary>The Terms &amp; Conditions printed at the end of every invoice PDF (one
    /// text for all customers; invoices raised from a quotation print the quotation's own).
    /// Read when each PDF is made, so an edit shows on every invoice at once.</summary>
    [HttpGet("terms")]
    [StaffOnly]
    [HasPermission(PermissionCodes.Invoice.View)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceTermsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTerms(CancellationToken ct) =>
        OkEnvelope(new InvoiceTermsDto(
            await settings.GetStringAsync(AppSettingKeys.Invoice.TermsAndConditions, "", ct)));

    [HttpPut("terms")]
    [StaffOnly]
    [HasPermission(PermissionCodes.Invoice.Edit)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceTermsDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SaveTerms(InvoiceTermsDto request, CancellationToken ct)
    {
        var text = (request.TermsAndConditions ?? "").Trim();
        await settingAdmin.UpdateSettingsAsync(new UpdateAppSettingsRequest(
            [new AppSettingValue(AppSettingKeys.Invoice.TermsAndConditions, text)]), ct);
        return OkEnvelope(new InvoiceTermsDto(text), "Invoice terms saved.");
    }

    /// <summary>Admin list with the common filter set; customers use /invoices/my.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.Invoice.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<InvoiceDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] AdminInvoiceListRequest request, CancellationToken ct)
    {
        if (User.FindFirst("user_type")?.Value == "1")
        {
            return OkEnvelope(await invoiceService.GetMyInvoicesAsync(
                new InvoiceListRequest { Page = request.Page, PageSize = request.PageSize }, ct));
        }
        return OkEnvelope(await invoiceService.GetInvoicesAsync(request, ct));
    }

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

    /// <summary>Raises the invoice for an approved quotation (document §9, rules 4
    /// and 5). The quotation is the only source; a second attempt is refused both here
    /// and by the unique index behind it.</summary>
    [HttpPost("from-quotation/{quotationId:long}")]
    [HasPermission(PermissionCodes.Invoice.Create)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateFromQuotation(long quotationId, CancellationToken ct)
    {
        var invoice = await invoiceService.CreateFromQuotationAsync(quotationId, ct);
        return CreatedEnvelope($"/api/invoices/{invoice.InvoiceId}", invoice, "Invoice raised.");
    }

    /// <summary>Marks a pending invoice as delivered to the customer.</summary>
    [HttpPost("{id:long}/send")]
    [HasPermission(PermissionCodes.Invoice.Edit)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Send(long id, CancellationToken ct) =>
        OkEnvelope(await invoiceService.SendAsync(id, ct), "Invoice marked as sent.");

    /// <summary>An issued invoice is never edited or deleted — this is the
    /// correction path, and it is refused once any payment exists.</summary>
    [HttpPost("{id:long}/cancel")]
    [HasPermission(PermissionCodes.Invoice.Edit)]
    [ProducesResponseType(typeof(ApiResponse<InvoiceDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long id, CancelInvoiceRequest request,
        CancellationToken ct) =>
        OkEnvelope(await invoiceService.CancelAsync(id, request.Reason, ct), "Invoice cancelled.");
}

/// <summary>Body for the cancel route — a reason is mandatory.</summary>
public record CancelInvoiceRequest(string Reason);
