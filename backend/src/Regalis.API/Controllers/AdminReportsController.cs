using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Reporting.Analytics;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>
/// Reports &amp; Analysis (Reports &amp; Analysis spec §9.4): the overview, the report
/// catalogue, running and exporting any report, favourites and schedules. Every route
/// is staff-only and checks the report's own permission again on the server — hiding a
/// button is presentation, not security (§13).
/// </summary>
[Route("api/admin/reports")]
[StaffOnly]
public class AdminReportsController(
    IReportOverviewService overview,
    IReportCatalogService catalog,
    IReportRunService runner,
    IReportScheduleService schedules,
    IDailyMetricsService metrics) : ApiControllerBase
{
    /// <summary>Everything the overview screen needs, in one response.</summary>
    [HttpGet("overview")]
    [HasPermission(PermissionCodes.Report.View)]
    [ProducesResponseType(typeof(ApiResponse<OverviewDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Overview([FromQuery] OverviewRequest request, CancellationToken ct) =>
        OkEnvelope(await overview.GetAsync(request, ct));

    /// <summary>The overview as a PDF, with the date range printed on it.</summary>
    [HttpGet("overview/pdf")]
    [HasPermission(PermissionCodes.Report.Export)]
    public async Task<IActionResult> OverviewPdf([FromQuery] OverviewRequest request, CancellationToken ct)
    {
        var file = await overview.ExportPdfAsync(request, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>The catalogue the library is drawn from, filtered to what this admin may see.</summary>
    [HttpGet("definitions")]
    [HasPermission(PermissionCodes.Report.View)]
    [ProducesResponseType(typeof(ApiResponse<ReportCatalogueDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Definitions(CancellationToken ct) =>
        OkEnvelope(await catalog.GetCatalogueAsync(ct));

    /// <summary>Type-ahead for the Customer filter: name, company, phone or email.</summary>
    [HttpGet("customers")]
    [HasPermission(PermissionCodes.Report.View)]
    [ProducesResponseType(typeof(ApiResponse<List<ReportCustomerOption>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Customers([FromQuery] string? q, CancellationToken ct) =>
        OkEnvelope(await catalog.SearchCustomersAsync(q, ct));

    [HttpGet("favourites")]
    [HasPermission(PermissionCodes.Report.View)]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Favourites(CancellationToken ct) => OkEnvelope(await catalog.GetFavouritesAsync(ct));

    [HttpPost("favourites/{key}")]
    [HasPermission(PermissionCodes.Report.View)]
    public async Task<IActionResult> AddFavourite(string key, CancellationToken ct)
    {
        await catalog.AddFavouriteAsync(key, ct);
        return OkEnvelope("Saved as a favourite.");
    }

    [HttpDelete("favourites/{key}")]
    [HasPermission(PermissionCodes.Report.View)]
    public async Task<IActionResult> RemoveFavourite(string key, CancellationToken ct)
    {
        await catalog.RemoveFavouriteAsync(key, ct);
        return OkEnvelope("Removed from favourites.");
    }

    [HttpGet("schedules")]
    [HasPermission(PermissionCodes.Report.View)]
    [ProducesResponseType(typeof(ApiResponse<List<ReportScheduleDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Schedules(CancellationToken ct) => OkEnvelope(await schedules.ListAsync(ct));

    [HttpPost("schedules")]
    [HasPermission(PermissionCodes.Report.Export)]
    [ProducesResponseType(typeof(ApiResponse<ReportScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CreateSchedule([FromBody] ReportScheduleRequest request, CancellationToken ct) =>
        OkEnvelope(await schedules.CreateAsync(request, ct), "The report will email itself.");

    [HttpPatch("schedules/{id:long}")]
    [HasPermission(PermissionCodes.Report.Export)]
    [ProducesResponseType(typeof(ApiResponse<ReportScheduleDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateSchedule(long id, [FromBody] ReportScheduleRequest request,
        CancellationToken ct) =>
        OkEnvelope(await schedules.UpdateAsync(id, request, ct), "Schedule saved.");

    /// <summary>Stops a schedule. It is kept, so it can be turned back on.</summary>
    [HttpDelete("schedules/{id:long}")]
    [HasPermission(PermissionCodes.Report.Export)]
    public async Task<IActionResult> StopSchedule(long id, CancellationToken ct)
    {
        await schedules.StopAsync(id, ct);
        return OkEnvelope("Schedule stopped.");
    }

    /// <summary>A large export built in the background, by the token in its emailed link.</summary>
    [HttpGet("exports/{token}")]
    [HasPermission(PermissionCodes.Report.Export)]
    public async Task<IActionResult> Download(string token, CancellationToken ct)
    {
        var file = await runner.DownloadAsync(token, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    /// <summary>Re-builds RPT_DailyMetric for a range of completed days.</summary>
    [HttpPost("metrics/rebuild")]
    [HasPermission(PermissionCodes.Report.Manage)]
    public async Task<IActionResult> RebuildMetrics([FromQuery] DateOnly from, [FromQuery] DateOnly to,
        CancellationToken ct)
    {
        var days = await metrics.RebuildAsync(from, to, ct);
        return OkEnvelope(days, $"Rebuilt {days} day(s).");
    }

    /// <summary>Runs a report: summary figures, chart series, one page of rows and the
    /// true totals over every matching row — in one response.</summary>
    [HttpPost("{key}/run")]
    [HasPermission(PermissionCodes.Report.View)]
    [ProducesResponseType(typeof(ApiResponse<ReportRunResult>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Run(string key, [FromBody] ReportRunRequest request, CancellationToken ct) =>
        OkEnvelope(await runner.RunAsync(key, request, ct));

    /// <summary>The file itself, or 202 with a message when it is built in the background.</summary>
    [HttpPost("{key}/export")]
    [HasPermission(PermissionCodes.Report.Export)]
    public async Task<IActionResult> Export(string key, [FromBody] ReportExportRequest request, CancellationToken ct)
    {
        var origin = Request.Headers.Origin.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(origin) && Uri.TryCreate(Request.Headers.Referer.FirstOrDefault(), UriKind.Absolute, out var referer))
        {
            origin = referer.GetLeftPart(UriPartial.Authority);
        }
        var outcome = await runner.ExportAsync(key, request, origin, ct);
        if (outcome.JobQueued)
        {
            return Accepted(ApiResponse<ReportExportOutcome>.Ok(outcome, outcome.Message ?? "Queued."));
        }
        return File(outcome.Content!, outcome.ContentType!, outcome.FileName);
    }

    /// <summary>Remembers which columns this admin shows, per report.</summary>
    [HttpPut("{key}/preferences")]
    [HasPermission(PermissionCodes.Report.View)]
    public async Task<IActionResult> SavePreferences(string key, [FromBody] ReportPreferenceRequest request,
        CancellationToken ct)
    {
        await catalog.SavePreferencesAsync(key, request, ct);
        return OkEnvelope("Saved.");
    }
}
