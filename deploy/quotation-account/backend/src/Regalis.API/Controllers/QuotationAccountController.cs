using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance.QuotationAccount;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>
/// quotation-account kit (27 Nov 2026). Convert to invoice for an approved quotation, with optional
/// corrections to its email / mobile when it has no customer account yet. The customer is linked (same
/// email, else same mobile) or created by the Customers module; never a staff account.
/// </summary>
[Route("api/quotations")]
public class QuotationAccountController(IQuotationAccountService quotationAccounts) : ApiControllerBase
{
    [HttpPost("{id:long}/raise-invoice")]
    [HasPermission("INVOICE.CREATE")]
    [ProducesResponseType(typeof(ApiResponse<QuotationInvoiceRaisedDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<QuotationInvoiceRaisedDto>), 200)]
    public async Task<IActionResult> RaiseInvoice(long id, [FromBody] RaiseQuotationInvoiceRequest? request, CancellationToken ct)
    {
        QuotationInvoiceRaisedDto result = await quotationAccounts.RaiseInvoiceAsync(id, request ?? new RaiseQuotationInvoiceRequest(null, null), ct);
        if (!result.InvoiceCreated)
        {
            return OkEnvelope(result, result.Message);
        }
        return CreatedEnvelope($"/api/invoices/{result.InvoiceId}", result, result.Message);
    }
}
