using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance.Billing;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>Recording a customer payment against an invoice (build doc §3) - cash, UPI, bank
/// transfer, cheque, card or wallet. A new path with no gateway in it; the gateway's
/// invoice-verify endpoint is not used.</summary>
[StaffOnly]
public class CustomerPaymentsController(ICustomerPaymentService payments) : ApiControllerBase
{
    /// <summary>What the Record payment dialog needs: totals, wallet balance, accounts, earlier payments.</summary>
    [HttpGet("api/invoices/{id:long}/payment-context")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<InvoicePaymentContextDto>), 200)]
    public async Task<IActionResult> GetContext(long id, CancellationToken ct) => OkEnvelope(await payments.GetContextAsync(id, ct));

    [HttpGet("api/invoices/{id:long}/payments")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<InvoicePaymentDto[]>), 200)]
    public async Task<IActionResult> GetForInvoice(long id, CancellationToken ct) => OkEnvelope(await payments.GetForInvoiceAsync(id, ct));

    /// <summary>Records a payment. The outstanding amount is checked inside the transaction.</summary>
    [HttpPost("api/invoices/{id:long}/payments")]
    [HasPermission("PAYMENT.RECORD")]
    [ProducesResponseType(typeof(ApiResponse<RecordInvoicePaymentResult>), 200)]
    public async Task<IActionResult> Record(long id, RecordInvoicePaymentRequest request, CancellationToken ct)
    {
        RecordInvoicePaymentResult result = await payments.RecordAsync(id, request, ct);
        return OkEnvelope(result, result.Message);
    }

    /// <summary>Every payment, whatever its mode, with the receipt and the invoice it settled.</summary>
    [HttpGet("api/payments/register")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<PaymentRegisterRowDto>>), 200)]
    public async Task<IActionResult> GetRegister([FromQuery] PaymentRegisterRequest request, CancellationToken ct) =>
        OkEnvelope(await payments.GetRegisterAsync(request, ct));

    /// <summary>Reverses a recorded payment with a reason: writes the opposite entry, keeps both.</summary>
    [HttpPost("api/payments/{id:long}/reverse")]
    [HasPermission("PAYMENT.REVERSE")]
    [ProducesResponseType(typeof(ApiResponse<InvoicePaymentDto>), 200)]
    public async Task<IActionResult> Reverse(long id, ReversePaymentRequest request, CancellationToken ct) =>
        OkEnvelope(await payments.ReverseAsync(id, request, ct), "Payment reversed.");

    [HttpPost("api/payments/{id:long}/receipt/email")]
    [HasPermission("PAYMENT.VIEW")]
    [ProducesResponseType(typeof(ApiResponse), 200)]
    public async Task<IActionResult> EmailReceipt(long id, CancellationToken ct) => OkEnvelope(await payments.EmailReceiptAsync(id, ct));

    [HttpGet("api/customers/{id:long}/tax-profile")]
    [HasPermission("CUSTOMER.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<CustomerTaxProfileDto>), 200)]
    public async Task<IActionResult> GetTaxProfile(long id, CancellationToken ct) => OkEnvelope(await payments.GetCustomerTaxProfileAsync(id, ct));

    [HttpPut("api/customers/{id:long}/tax-profile")]
    [HasPermission("CUSTOMER.EDIT")]
    [ProducesResponseType(typeof(ApiResponse<CustomerTaxProfileDto>), 200)]
    public async Task<IActionResult> SaveTaxProfile(long id, SaveCustomerTaxProfileRequest request, CancellationToken ct) =>
        OkEnvelope(await payments.SaveCustomerTaxProfileAsync(id, request, ct), "Saved.");
}
