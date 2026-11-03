using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance.Billing;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>Purchase bills (build doc §4-5): what Regalis owes suppliers.</summary>
[Route("api/purchase-bills")]
[StaffOnly]
public class PurchaseBillsController(IPurchaseBillService bills) : ApiControllerBase
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet]
    [HasPermission("BILL.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillListResultDto>), 200)]
    public async Task<IActionResult> GetBills([FromQuery] PurchaseBillListRequest request, CancellationToken ct) => OkEnvelope(await bills.GetBillsAsync(request, ct));

    [HttpGet("summary")]
    [HasPermission("BILL.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillSummaryDto>), 200)]
    public async Task<IActionResult> GetSummary(CancellationToken ct) => OkEnvelope(await bills.GetSummaryAsync(ct));

    [HttpGet("export")]
    [HasPermission("BILL.VIEW")]
    public async Task<IActionResult> Export([FromQuery] PurchaseBillListRequest request, CancellationToken ct) =>
        File(await bills.ExportAsync(request, ct), Xlsx, "purchase-bills.xlsx");

    [HttpGet("form-options")]
    [HasPermission("BILL.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillFormOptionsDto>), 200)]
    public async Task<IActionResult> GetFormOptions(CancellationToken ct) => OkEnvelope(await bills.GetFormOptionsAsync(ct));

    /// <summary>Lines of a purchase order, to start a bill from it.</summary>
    [HttpGet("from-po/{purchaseOrderId:long}")]
    [HasPermission("BILL.CREATE")]
    [ProducesResponseType(typeof(ApiResponse<PoBillPrefillDto>), 200)]
    public async Task<IActionResult> GetPoPrefill(long purchaseOrderId, CancellationToken ct) => OkEnvelope(await bills.GetPoPrefillAsync(purchaseOrderId, ct));

    [HttpGet("{id:long}")]
    [HasPermission("BILL.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillDto>), 200)]
    public async Task<IActionResult> GetBill(long id, CancellationToken ct) => OkEnvelope(await bills.GetBillAsync(id, ct));

    /// <summary>Creates a bill. A duplicate supplier bill number is refused with 409 and the existing bill's id in the error code.</summary>
    [HttpPost]
    [HasPermission("BILL.CREATE")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillDto>), 200)]
    public async Task<IActionResult> Create(SavePurchaseBillRequest request, CancellationToken ct) => OkEnvelope(await bills.CreateAsync(request, ct), "Bill saved.");

    [HttpPut("{id:long}")]
    [HasPermission("BILL.CREATE")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillDto>), 200)]
    public async Task<IActionResult> Update(long id, SavePurchaseBillRequest request, CancellationToken ct) => OkEnvelope(await bills.UpdateAsync(id, request, ct), "Bill saved.");

    [HttpPost("{id:long}/submit")]
    [HasPermission("BILL.CREATE")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillDto>), 200)]
    public async Task<IActionResult> Submit(long id, CancellationToken ct) => OkEnvelope(await bills.SubmitAsync(id, ct), "Bill submitted.");

    [HttpPost("{id:long}/approve")]
    [HasPermission("BILL.APPROVE")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillDto>), 200)]
    public async Task<IActionResult> Approve(long id, BillDecisionRequest request, CancellationToken ct) => OkEnvelope(await bills.ApproveAsync(id, request, ct), "Bill approved.");

    [HttpPost("{id:long}/reject")]
    [HasPermission("BILL.APPROVE")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillDto>), 200)]
    public async Task<IActionResult> Reject(long id, BillDecisionRequest request, CancellationToken ct) => OkEnvelope(await bills.RejectAsync(id, request, ct), "Bill rejected.");

    [HttpPost("{id:long}/hold")]
    [HasPermission("BILL.APPROVE")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillDto>), 200)]
    public async Task<IActionResult> Hold(long id, BillDecisionRequest request, CancellationToken ct) => OkEnvelope(await bills.HoldAsync(id, request, ct), "Bill put on hold.");

    [HttpPost("{id:long}/release")]
    [HasPermission("BILL.APPROVE")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillDto>), 200)]
    public async Task<IActionResult> Release(long id, CancellationToken ct) => OkEnvelope(await bills.ReleaseAsync(id, ct), "Bill released.");

    [HttpPost("{id:long}/cancel")]
    [HasPermission("BILL.CREATE")]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillDto>), 200)]
    public async Task<IActionResult> Cancel(long id, BillDecisionRequest request, CancellationToken ct) => OkEnvelope(await bills.CancelAsync(id, request, ct), "Bill cancelled.");

    /// <summary>PDF, JPG or PNG, up to 10 MB, content-sniffed; up to 10 per bill.</summary>
    [HttpPost("{id:long}/attachments")]
    [HasPermission("BILL.CREATE")]
    [RequestSizeLimit(12582912L)]
    [ProducesResponseType(typeof(ApiResponse<PurchaseBillDto>), 200)]
    public async Task<IActionResult> AddAttachment(long id, IFormFile file, CancellationToken ct)
    {
        await using Stream stream = file.OpenReadStream();
        return OkEnvelope(await bills.AddAttachmentAsync(id, new AttachmentUploadInput(file.FileName, file.ContentType, file.Length, stream), ct), "Document added.");
    }

    [HttpGet("{id:long}/attachments/{attachmentId:long}")]
    [HasPermission("BILL.VIEW")]
    public async Task<IActionResult> GetAttachment(long id, long attachmentId, CancellationToken ct)
    {
        AttachmentFileDto file = await bills.GetAttachmentAsync(id, attachmentId, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }
}

/// <summary>Vendor payments (build doc §6): one payment, several bills, less TDS.</summary>
[StaffOnly]
public class VendorPaymentsController(IVendorPaymentService vendorPayments, IPurchaseBillService bills) : ApiControllerBase
{
    /// <summary>Approved, unpaid bills of a supplier, oldest due first, and their advance.</summary>
    [HttpGet("api/suppliers/{id:int}/outstanding-bills")]
    [HasPermission("BILL.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<SupplierPayablesDto>), 200)]
    public async Task<IActionResult> GetPayables(int id, CancellationToken ct) => OkEnvelope(await vendorPayments.GetSupplierPayablesAsync(id, ct));

    [HttpGet("api/suppliers/{id:int}/tax-profile")]
    [HasPermission("SUPPLIER.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<SupplierOptionDto>), 200)]
    public async Task<IActionResult> GetTaxProfile(int id, CancellationToken ct) => OkEnvelope(await bills.GetSupplierProfileAsync(id, ct));

    [HttpPut("api/suppliers/{id:int}/tax-profile")]
    [HasPermission("SUPPLIER.EDIT")]
    [ProducesResponseType(typeof(ApiResponse<SupplierOptionDto>), 200)]
    public async Task<IActionResult> SaveTaxProfile(int id, SaveSupplierTaxProfileRequest request, CancellationToken ct) =>
        OkEnvelope(await bills.SaveSupplierProfileAsync(id, request, ct), "Saved.");

    [HttpGet("api/vendor-payments")]
    [HasPermission("BILL.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<VendorPaymentDto>>), 200)]
    public async Task<IActionResult> GetPayments([FromQuery] VendorPaymentListRequest request, CancellationToken ct) => OkEnvelope(await vendorPayments.GetPaymentsAsync(request, ct));

    [HttpGet("api/vendor-payments/{id:long}")]
    [HasPermission("BILL.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<VendorPaymentDto>), 200)]
    public async Task<IActionResult> GetPayment(long id, CancellationToken ct) => OkEnvelope(await vendorPayments.GetPaymentAsync(id, ct));

    /// <summary>Posts a payment: allocations, TDS payable and bill statuses in one transaction.</summary>
    [HttpPost("api/vendor-payments")]
    [HasPermission("BILL.PAY")]
    [ProducesResponseType(typeof(ApiResponse<VendorPaymentResultDto>), 200)]
    public async Task<IActionResult> Create(CreateVendorPaymentRequest request, CancellationToken ct)
    {
        VendorPaymentResultDto result = await vendorPayments.CreateAsync(request, ct);
        return OkEnvelope(result, result.Message);
    }

    [HttpPost("api/vendor-payments/{id:long}/reverse")]
    [HasPermission("PAYMENT.REVERSE")]
    [ProducesResponseType(typeof(ApiResponse<VendorPaymentDto>), 200)]
    public async Task<IActionResult> Reverse(long id, ReversePaymentRequest request, CancellationToken ct) =>
        OkEnvelope(await vendorPayments.ReverseAsync(id, request, ct), "Payment reversed.");
}
