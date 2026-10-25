using System.Text.Json;

namespace Regalis.Application.Reporting.Analytics;

// ── The report catalogue (spec §5, §6) ──────────────────────────────────────

public record ReportOption(string Value, string Label);

/// <summary>One control in a report's filter bar. Kind: dateRange | location | select |
/// multi | customer | toggle. The filter bar is one component driven by these.</summary>
public record ReportFilterDefinition(
    string Key,
    string Label,
    string Kind,
    List<ReportOption> Options,
    List<string> Default,
    string? AllLabel = null);

/// <summary>Type: text | number | money | percent | hours | date | datetime | status.</summary>
public record ReportColumnDefinition(
    string Key,
    string Header,
    string Type,
    bool Sortable,
    bool Totalled,
    bool DefaultVisible);

public record ReportDefinitionDto(
    string Key,
    string Name,
    string Description,
    string Group,
    string Chip,
    List<string> Keywords,
    string RowNoun,
    string DefaultSort,
    string DefaultRange,
    string? Hint,
    string? LiveFigure,
    List<ReportFilterDefinition> Filters,
    List<ReportColumnDefinition> Columns,
    bool IsFavourite,
    DateTime? LastOpenedUtc,
    int? LastRowCount,
    DateTime? NextEmailUtc);

public record ReportGroupDto(string Key, string Label, string Description, int Slot);

public record ReportCatalogueDto(
    List<ReportGroupDto> Groups,
    List<ReportDefinitionDto> Reports,
    /// <summary>The four report keys for the top of the library.</summary>
    List<string> Pinned,
    /// <summary>True when the admin has starred nothing and Pinned is the account's
    /// four most opened reports ("Most used").</summary>
    bool PinnedAreMostUsed,
    List<ReportOption> Locations,
    bool CanExport,
    bool CanManage);

// ── Running a report (spec §4, §9.4) ────────────────────────────────────────

public class ReportRunRequest
{
    /// <summary>Filter values by filter key. A missing key takes the filter's default;
    /// an empty list means "all".</summary>
    public Dictionary<string, JsonElement>? Filters { get; set; }
    public int Page { get; set; } = 1;
    /// <summary>"column:asc" or "column:desc".</summary>
    public string? Sort { get; set; }
    /// <summary>Narrows the rows of this result only; it never widens the filters.</summary>
    public string? Search { get; set; }
    /// <summary>True on the first run after the page opens: counts towards "Most used".</summary>
    public bool Opened { get; set; }
}

public record ReportChipDto(string Key, string Label);

/// <summary>A summary figure. ChangeKind: percent | absolute | none (previous was zero:
/// "no comparison") | null (this figure is never compared). Value null means it cannot
/// be calculated, and the screen shows a dash — never a zero.</summary>
public record SummaryFigureDto(
    string Key,
    string Label,
    decimal? Value,
    string Format,
    decimal? Previous,
    string? ChangeKind,
    decimal? Change,
    bool UpIsGood,
    string? Sub);

public record ChartPointDto(string Label, string? Date, decimal? Value, decimal? Value2 = null);

/// <summary>Slot 1–5 is the fixed palette (gold, blue, clay, green, plum), 0 is "Other"
/// grey. Band, when set, is the occupancy band that colours the bar instead.</summary>
public record ChartSliceDto(string Label, decimal Value, decimal Share, int Slot, string? Sub = null,
    string? Band = null);

public record ChartSeriesDto(string Name, int Slot);

public record ChartStackDto(string Label, string? Date, List<decimal> Values, decimal Total);

public record HeatCellDto(decimal? Percent, decimal People, int Step);

/// <summary>
/// A chart already shaped by the server (spec §14.3): the browser never groups or totals.
/// Kind: columns | line | doughnut | stackedbar | hbars | stackedcolumns | heatmap.
/// </summary>
public record ChartDto
{
    public string Id { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Title { get; init; } = "";
    public string? Subtitle { get; init; }
    /// <summary>count | money | percent | hours.</summary>
    public string ValueFormat { get; init; } = "count";
    public string? Value2Format { get; init; }
    public string? Value2Label { get; init; }
    public List<ChartPointDto>? Points { get; init; }
    public List<ChartPointDto>? Previous { get; init; }
    public List<ChartSliceDto>? Slices { get; init; }
    public List<ChartSeriesDto>? Series { get; init; }
    public List<ChartStackDto>? Stacks { get; init; }
    public List<string>? RowLabels { get; init; }
    public List<string>? ColLabels { get; init; }
    public List<List<HeatCellDto>>? Cells { get; init; }
    public decimal? Total { get; init; }
    public int? HighlightIndex { get; init; }
    public string? Note { get; init; }
}

public record ReportRunResult(
    string Key,
    string Name,
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    string Sentence,
    List<ReportChipDto> Chips,
    List<SummaryFigureDto> Summary,
    List<ChartDto> Charts,
    List<ReportColumnDefinition> Columns,
    List<Dictionary<string, object?>> Rows,
    Dictionary<string, object?> Totals,
    int TotalRows,
    int Page,
    int PageSize,
    string Sort,
    string RowNoun,
    /// <summary>The filters as the server applied them, defaults filled in — what a
    /// "Clear" goes back to is the definition's defaults, not this.</summary>
    Dictionary<string, List<string>> AppliedFilters,
    /// <summary>The columns this admin chose to show, in their order; null = the defaults.</summary>
    List<string>? SavedColumns);

public class ReportExportRequest : ReportRunRequest
{
    /// <summary>xlsx | pdf.</summary>
    public string Format { get; set; } = "xlsx";
}

/// <summary>Either a file (Content set) or a background job (JobQueued).</summary>
public record ReportExportOutcome(
    byte[]? Content,
    string? ContentType,
    string? FileName,
    bool JobQueued,
    int RowCount,
    string? Message);

public record ReportDownload(byte[] Content, string ContentType, string FileName);

public class ReportPreferenceRequest
{
    public List<string>? Columns { get; set; }
}

public record ReportCustomerOption(long CustomerId, string Name, string? Company, string? Mobile, string Email);

// ── The overview (spec §3) ──────────────────────────────────────────────────

public class OverviewRequest
{
    /// <summary>7d | 30d | 90d | fy | custom. Omitted: the admin's last choice.</summary>
    public string? Preset { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? LocationId { get; set; }
    public bool? Compare { get; set; }
}

public record KpiCardDto(
    string Key,
    string Label,
    decimal? Value,
    string Format,
    decimal? Previous,
    string? ChangeKind,
    decimal? Change,
    string PreviousText,
    List<decimal?> Sparkline,
    int Slot,
    string ReportKey);

public record RevenuePointDto(string Date, string Label, decimal Revenue, int Bookings);

public record RevenueSeriesDto(
    string DefaultGrouping,
    Dictionary<string, List<RevenuePointDto>> Current,
    Dictionary<string, List<RevenuePointDto>> Previous,
    decimal Total);

public record MoneyOwedDto(List<ChartSliceDto> Buckets, decimal Total, int Invoices);

public record TopCustomerDto(long? CustomerId, string Name, string? Company, decimal Revenue, int Bookings);

public record TopCustomersDto(List<TopCustomerDto> Rows, TopCustomerDto? EveryoneElse, decimal Total,
    string Sentence);

public record ShortcutDto(string ReportKey, string Name, string Description);

public record OverviewDto(
    string Preset,
    DateOnly From,
    DateOnly To,
    DateOnly PreviousFrom,
    DateOnly PreviousTo,
    int Days,
    string PeriodText,
    bool Compare,
    int? LocationId,
    List<ReportOption> Locations,
    bool CalculatedLive,
    bool HasData,
    List<KpiCardDto> Cards,
    RevenueSeriesDto? Revenue,
    ChartDto? RevenueByCategory,
    string? BiggestMover,
    ChartDto BookingsByType,
    ChartDto Heatmap,
    string? Quietest,
    ChartDto Busiest,
    string? BusiestNote,
    MoneyOwedDto? MoneyOwed,
    TopCustomersDto? TopCustomers,
    List<ShortcutDto> Shortcuts,
    bool CanFinance,
    bool CanCustomers,
    bool CanExport);

// ── Schedules (spec §12) ────────────────────────────────────────────────────

public class ReportScheduleRequest
{
    public string ReportKey { get; set; } = "";
    public Dictionary<string, JsonElement>? Filters { get; set; }
    /// <summary>daily | weekly | monthly.</summary>
    public string Frequency { get; set; } = "weekly";
    /// <summary>Weekly: 0 Sunday … 6 Saturday.</summary>
    public int? DayOfWeek { get; set; }
    /// <summary>Monthly: 1–28, or 0 for the last day of the month.</summary>
    public int? DayOfMonth { get; set; }
    /// <summary>"HH:mm" IST, on the half hour.</summary>
    public string Time { get; set; } = "09:00";
    public List<string> Recipients { get; set; } = [];
    /// <summary>xlsx | pdf.</summary>
    public string Format { get; set; } = "xlsx";
    public bool OnlyIfData { get; set; }
    public bool? IsActive { get; set; }
}

public record ReportScheduleDto(
    long ScheduleId,
    string ReportKey,
    string ReportName,
    string Frequency,
    int? DayOfWeek,
    int? DayOfMonth,
    string Time,
    string HowOften,
    List<string> Recipients,
    string Format,
    bool OnlyIfData,
    bool IsActive,
    DateTime? NextRunUtc,
    string? NextRunText,
    DateTime? LastRunUtc,
    string? LastRunStatus,
    string? LastError,
    string? DisabledReason,
    long CreatedByUserId,
    string CreatedByName,
    bool CanEdit,
    Dictionary<string, List<string>> Filters);

// ── Services ───────────────────────────────────────────────────────────────

public interface IReportCatalogService
{
    Task<ReportCatalogueDto> GetCatalogueAsync(CancellationToken ct = default);
    Task<List<string>> GetFavouritesAsync(CancellationToken ct = default);
    Task AddFavouriteAsync(string reportKey, CancellationToken ct = default);
    Task RemoveFavouriteAsync(string reportKey, CancellationToken ct = default);
    Task SavePreferencesAsync(string reportKey, ReportPreferenceRequest request, CancellationToken ct = default);
    Task<List<ReportCustomerOption>> SearchCustomersAsync(string? query, CancellationToken ct = default);
}

public interface IReportRunService
{
    Task<ReportRunResult> RunAsync(string reportKey, ReportRunRequest request, CancellationToken ct = default);
    Task<ReportExportOutcome> ExportAsync(string reportKey, ReportExportRequest request, string? portalBaseUrl,
        CancellationToken ct = default);
    Task<ReportDownload> DownloadAsync(string token, CancellationToken ct = default);
}

public interface IReportOverviewService
{
    Task<OverviewDto> GetAsync(OverviewRequest request, CancellationToken ct = default);
    Task<ReportDownload> ExportPdfAsync(OverviewRequest request, CancellationToken ct = default);
}

public interface IReportScheduleService
{
    Task<List<ReportScheduleDto>> ListAsync(CancellationToken ct = default);
    Task<ReportScheduleDto> CreateAsync(ReportScheduleRequest request, CancellationToken ct = default);
    Task<ReportScheduleDto> UpdateAsync(long scheduleId, ReportScheduleRequest request, CancellationToken ct = default);
    Task StopAsync(long scheduleId, CancellationToken ct = default);
}

/// <summary>The background side: due schedules, queued exports, the nightly rollup.</summary>
public interface IReportBackgroundService
{
    Task ProcessDueSchedulesAsync(CancellationToken ct = default);
    Task ProcessExportJobsAsync(CancellationToken ct = default);
}

public interface IDailyMetricsService
{
    /// <summary>Totals each IST day in the range into RPT_DailyMetric, replacing what
    /// was there. Re-runnable for any past date without leaving duplicates.</summary>
    Task<int> RebuildAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    /// <summary>Nightly: rebuild the last two completed days, and fill any gap.</summary>
    Task RunNightlyAsync(CancellationToken ct = default);
}
