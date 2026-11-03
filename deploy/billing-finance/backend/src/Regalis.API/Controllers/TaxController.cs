using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance.Billing;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>TDS payable and receivable, challans, certificates and rates (build doc §10).</summary>
[Route("api/tds")]
[StaffOnly]
public class TdsController(ITdsService tds) : ApiControllerBase
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("summary")]
    [HasPermission("TDS.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<TdsSummaryDto>), 200)]
    public async Task<IActionResult> GetSummary([FromQuery] string? fy, [FromQuery] byte? quarter, CancellationToken ct) => OkEnvelope(await tds.GetSummaryAsync(fy, quarter, ct));

    [HttpGet("payable")]
    [HasPermission("TDS.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<TdsEntryDto[]>), 200)]
    public async Task<IActionResult> GetPayable([FromQuery] string? fy, [FromQuery] byte? quarter, [FromQuery] string? section, [FromQuery] bool? open, CancellationToken ct) =>
        OkEnvelope(await tds.GetEntriesAsync(1, fy, quarter, section, open, ct));

    [HttpGet("receivable")]
    [HasPermission("TDS.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<TdsEntryDto[]>), 200)]
    public async Task<IActionResult> GetReceivable([FromQuery] string? fy, [FromQuery] byte? quarter, [FromQuery] string? section, [FromQuery] bool? open, CancellationToken ct) =>
        OkEnvelope(await tds.GetEntriesAsync(2, fy, quarter, section, open, ct));

    /// <summary>direction = 1 payable (the 26Q working), 2 receivable.</summary>
    [HttpGet("export")]
    [HasPermission("TDS.VIEW")]
    public async Task<IActionResult> Export([FromQuery] byte direction, [FromQuery] string? fy, [FromQuery] byte? quarter, CancellationToken ct) =>
        File(await tds.ExportAsync(direction, fy, quarter, ct), Xlsx, direction == 1 ? "tds-payable.xlsx" : "tds-receivable.xlsx");

    [HttpGet("challans")]
    [HasPermission("TDS.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<TdsChallanDto[]>), 200)]
    public async Task<IActionResult> GetChallans([FromQuery] string? fy, CancellationToken ct) => OkEnvelope(await tds.GetChallansAsync(fy, ct));

    [HttpPost("challans")]
    [HasPermission("TDS.MANAGE")]
    [ProducesResponseType(typeof(ApiResponse<TdsChallanDto>), 200)]
    public async Task<IActionResult> RecordChallan(RecordChallanRequest request, CancellationToken ct) => OkEnvelope(await tds.RecordChallanAsync(request, ct), "Challan recorded.");

    /// <summary>Multipart: certificateNo, receivedOn, optional file (Form 16A PDF).</summary>
    [HttpPost("entries/{id:long}/certificate")]
    [HasPermission("TDS.MANAGE")]
    [RequestSizeLimit(12582912L)]
    [ProducesResponseType(typeof(ApiResponse<TdsEntryDto>), 200)]
    public async Task<IActionResult> SaveCertificate(long id, [FromForm] string certificateNo, [FromForm] System.DateOnly receivedOn, IFormFile? file, CancellationToken ct)
    {
        if (file == null) return OkEnvelope(await tds.SaveCertificateAsync(id, new TdsCertificateRequest(certificateNo, receivedOn), null, ct), "Certificate saved.");
        await using Stream stream = file.OpenReadStream();
        return OkEnvelope(await tds.SaveCertificateAsync(id, new TdsCertificateRequest(certificateNo, receivedOn),
            new AttachmentUploadInput(file.FileName, file.ContentType, file.Length, stream), ct), "Certificate saved.");
    }

    [HttpGet("entries/{id:long}/certificate")]
    [HasPermission("TDS.VIEW")]
    public async Task<IActionResult> GetCertificate(long id, CancellationToken ct)
    {
        AttachmentFileDto file = await tds.GetCertificateAsync(id, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("sections")]
    [HasPermission("TDS.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<TdsSectionRowDto[]>), 200)]
    public async Task<IActionResult> GetSections(CancellationToken ct) => OkEnvelope(await tds.GetSectionsAsync(ct));

    /// <summary>A new rate from a date; the old one is closed the day before.</summary>
    [HttpPost("sections")]
    [HasPermission("SETTING.EDIT")]
    [ProducesResponseType(typeof(ApiResponse<TdsSectionRowDto>), 200)]
    public async Task<IActionResult> SaveRate(SaveTdsRateRequest request, CancellationToken ct) => OkEnvelope(await tds.SaveRateAsync(request, ct), "Rate saved.");
}

/// <summary>GST: this month, registers, GSTR-2B reconciliation, return tracker (build doc §11).</summary>
[Route("api/gst")]
[StaffOnly]
public class GstController(IGstService gst) : ApiControllerBase
{
    private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    [HttpGet("summary")]
    [HasPermission("GST.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<GstSummaryDto>), 200)]
    public async Task<IActionResult> GetSummary([FromQuery] string period, CancellationToken ct) => OkEnvelope(await gst.GetSummaryAsync(period, ct));

    [HttpGet("sales-register")]
    [HasPermission("GST.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<GstRegisterDto>), 200)]
    public async Task<IActionResult> GetSales([FromQuery] string period, CancellationToken ct) => OkEnvelope(await gst.GetSalesRegisterAsync(period, ct));

    [HttpGet("purchase-register")]
    [HasPermission("GST.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<GstRegisterDto>), 200)]
    public async Task<IActionResult> GetPurchases([FromQuery] string period, CancellationToken ct) => OkEnvelope(await gst.GetPurchaseRegisterAsync(period, ct));

    /// <summary>kind = sales or purchase.</summary>
    [HttpGet("{kind}-register/export")]
    [HasPermission("GST.VIEW")]
    public async Task<IActionResult> ExportRegister(string kind, [FromQuery] string period, CancellationToken ct) =>
        File(await gst.ExportRegisterAsync(kind, period, ct), Xlsx, $"gst-{kind}-register-{period}.xlsx");

    [HttpPost("gstr2b/upload")]
    [HasPermission("GST.MANAGE")]
    [RequestSizeLimit(12582912L)]
    [ProducesResponseType(typeof(ApiResponse<Gstr2bResultDto>), 200)]
    public async Task<IActionResult> Upload2b([FromForm] string period, IFormFile file, CancellationToken ct)
    {
        await using Stream stream = file.OpenReadStream();
        return OkEnvelope(await gst.Upload2bAsync(period, new BankUploadInput(file.FileName, stream, file.Length), ct), "GSTR-2B compared.");
    }

    [HttpGet("gstr2b")]
    [HasPermission("GST.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<Gstr2bResultDto>), 200)]
    public async Task<IActionResult> Get2b([FromQuery] string period, CancellationToken ct) => OkEnvelope(await gst.Get2bAsync(period, ct));

    [HttpGet("returns")]
    [HasPermission("GST.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<GstReturnRowDto[]>), 200)]
    public async Task<IActionResult> GetReturns([FromQuery] string? fy, CancellationToken ct) => OkEnvelope(await gst.GetReturnsAsync(fy, ct));

    [HttpPut("returns")]
    [HasPermission("GST.MANAGE")]
    [ProducesResponseType(typeof(ApiResponse<GstReturnRowDto>), 200)]
    public async Task<IActionResult> SaveReturn(SaveGstReturnRequest request, CancellationToken ct) => OkEnvelope(await gst.SaveReturnAsync(request, ct), "Saved.");

    [HttpGet("rates")]
    [HasPermission("GST.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<GstRateOptionRowDto[]>), 200)]
    public async Task<IActionResult> GetRates(CancellationToken ct) => OkEnvelope(await gst.GetRatesAsync(ct));

    [HttpPost("rates")]
    [HasPermission("SETTING.EDIT")]
    [ProducesResponseType(typeof(ApiResponse<GstRateOptionRowDto>), 200)]
    public async Task<IActionResult> CreateRate(SaveGstRateRequest request, CancellationToken ct) => OkEnvelope(await gst.SaveRateAsync(null, request, ct), "Saved.");

    [HttpPut("rates/{id:int}")]
    [HasPermission("SETTING.EDIT")]
    [ProducesResponseType(typeof(ApiResponse<GstRateOptionRowDto>), 200)]
    public async Task<IActionResult> UpdateRate(int id, SaveGstRateRequest request, CancellationToken ct) => OkEnvelope(await gst.SaveRateAsync(id, request, ct), "Saved.");
}
