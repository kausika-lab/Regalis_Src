using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance.QuotationPayment;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>
/// quotation-payment kit (27 Nov 2026). The Payments screen's Quotations view: approved quotations with
/// the paid / outstanding position of their invoice. Read-only; payments are recorded against the invoice
/// (POST /api/invoices/{id}/payments). Staff only: the data spans customers.
/// </summary>
[StaffOnly]
[Route("api/payments/quotations")]
public class QuotationPaymentsController(IQuotationPaymentService quotationPayments) : ApiControllerBase
{
    [HttpGet]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<QuotationPaymentRowDto>>), 200)]
    public async Task<IActionResult> Get([FromQuery] QuotationPaymentListRequest request, CancellationToken ct) =>
        OkEnvelope(await quotationPayments.GetAsync(request, ct));

    [HttpGet("summary")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<QuotationPaymentSummaryDto>), 200)]
    public async Task<IActionResult> Summary([FromQuery] QuotationPaymentListRequest request, CancellationToken ct) =>
        OkEnvelope(await quotationPayments.GetSummaryAsync(request, ct));
}
