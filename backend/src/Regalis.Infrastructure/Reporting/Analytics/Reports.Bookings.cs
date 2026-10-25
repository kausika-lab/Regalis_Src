using Microsoft.EntityFrameworkCore;
using Regalis.Application.Reporting.Analytics;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Enums;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Reporting.Analytics;

internal static class ReportText
{
    public static string Plural(decimal count, string noun) =>
        $"{ReportMath.Count(count)} {(count == 1 ? Singular(noun) : noun)}";

    private static string Singular(string noun) =>
        noun.EndsWith("ies") ? noun[..^3] + "y"
        : noun.EndsWith("sses") ? noun[..^2]
        : noun.EndsWith('s') ? noun[..^1] : noun;

    public static decimal Num(Dictionary<string, object?> totals, string key) =>
        totals.GetValueOrDefault(key) switch
        {
            decimal d => d,
            int i => i,
            long l => l,
            _ => 0m
        };
}

/// <summary>The booking report's filters, applied in SQL and shared by every report
/// that lists bookings.</summary>
internal static class BookingFilters
{
    public static List<ReportOption> StatusOptions => Filters.Options(
        ("confirmed", "Confirmed"), ("completed", "Completed"), ("cancelled", "Cancelled"),
        ("noshow", "No-show"), ("pending", "Awaiting payment"));

    public static IQueryable<Booking> Apply(ReportRunContext ctx, bool status = true)
    {
        var query = ctx.Db.Bookings.AsNoTracking()
            .Where(b => b.BookingDate >= ctx.From && b.BookingDate <= ctx.To);
        return Scope(ctx, query, status);
    }

    /// <summary>Everything except the date window.</summary>
    public static IQueryable<Booking> Scope(ReportRunContext ctx, IQueryable<Booking> query, bool status = true)
    {
        if (ctx.LocationId is { } location)
        {
            query = query.Where(b => b.LocationId == location);
        }
        var types = ctx.Ints("types");
        if (types.Count > 0)
        {
            query = query.Where(b => types.Contains(b.WorkspaceTypeId));
        }
        if (ctx.CustomerId is { } customer)
        {
            query = query.Where(b => b.CustomerId == customer);
        }
        if (!ctx.Flag("internal"))
        {
            query = BookingRules.ExcludeInternal(query);
        }
        if (status)
        {
            var statuses = ctx.Multi("status").SelectMany(BookingRules.StatusesFor).Distinct().ToList();
            if (statuses.Count > 0)
            {
                query = query.Where(b => statuses.Contains(b.Status));
            }
        }
        return query;
    }
}

internal sealed class BookingRow
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public long CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public string? Company { get; set; }
    public string Workspace { get; set; } = "";
    public int WorkspaceTypeId { get; set; }
    public string Type { get; set; } = "";
    public string Location { get; set; } = "";
    public DateOnly BookingDate { get; set; }
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public decimal? Hours { get; set; }
    public decimal Amount { get; set; }
    public string Status { get; set; } = "";
    public string Payment { get; set; } = "";
    public string Source { get; set; } = "";
    public DateTime Booked { get; set; }
}

/// <summary>Every booking with customer, workspace, type, date and time, hours, amount
/// and status (spec §4 — the worked example).</summary>
internal sealed class BookingReport : ReportHandler<BookingRow>
{
    public override string Key => "booking";
    public override string Name => "Booking report";
    public override string Description => "Every booking with customer, workspace, type, date and time, hours, amount and status.";
    public override string Group => "bookings";
    public override string[] Keywords => ["bookings", "reservations", "desk", "cabin", "meeting", "hours"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "bookings";
    public override string DefaultSort => "start:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Multi("types", "Booking type", lookups.WorkspaceTypes, allLabel: "All types"),
        // Confirmed + completed by default: a report of bookings never silently
        // includes the cancelled ones (spec §4.2).
        Analytics.Filters.Multi("status", "Status", BookingFilters.StatusOptions, ["confirmed", "completed"]),
        Analytics.Filters.Customer(),
        Analytics.Filters.Toggle("internal", "Include internal bookings")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(BookingRow.Number), "Booking", "text", Searchable: true),
        new(nameof(BookingRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(BookingRow.Company), "Company", "text", Visible: false, Searchable: true),
        new(nameof(BookingRow.Workspace), "Workspace", "text", Searchable: true),
        new(nameof(BookingRow.Type), "Type", "text"),
        new(nameof(BookingRow.Location), "Location", "text", Visible: false),
        new(nameof(BookingRow.Start), "Date & time", "datetime"),
        new(nameof(BookingRow.End), "Ends", "datetime", Visible: false),
        new(nameof(BookingRow.Hours), "Hours", "hours", Total.Sum),
        new(nameof(BookingRow.Amount), "Amount", "money", Total.Sum),
        new(nameof(BookingRow.Status), "Status", "status"),
        new(nameof(BookingRow.Payment), "Payment", "text", Visible: false),
        new(nameof(BookingRow.Source), "Source", "text", Visible: false),
        new(nameof(BookingRow.Booked), "Booked on", "datetime", Visible: false)
    ];

    protected override string? LinkFor(BookingRow row) => $"/admin/customers/{row.CustomerId}";

    internal static IQueryable<BookingRow> Project(IQueryable<Booking> bookings) =>
        bookings.Select(b => new BookingRow
        {
            Id = b.BookingId,
            Number = b.BookingNumber,
            CustomerId = b.CustomerId,
            Customer = b.Customer.FullName,
            Company = b.Customer.CompanyName,
            Workspace = b.Workspace.WorkspaceName,
            WorkspaceTypeId = b.WorkspaceTypeId,
            Type = b.WorkspaceType.TypeName,
            Location = b.Location.LocationName,
            BookingDate = b.BookingDate,
            Start = b.StartUtc,
            End = b.EndUtc,
            // Monthly and open-ended bookings contribute no hours (spec §4.3).
            Hours = b.BillingMonths != null ? null : (decimal?)b.DurationMinutes / 60m,
            Amount = b.TotalAmount,
            Status = b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn ? "Confirmed"
                : b.Status == BookingStatus.Completed ? "Completed"
                : b.Status == BookingStatus.Cancelled ? "Cancelled"
                : b.Status == BookingStatus.NoShow ? "No-show"
                : b.Status == BookingStatus.Pending ? "Awaiting payment"
                : "Expired",
            Payment = b.PaymentStatus == BookingPaymentStatus.Paid ? "Paid"
                : b.PaymentStatus == BookingPaymentStatus.Refunded ? "Refunded"
                : b.PaymentStatus == BookingPaymentStatus.PartiallyRefunded ? "Part refunded"
                : b.PaymentStatus == BookingPaymentStatus.Failed ? "Failed"
                : "Unpaid",
            Source = b.BookingSource == BookingSource.Admin ? "Admin" : b.BookingSource == BookingSource.Pass ? "Pass" : "Customer",
            Booked = b.CreatedDate
        });

    protected override Task<IQueryable<BookingRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct) =>
        Task.FromResult(Project(BookingFilters.Apply(ctx)));

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<BookingRow> rows,
        CancellationToken ct)
    {
        var count = await CountAsync(rows, ct);
        var value = await SumAsync(rows, r => r.Amount, ct);
        var hours = await SumAsync(rows, r => r.Hours, ct);

        // Cancellation rate: cancelled ÷ bookings MADE in the period — the denominator
        // is bookings made, not bookings starting (spec §9.3). The status filter does
        // not apply, or the default (confirmed + completed) would always give zero.
        var made = BookingFilters.Scope(ctx, ctx.Db.Bookings.AsNoTracking()
            .Where(b => b.CreatedDate >= ctx.FromUtc && b.CreatedDate < ctx.ToUtc), status: false);
        var madeCount = await made.CountAsync(ct);
        var cancelled = await made.CountAsync(b => b.Status == BookingStatus.Cancelled, ct);

        return
        [
            new("bookings", "Bookings", count, "count"),
            new("value", "Booking value", value, "money",
                Sub: count == 0 ? null : $"Average {ReportMath.Money(value / count)} per booking"),
            new("hours", "Hours booked", hours, "hours", Sub: "Monthly bookings add no hours"),
            new("cancelled", "Cancelled", cancelled, "count", UpIsGood: false,
                Sub: madeCount == 0 ? "No bookings made in this period"
                    : $"{ReportMath.Percent(cancelled * 100m / madeCount, 1)} of {ReportText.Plural(madeCount, "bookings")} made")
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<BookingRow> rows,
        CancellationToken ct)
    {
        var byDay = await rows.GroupBy(r => r.BookingDate)
            .Select(g => new { Day = g.Key, Count = g.Count(), Value = g.Sum(r => r.Amount) })
            .ToListAsync(ct);
        var points = ChartDtoBuilder.DailyPoints(ctx.From, ctx.To,
            byDay.ToDictionary(d => d.Day, d => ((decimal)d.Count, d.Value)));
        var busiest = ChartDtoBuilder.Highest(points);

        var byType = await rows.GroupBy(r => r.WorkspaceTypeId)
            .Select(g => new { TypeId = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var typeSlices = ChartDtoBuilder.Slices(byType
            .Select(t => (Name: ctx.Categories.TypeName(t.TypeId), Count: (decimal)t.Count))
            .OrderBy(t => ctx.Categories.Ordered.IndexOf(t.Name))
            .Select(t => (t.Name, t.Count, ctx.Categories.SlotOf(t.Name))));

        var ids = rows.Select(r => (long?)r.Id);
        var paid = await ctx.Db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Success && ids.Contains(p.BookingId))
            .GroupBy(p => p.PaymentMethod)
            .Select(g => new { Method = g.Key, Amount = g.Sum(p => p.Amount) })
            .ToListAsync(ct);
        string[] modes = ["Online", "Wallet", "Desk / invoice"];
        var paidSlices = ChartDtoBuilder.Slices(modes.Select((mode, i) => (mode,
            paid.Where(p => BookingRules.PaymentModeGroup(p.Method) == mode).Sum(p => p.Amount), i + 1)));

        var charts = new List<ChartDto>
        {
            new()
            {
                Id = "per-day",
                Kind = "columns",
                Title = "Bookings each day",
                Subtitle = busiest is { } b
                    ? $"Busiest: {points[b].Label}, {ReportText.Plural(points[b].Value ?? 0, "bookings")}"
                    : "No bookings in this range",
                ValueFormat = "count",
                Value2Format = "money",
                Value2Label = "value",
                Points = points,
                HighlightIndex = busiest
            },
            new()
            {
                Id = "by-type",
                Kind = typeSlices.Count <= 5 ? "doughnut" : "hbars",
                Title = "What was booked",
                ValueFormat = "count",
                Slices = typeSlices,
                Total = typeSlices.Sum(s => s.Value)
            }
        };
        if (paidSlices.Any(s => s.Value > 0))
        {
            charts.Add(new ChartDto
            {
                Id = "paid",
                Kind = "stackedbar",
                Title = "How it was paid",
                ValueFormat = "money",
                Slices = paidSlices,
                Total = paidSlices.Sum(s => s.Value)
            });
        }
        return charts;
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Every booking between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "bookings")}, worth {ReportMath.Money(ReportText.Num(totals, "amount"))}.";
}

internal sealed class CancellationRow
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public long CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public string Workspace { get; set; } = "";
    public int WorkspaceTypeId { get; set; }
    public string Type { get; set; } = "";
    public DateOnly BookingDate { get; set; }
    public DateTime Start { get; set; }
    public string Status { get; set; } = "";
    public DateTime? CancelledOn { get; set; }
    public decimal? NoticeHours { get; set; }
    public string? CancelledBy { get; set; }
    public string? Reason { get; set; }
    public decimal Amount { get; set; }
    public decimal Refund { get; set; }
}

internal sealed class CancellationsReport : ReportHandler<CancellationRow>
{
    public override string Key => "cancellations";
    public override string Name => "Cancellations & no-shows";
    public override string Description => "Cancelled and no-show bookings with the reason, who cancelled, how much notice was given, and the refund if any.";
    public override string Group => "bookings";
    public override string[] Keywords => ["cancelled", "cancellation", "no-show", "refund", "notice"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "bookings";
    public override string DefaultSort => "start:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Multi("types", "Booking type", lookups.WorkspaceTypes, allLabel: "All types"),
        Analytics.Filters.Multi("status", "Status",
            Analytics.Filters.Options(("cancelled", "Cancelled"), ("noshow", "No-show")), allLabel: "Both"),
        Analytics.Filters.Customer()
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(CancellationRow.Number), "Booking", "text", Searchable: true),
        new(nameof(CancellationRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(CancellationRow.Workspace), "Workspace", "text", Searchable: true),
        new(nameof(CancellationRow.Type), "Type", "text", Visible: false),
        new(nameof(CancellationRow.Start), "Booked for", "datetime"),
        new(nameof(CancellationRow.Status), "Status", "status"),
        new(nameof(CancellationRow.CancelledOn), "Cancelled on", "datetime"),
        new(nameof(CancellationRow.NoticeHours), "Notice", "hours"),
        new(nameof(CancellationRow.CancelledBy), "Cancelled by", "text"),
        new(nameof(CancellationRow.Reason), "Reason", "text", Searchable: true),
        new(nameof(CancellationRow.Amount), "Amount", "money", Total.Sum),
        new(nameof(CancellationRow.Refund), "Refund", "money", Total.Sum)
    ];

    protected override string? LinkFor(CancellationRow row) => $"/admin/customers/{row.CustomerId}";

    protected override Task<IQueryable<CancellationRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var bookings = BookingFilters.Apply(ctx, status: false);
        var wanted = ctx.Multi("status").SelectMany(BookingRules.StatusesFor).ToList();
        if (wanted.Count == 0)
        {
            wanted = [BookingStatus.Cancelled, BookingStatus.NoShow];
        }
        bookings = bookings.Where(b => wanted.Contains(b.Status));
        var db = ctx.Db;
        return Task.FromResult(
            from b in bookings
            from canceller in db.Users.Where(u => u.UserId == b.CancelledByUserId).DefaultIfEmpty()
            select new CancellationRow
            {
                Id = b.BookingId,
                Number = b.BookingNumber,
                CustomerId = b.CustomerId,
                Customer = b.Customer.FullName,
                Workspace = b.Workspace.WorkspaceName,
                WorkspaceTypeId = b.WorkspaceTypeId,
                Type = b.WorkspaceType.TypeName,
                BookingDate = b.BookingDate,
                Start = b.StartUtc,
                Status = b.Status == BookingStatus.NoShow ? "No-show" : "Cancelled",
                CancelledOn = b.CancelledUtc,
                NoticeHours = b.CancelledUtc == null ? null
                    : (decimal?)EF.Functions.DateDiffMinute(b.CancelledUtc.Value, b.StartUtc) / 60m,
                CancelledBy = canceller == null ? null
                    : canceller.UserId == b.CustomerId ? "Customer" : canceller.FullName,
                Reason = b.CancellationReason,
                Amount = b.TotalAmount,
                Refund = b.RefundAmount ?? 0m
            });
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<CancellationRow> rows,
        CancellationToken ct)
    {
        var cancelled = await rows.CountAsync(r => r.Status == "Cancelled", ct);
        var noShows = await rows.CountAsync(r => r.Status == "No-show", ct);
        var refunded = await SumAsync(rows, r => r.Refund, ct);
        var notice = await rows.Where(r => r.NoticeHours != null).AverageAsync(r => r.NoticeHours, ct);
        return
        [
            new("cancelled", "Cancelled", cancelled, "count", UpIsGood: false),
            new("noshows", "No-shows", noShows, "count", UpIsGood: false),
            new("refunded", "Refunded", refunded, "money", UpIsGood: false, Sub: "Paid back to wallets"),
            new("notice", "Average notice", notice, "hours", Compare: false,
                Sub: notice is null ? null : $"About {ReportMath.Count(Math.Max(0, notice.Value) / 24m)} days ahead")
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<CancellationRow> rows,
        CancellationToken ct)
    {
        var byDay = await rows.GroupBy(r => r.BookingDate)
            .Select(g => new { Day = g.Key, Count = g.Count(), Value = g.Sum(r => r.Amount) })
            .ToListAsync(ct);
        var points = ChartDtoBuilder.DailyPoints(ctx.From, ctx.To,
            byDay.ToDictionary(d => d.Day, d => ((decimal)d.Count, d.Value)));
        var bands = await rows.GroupBy(r =>
                r.Status == "No-show" ? 5
                : r.NoticeHours == null || r.NoticeHours < 24 ? 1
                : r.NoticeHours < 72 ? 2
                : r.NoticeHours < 168 ? 3 : 4)
            .Select(g => new { Band = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        string[] labels = ["Under 24 hours", "1–3 days", "3–7 days", "Over a week", "No-show"];
        var slices = ChartDtoBuilder.Slices(labels.Select((label, i) =>
            (label, (decimal)(bands.FirstOrDefault(b => b.Band == i + 1)?.Count ?? 0), i + 1)));
        return
        [
            new ChartDto
            {
                Id = "per-day", Kind = "columns", Title = "Cancellations each day", ValueFormat = "count",
                Value2Format = "money", Value2Label = "value", Points = points,
                HighlightIndex = ChartDtoBuilder.Highest(points)
            },
            new ChartDto
            {
                Id = "notice", Kind = "doughnut", Title = "Notice given", ValueFormat = "count",
                Slices = slices, Total = slices.Sum(s => s.Value)
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Cancelled and no-show bookings between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "bookings")}, {ReportMath.Money(ReportText.Num(totals, "refund"))} refunded.";
}

internal sealed class MeetingRoomRow
{
    public long Id { get; set; }
    public string Room { get; set; } = "";
    public DateOnly Date { get; set; }
    public int Bookings { get; set; }
    public decimal HoursBooked { get; set; }
    public decimal HoursAvailable { get; set; }
    public decimal? ShareUsed { get; set; }
}

internal sealed class MeetingRoomUsageReport : ReportHandler<MeetingRoomRow>
{
    public override string Key => "meeting-room-usage";
    public override string Name => "Meeting room usage";
    public override string Description => "Hours booked per room, per day and per hour, with the share of available hours used.";
    public override string Group => "bookings";
    public override string[] Keywords => ["meeting", "room", "board room", "usage", "utilisation", "hours"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "room-days";
    public override string DefaultSort => "date:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Select("room", "Room", lookups.MeetingRooms, allLabel: "All rooms")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(MeetingRoomRow.Room), "Room", "text", Searchable: true),
        new(nameof(MeetingRoomRow.Date), "Date", "date"),
        new(nameof(MeetingRoomRow.Bookings), "Bookings", "number", Total.Sum),
        new(nameof(MeetingRoomRow.HoursBooked), "Hours booked", "hours", Total.Sum),
        new(nameof(MeetingRoomRow.HoursAvailable), "Hours available", "hours", Total.Sum),
        new(nameof(MeetingRoomRow.ShareUsed), "Share used", "percent", Total.Custom)
    ];

    private static async Task<(OccupancyResult Occupancy, List<MeetingRoomRow> Rows)> BuildAsync(
        ReportRunContext ctx, CancellationToken ct)
    {
        var typeIds = await ctx.Db.WorkspaceTypes.AsNoTracking()
            .Where(t => t.TypeCode == "MEETING_ROOM").Select(t => t.WorkspaceTypeId).ToListAsync(ct);
        var occupancy = await OccupancyCalculator.ComputeAsync(ctx.Db, ctx.From, ctx.To, ctx.LocationId,
            typeIds.Count == 0 ? [-1] : typeIds, ctx.Int("room"), includeInternal: false, ct);
        var roomIds = occupancy.Spaces.Select(s => s.WorkspaceId).ToList();
        var counts = await BookingRules.ExcludeInternal(ctx.Db.Bookings.AsNoTracking())
            .Where(b => roomIds.Contains(b.WorkspaceId) && BookingRules.Kept.Contains(b.Status) &&
                        b.BookingDate >= ctx.From && b.BookingDate <= ctx.To)
            .GroupBy(b => new { b.WorkspaceId, b.BookingDate })
            .Select(g => new { g.Key.WorkspaceId, g.Key.BookingDate, Count = g.Count() })
            .ToListAsync(ct);
        var names = occupancy.Spaces.ToDictionary(s => s.WorkspaceId, s => s.Name);
        var rows = occupancy.Days.Select((d, i) => new MeetingRoomRow
        {
            Id = i + 1,
            Room = names[d.WorkspaceId],
            Date = d.Day,
            Bookings = counts.FirstOrDefault(c => c.WorkspaceId == d.WorkspaceId && c.BookingDate == d.Day)?.Count ?? 0,
            HoursBooked = Math.Round(d.Booked, 2),
            HoursAvailable = Math.Round(d.Available, 2),
            ShareUsed = d.Available == 0 ? null : Math.Round(d.Booked / d.Available * 100m, 1)
        }).ToList();
        return (occupancy, rows);
    }

    protected override async Task<IQueryable<MeetingRoomRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct) =>
        FromList((await BuildAsync(ctx, ct)).Rows);

    protected override Task CustomTotalsAsync(ReportRunContext ctx, IQueryable<MeetingRoomRow> rows,
        Dictionary<string, object?> totals, CancellationToken ct)
    {
        var available = ReportText.Num(totals, "hoursAvailable");
        totals["shareUsed"] = available == 0 ? null : Math.Round(ReportText.Num(totals, "hoursBooked") / available * 100m, 1);
        return Task.CompletedTask;
    }

    protected override Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<MeetingRoomRow> rows,
        CancellationToken ct)
    {
        var booked = rows.Sum(r => r.HoursBooked);
        var available = rows.Sum(r => r.HoursAvailable);
        var busiest = rows.GroupBy(r => r.Room)
            .Select(g => new { Room = g.Key, Hours = g.Sum(r => r.HoursBooked) })
            .OrderByDescending(g => g.Hours).FirstOrDefault();
        return Task.FromResult(new List<Figure>
        {
            new("hours", "Hours booked", booked, "hours"),
            new("share", "Share used", available == 0 ? null : Math.Round(booked / available * 100m, 1), "percent",
                Sub: busiest is { Hours: > 0 } ? $"Busiest: {busiest.Room}" : null),
            new("bookings", "Bookings", rows.Sum(r => r.Bookings), "count"),
            new("available", "Hours available", available, "hours", Compare: false, Sub: "Opening hours, less blocked time")
        });
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<MeetingRoomRow> rows,
        CancellationToken ct)
    {
        var (occupancy, _) = await BuildAsync(ctx, ct);
        var heat = OccupancyCalculator.Heatmap(occupancy);
        return
        [
            ChartDtoBuilder.Heatmap("heatmap", "When the rooms are busy",
                "Share of room-hours booked, by weekday and hour", heat) with
            {
                Note = ChartDtoBuilder.QuietestSentence(heat)
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Meeting room use between {ReportMath.Range(ctx.From, ctx.To)} — {ReportMath.Hours(ReportText.Num(totals, "hoursBooked"))} booked of {ReportMath.Hours(ReportText.Num(totals, "hoursAvailable"))} open.";
}

internal sealed class DayPassRow
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public long CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public int PlanId { get; set; }
    public string Plan { get; set; } = "";
    public DateTime Sold { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly ValidTo { get; set; }
    public int Guests { get; set; }
    public decimal Price { get; set; }
    public decimal Gst { get; set; }
    public decimal Total { get; set; }
    public string Mode { get; set; } = "";
    public string Channel { get; set; } = "";
    public string Status { get; set; } = "";
}

internal sealed class DayPassSalesReport : ReportHandler<DayPassRow>
{
    public override string Key => "day-pass-sales";
    public override string Name => "Day pass sales";
    public override string Description => "Day passes sold, including walk-ins, with the price charged and the payment mode.";
    public override string Group => "bookings";
    public override string[] Keywords => ["day pass", "passes", "walk-in", "sales"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "passes";
    public override string DefaultSort => "sold:desc";

    private static List<ReportOption> Modes => Analytics.Filters.Options(
        ("online", "Online"), ("wallet", "Wallet"), ("desk", "Desk / invoice"));

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Select("plan", "Plan", lookups.DayPassPlans, allLabel: "All plans"),
        Analytics.Filters.Multi("mode", "Payment mode", Modes, allLabel: "All modes"),
        Analytics.Filters.Select("channel", "Sold", Analytics.Filters.Options(("walkin", "Walk-in at the desk"),
            ("online", "Online")), allLabel: "Walk-in and online"),
        Analytics.Filters.Customer()
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(DayPassRow.Number), "Pass", "text", Searchable: true),
        new(nameof(DayPassRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(DayPassRow.Plan), "Plan", "text"),
        new(nameof(DayPassRow.Sold), "Sold on", "datetime"),
        new(nameof(DayPassRow.ValidFrom), "Valid from", "date", Visible: false),
        new(nameof(DayPassRow.ValidTo), "Valid to", "date"),
        new(nameof(DayPassRow.Guests), "Guests", "number", Total.Sum, Visible: false),
        new(nameof(DayPassRow.Price), "Price", "money", Total.Sum),
        new(nameof(DayPassRow.Gst), "GST", "money", Total.Sum),
        new(nameof(DayPassRow.Total), "Total", "money", Total.Sum),
        new(nameof(DayPassRow.Mode), "Payment mode", "text"),
        new(nameof(DayPassRow.Channel), "Sold", "text"),
        new(nameof(DayPassRow.Status), "Status", "status")
    ];

    protected override string? LinkFor(DayPassRow row) => $"/admin/customers/{row.CustomerId}";

    protected override Task<IQueryable<DayPassRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var passes = db.CustomerPasses.AsNoTracking()
            .Where(p => p.PassType == PassType.DayPass && p.PurchaseUtc >= ctx.FromUtc && p.PurchaseUtc < ctx.ToUtc);
        if (ctx.LocationId is { } location)
        {
            passes = passes.Where(p => p.LocationId == location);
        }
        if (ctx.Int("plan") is { } plan)
        {
            passes = passes.Where(p => p.PassPlanId == plan);
        }
        if (ctx.CustomerId is { } customer)
        {
            passes = passes.Where(p => p.CustomerId == customer);
        }
        var rows =
            from p in passes
            from u in db.Users.Where(u => u.UserId == p.CustomerId)
            from pay in db.Payments.Where(x => x.PaymentId == p.PaymentId).DefaultIfEmpty()
            select new DayPassRow
            {
                Id = p.CustomerPassId,
                Number = p.PassNumber,
                CustomerId = p.CustomerId,
                Customer = u.FullName,
                PlanId = p.PassPlanId,
                Plan = p.PassPlan.PlanName,
                Sold = p.PurchaseUtc,
                ValidFrom = p.ValidFrom,
                ValidTo = p.ValidTo,
                Guests = p.GuestCount,
                Price = p.Price,
                Gst = p.TaxAmount,
                Total = p.TotalAmount,
                Mode = pay == null ? "Not paid"
                    : pay.PaymentMethod == PaymentMethod.Wallet ? "Wallet"
                    : pay.PaymentMethod == PaymentMethod.Cash || pay.PaymentMethod == PaymentMethod.Other ? "Desk / invoice"
                    : "Online",
                // Sold by staff at the desk = a walk-in; bought by the customer = online.
                Channel = p.CreatedBy == p.CustomerId ? "Online" : "Walk-in",
                Status = p.Status == CustomerPassStatus.Active ? "Active"
                    : p.Status == CustomerPassStatus.Used ? "Used"
                    : p.Status == CustomerPassStatus.Expired ? "Expired" : "Cancelled"
            };
        var modes = ctx.Multi("mode").Select(m => m switch
        {
            "online" => "Online",
            "wallet" => "Wallet",
            "desk" => "Desk / invoice",
            _ => ""
        }).ToList();
        if (modes.Count > 0)
        {
            rows = rows.Where(r => modes.Contains(r.Mode));
        }
        if (ctx.One("channel") is { } channel)
        {
            var label = channel == "walkin" ? "Walk-in" : "Online";
            rows = rows.Where(r => r.Channel == label);
        }
        return Task.FromResult(rows);
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<DayPassRow> rows,
        CancellationToken ct)
    {
        var count = await CountAsync(rows, ct);
        var value = await SumAsync(rows, r => r.Total, ct);
        var walkIns = await rows.CountAsync(r => r.Channel == "Walk-in", ct);
        return
        [
            new("sold", "Passes sold", count, "count"),
            new("value", "Value", value, "money", Sub: "Including GST"),
            new("walkins", "Walk-ins", walkIns, "count",
                Sub: count == 0 ? null : $"{ReportMath.Percent(walkIns * 100m / count)} of passes sold"),
            new("average", "Average price", count == 0 ? null : value / count, "money")
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<DayPassRow> rows,
        CancellationToken ct)
    {
        var sold = await rows.Select(r => new { r.Sold, r.Total, r.Mode }).ToListAsync(ct);
        var byDay = sold.GroupBy(r => Shared.IstClock.IstDateOf(r.Sold))
            .ToDictionary(g => g.Key, g => ((decimal)g.Count(), g.Sum(r => r.Total)));
        var points = ChartDtoBuilder.DailyPoints(ctx.From, ctx.To, byDay);
        string[] modes = ["Online", "Wallet", "Desk / invoice"];
        var slices = ChartDtoBuilder.Slices(modes.Select((m, i) =>
            (m, sold.Where(s => s.Mode == m).Sum(s => s.Total), i + 1)));
        var charts = new List<ChartDto>
        {
            new()
            {
                Id = "per-day", Kind = "columns", Title = "Passes sold each day", ValueFormat = "count",
                Value2Format = "money", Value2Label = "value", Points = points,
                HighlightIndex = ChartDtoBuilder.Highest(points)
            }
        };
        if (slices.Any(s => s.Value > 0))
        {
            charts.Add(new ChartDto
            {
                Id = "paid", Kind = "stackedbar", Title = "How it was paid", ValueFormat = "money",
                Slices = slices, Total = slices.Sum(s => s.Value)
            });
        }
        return charts;
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Day passes sold between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "passes")}, worth {ReportMath.Money(ReportText.Num(totals, "total"))}.";
}

internal sealed class EventRow
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public long CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public string Space { get; set; } = "";
    public DateTime Start { get; set; }
    public decimal? Hours { get; set; }
    public int Capacity { get; set; }
    public decimal Amount { get; set; }
    public decimal Invoiced { get; set; }
    public string Status { get; set; } = "";
}

/// <summary>Bookings of an event space. Regalis has no event-space type and records no
/// add-ons or headcount yet, so this lists what exists — space capacity stands in for
/// headcount — and is empty until an event space is added.</summary>
internal sealed class EventBookingsReport : ReportHandler<EventRow>
{
    public override string Key => "event-bookings";
    public override string Name => "Event bookings";
    public override string Description => "Event space bookings with the space's capacity and the final invoiced amount.";
    public override string Group => "bookings";
    public override string[] Keywords => ["event", "events", "hall", "invoiced"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "events";
    public override string DefaultSort => "start:desc";
    public override string? Hint => "Event spaces only";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Multi("status", "Status", BookingFilters.StatusOptions, ["confirmed", "completed"]),
        Analytics.Filters.Customer()
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(EventRow.Number), "Booking", "text", Searchable: true),
        new(nameof(EventRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(EventRow.Space), "Space", "text"),
        new(nameof(EventRow.Start), "Date & time", "datetime"),
        new(nameof(EventRow.Hours), "Hours", "hours", Total.Sum),
        new(nameof(EventRow.Capacity), "Capacity", "number"),
        new(nameof(EventRow.Amount), "Amount", "money", Total.Sum),
        new(nameof(EventRow.Invoiced), "Invoiced", "money", Total.Custom),
        new(nameof(EventRow.Status), "Status", "status")
    ];

    protected override string? LinkFor(EventRow row) => $"/admin/customers/{row.CustomerId}";

    private static IQueryable<Booking> Events(ReportRunContext ctx) =>
        BookingFilters.Apply(ctx).Where(b => b.WorkspaceType.TypeCode.Contains("EVENT") ||
                                             b.WorkspaceType.TypeName.Contains("Event"));

    protected override Task<IQueryable<EventRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        return Task.FromResult(Events(ctx).Select(b => new EventRow
        {
            Id = b.BookingId,
            Number = b.BookingNumber,
            CustomerId = b.CustomerId,
            Customer = b.Customer.FullName,
            Space = b.Workspace.WorkspaceName,
            Start = b.StartUtc,
            Hours = b.BillingMonths != null ? null : (decimal?)b.DurationMinutes / 60m,
            Capacity = b.Workspace.Capacity,
            Amount = b.TotalAmount,
            Invoiced = db.Invoices.Where(i => i.BookingId == b.BookingId && i.Status != InvoiceStatus.Cancelled)
                .Sum(i => (decimal?)i.TotalAmount) ?? 0m,
            Status = b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn ? "Confirmed"
                : b.Status == BookingStatus.Completed ? "Completed"
                : b.Status == BookingStatus.Cancelled ? "Cancelled"
                : b.Status == BookingStatus.NoShow ? "No-show" : "Awaiting payment"
        }));
    }

    /// <summary>SQL Server cannot SUM a column that is itself a subquery, so the
    /// invoiced total is its own aggregate over the same bookings.</summary>
    protected override async Task CustomTotalsAsync(ReportRunContext ctx, IQueryable<EventRow> rows,
        Dictionary<string, object?> totals, CancellationToken ct)
    {
        var ids = rows.Select(r => (long?)r.Id);
        totals["invoiced"] = await ctx.Db.Invoices.AsNoTracking()
            .Where(i => ids.Contains(i.BookingId) && i.Status != InvoiceStatus.Cancelled)
            .SumAsync(i => (decimal?)i.TotalAmount, ct) ?? 0m;
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<EventRow> rows,
        CancellationToken ct)
    {
        var totals = new Dictionary<string, object?>();
        await CustomTotalsAsync(ctx, rows, totals, ct);
        return
        [
            new("events", "Events", await CountAsync(rows, ct), "count"),
            new("value", "Booking value", await SumAsync(rows, r => r.Amount, ct), "money"),
            new("invoiced", "Invoiced", ReportText.Num(totals, "invoiced"), "money", Sub: "Labelled invoiced, not revenue")
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Event space bookings between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "events")}, {ReportMath.Money(ReportText.Num(totals, "invoiced"))} invoiced.";
}

internal sealed class SourceRow
{
    public long Id { get; set; }
    public string Source { get; set; } = "";
    public int Bookings { get; set; }
    public decimal? Share { get; set; }
    public decimal Value { get; set; }
    public decimal? Average { get; set; }
}

internal sealed class BookingSourceReport : ReportHandler<SourceRow>
{
    public override string Key => "booking-source";
    public override string Name => "Booking source";
    public override string Description => "Bookings split by where they came from: the customer app or website, the front desk, or a pass.";
    public override string Group => "bookings";
    public override string[] Keywords => ["source", "channel", "app", "website", "admin", "walk-in"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "sources";
    public override string DefaultSort => "bookings:desc";

    private static readonly (BookingSource Source, string Label, int Slot)[] Sources =
    [
        (BookingSource.Customer, "Customer app / website", 1),
        (BookingSource.Admin, "Front desk (admin)", 2),
        (BookingSource.Pass, "Pass holder", 3)
    ];

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Multi("types", "Booking type", lookups.WorkspaceTypes, allLabel: "All types"),
        Analytics.Filters.Multi("status", "Status", BookingFilters.StatusOptions, ["confirmed", "completed"])
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(SourceRow.Source), "Source", "text"),
        new(nameof(SourceRow.Bookings), "Bookings", "number", Total.Sum),
        new(nameof(SourceRow.Share), "Share", "percent", Total.Custom),
        new(nameof(SourceRow.Value), "Booking value", "money", Total.Sum),
        new(nameof(SourceRow.Average), "Average value", "money", Total.Custom)
    ];

    protected override async Task<IQueryable<SourceRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var grouped = await BookingFilters.Apply(ctx).GroupBy(b => b.BookingSource)
            .Select(g => new { Source = g.Key, Count = g.Count(), Value = g.Sum(b => b.TotalAmount) })
            .ToListAsync(ct);
        var shares = ReportMath.SharesTo100(Sources
            .Select(s => (decimal)(grouped.FirstOrDefault(g => g.Source == s.Source)?.Count ?? 0)).ToList());
        return FromList(Sources.Select((s, i) =>
        {
            var g = grouped.FirstOrDefault(x => x.Source == s.Source);
            return new SourceRow
            {
                Id = i + 1,
                Source = s.Label,
                Bookings = g?.Count ?? 0,
                Share = shares[i],
                Value = g?.Value ?? 0,
                Average = g is null || g.Count == 0 ? null : g.Value / g.Count
            };
        }));
    }

    protected override Task CustomTotalsAsync(ReportRunContext ctx, IQueryable<SourceRow> rows,
        Dictionary<string, object?> totals, CancellationToken ct)
    {
        var count = ReportText.Num(totals, "bookings");
        totals["share"] = count == 0 ? 0m : 100m;
        totals["average"] = count == 0 ? null : ReportText.Num(totals, "value") / count;
        return Task.CompletedTask;
    }

    protected override Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<SourceRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        var total = list.Sum(r => r.Bookings);
        decimal? ShareOf(int index) => total == 0 ? null : list[index].Share;
        return Task.FromResult(new List<Figure>
        {
            new("bookings", "Bookings", total, "count"),
            new("customer", "Booked by customers", ShareOf(0), "percent", Compare: false),
            new("desk", "Booked at the desk", ShareOf(1), "percent", Compare: false),
            new("value", "Booking value", list.Sum(r => r.Value), "money")
        });
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<SourceRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        var bookings = await BookingFilters.Apply(ctx)
            .GroupBy(b => new { b.BookingDate, b.BookingSource })
            .Select(g => new { g.Key.BookingDate, g.Key.BookingSource, Count = g.Count() })
            .ToListAsync(ct);
        var (buckets, byWeek) = ChartDtoBuilder.Buckets(ctx.From, ctx.To);
        var stacks = buckets.Select(bucket =>
        {
            var values = Sources.Select(s => (decimal)bookings
                .Where(b => b.BookingSource == s.Source && b.BookingDate >= bucket.Start && b.BookingDate <= bucket.End)
                .Sum(b => b.Count)).ToList();
            return new ChartStackDto(bucket.Label, bucket.Start.ToString("yyyy-MM-dd"), values, values.Sum());
        }).ToList();
        return
        [
            new ChartDto
            {
                Id = "share", Kind = "doughnut", Title = "Where bookings came from", ValueFormat = "count",
                Slices = list.Select((r, i) => new ChartSliceDto(r.Source, r.Bookings, r.Share ?? 0, Sources[i].Slot)).ToList(),
                Total = list.Sum(r => r.Bookings)
            },
            new ChartDto
            {
                Id = "over-time", Kind = "stackedcolumns",
                Title = byWeek ? "Bookings by source, each week" : "Bookings by source, each day",
                ValueFormat = "count",
                Series = Sources.Select(s => new ChartSeriesDto(s.Label, s.Slot)).ToList(),
                Stacks = stacks
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Where the bookings between {ReportMath.Range(ctx.From, ctx.To)} came from — {ReportText.Plural(ReportText.Num(totals, "bookings"), "bookings")}.";
}
