using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Reporting;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Reports &amp; Analysis (API_PLAN §12, M24). One tabular contract drives
/// the grid, the Excel export and the PDF export for all sixteen reports.</summary>
[Route("api/reports")]
public class ReportsController(IReportService reportService) : ApiControllerBase
{
    /// <summary>The landing-grid catalogue; unavailable keys are flagged, not hidden.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.Report.View)]
    [ProducesResponseType(typeof(ApiResponse<List<ReportInfo>>), StatusCodes.Status200OK)]
    public IActionResult GetCatalogue() => OkEnvelope(reportService.GetCatalogue());

    [HttpGet("{key}")]
    [HasPermission(PermissionCodes.Report.View)]
    [ProducesResponseType(typeof(ApiResponse<ReportResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(string key, [FromQuery] ReportFilterRequest filters,
        CancellationToken ct) =>
        OkEnvelope(await reportService.GetAsync(key, filters, ct));

    [HttpGet("{key}/export")]
    [HasPermission(PermissionCodes.Report.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(string key, [FromQuery] string format,
        [FromQuery] ReportFilterRequest filters, CancellationToken ct)
    {
        var export = await reportService.ExportAsync(key, format, filters, ct);
        return File(export.Content, export.ContentType, export.FileName);
    }
}
