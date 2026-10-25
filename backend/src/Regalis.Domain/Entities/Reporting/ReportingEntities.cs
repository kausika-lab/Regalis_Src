using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Reporting;

/// <summary>
/// One completed IST day, per location, per category, totalled by the nightly rollup
/// (Reports &amp; Analysis spec §9.2). The overview page adds these rows up and calculates
/// only today live; reports never read this table.
///
/// Every built day also carries one marker row (<see cref="DayMarker"/>, location 0),
/// which holds the location-independent new-customer count. A day with no marker has
/// not been built, so a quiet day (no other rows) is never mistaken for a missing one.
/// </summary>
public class DailyMetric
{
    public const string DayMarker = "*";

    public long DailyMetricId { get; set; }
    public DateOnly MetricDate { get; set; }
    /// <summary>0 on the day marker row.</summary>
    public int LocationId { get; set; }
    /// <summary>The revenue/booking category: a workspace type name, "Passes &amp; memberships",
    /// "Services &amp; coffee" or "Other" — or <see cref="DayMarker"/>.</summary>
    public string Category { get; set; } = string.Empty;
    public long RevenuePaise { get; set; }
    public int BookingsCount { get; set; }
    /// <summary>Space-hours booked inside opening hours.</summary>
    public decimal HoursBooked { get; set; }
    /// <summary>Space-hours open for booking, less blocked time.</summary>
    public decimal HoursAvailable { get; set; }
    public int NewCustomers { get; set; }
    public int CancelledCount { get; set; }
    public DateTime BuiltUtc { get; set; }
}

/// <summary>A report an admin starred. Per admin, never shared.</summary>
public class ReportFavourite
{
    public long UserId { get; set; }
    public string ReportKey { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// What an admin last did with a report: the filters, the columns and sort they chose,
/// and how often they open it. ReportKey "_overview" holds the overview's range,
/// location and Compare setting, so the page reopens where they left it.
/// </summary>
public class ReportPreference
{
    public const string OverviewKey = "_overview";

    public long UserId { get; set; }
    public string ReportKey { get; set; } = string.Empty;
    public string? FiltersJson { get; set; }
    public string? ColumnsJson { get; set; }
    public string? SortJson { get; set; }
    public int OpenCount { get; set; }
    public DateTime? LastOpenedUtc { get; set; }
    public int? LastRowCount { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>A report that emails itself (spec §12). Stopping keeps the row.</summary>
public class ReportSchedule
{
    public long ReportScheduleId { get; set; }
    public string ReportKey { get; set; } = string.Empty;
    /// <summary>The filters as they were when saved, without dates: each run covers
    /// the period before it (yesterday / last week / last month).</summary>
    public string FiltersJson { get; set; } = "{}";
    public ReportFrequency Frequency { get; set; }
    /// <summary>Weekly: 0 Sunday … 6 Saturday.</summary>
    public byte? DayOfWeek { get; set; }
    /// <summary>Monthly: 1–28, or 0 for the last day of the month.</summary>
    public byte? DayOfMonth { get; set; }
    public TimeOnly TimeIst { get; set; }
    /// <summary>Addresses separated by commas.</summary>
    public string Recipients { get; set; } = string.Empty;
    public ReportFormat Format { get; set; }
    public bool OnlyIfData { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? ModifiedUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime NextRunUtc { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public ReportRunOutcome? LastRunStatus { get; set; }
    public string? LastError { get; set; }
    /// <summary>Failed attempts of the current run; the retries go out ten minutes apart.</summary>
    public byte Attempt { get; set; }
    public DateTime? RetryAtUtc { get; set; }
    /// <summary>When the run of failures began; a week of failures disables the schedule.</summary>
    public DateTime? FailingSinceUtc { get; set; }
    public string? DisabledReason { get; set; }
}

/// <summary>Every attempt a schedule makes, kept for the record.</summary>
public class ReportScheduleRun
{
    public long ReportScheduleRunId { get; set; }
    public long ReportScheduleId { get; set; }
    public DateTime RunUtc { get; set; }
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public int RowCount { get; set; }
    public string Recipients { get; set; } = string.Empty;
    public ReportRunOutcome Outcome { get; set; }
    public byte Attempt { get; set; }
    public string? Error { get; set; }
}

/// <summary>The export log (spec §11.3). Append only: who took which report, with
/// which filters, how many rows, and when.</summary>
public class ReportExport
{
    public long ReportExportId { get; set; }
    public long UserId { get; set; }
    public string ReportKey { get; set; } = string.Empty;
    public string FiltersJson { get; set; } = "{}";
    public int RowCount { get; set; }
    public ReportFormat Format { get; set; }
    public ReportExportDelivery Delivery { get; set; }
    public DateTime CreatedUtc { get; set; }
}

/// <summary>An export too large to build while the browser waits: built in the
/// background, then emailed as a link that expires.</summary>
public class ReportExportJob
{
    public long ReportExportJobId { get; set; }
    public long UserId { get; set; }
    public string ReportKey { get; set; } = string.Empty;
    public string FiltersJson { get; set; } = "{}";
    public ReportFormat Format { get; set; }
    public ReportExportJobStatus Status { get; set; }
    public int RowCount { get; set; }
    /// <summary>Unguessable id used in the download link.</summary>
    public string Token { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
    /// <summary>The admin portal origin the request came from, for the emailed link.</summary>
    public string? PortalBaseUrl { get; set; }
    public string? Error { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public DateTime? ExpiresUtc { get; set; }
}
