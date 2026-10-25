using Microsoft.EntityFrameworkCore;
using Regalis.Application.Reporting.Analytics;
using Regalis.Domain.Enums;
using Regalis.Shared;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Reporting.Analytics;

internal sealed class SpaceRow
{
    public long Id { get; set; }
    public string Space { get; set; } = "";
    public string Type { get; set; } = "";
    public int Capacity { get; set; }
    public decimal HoursBooked { get; set; }
    public decimal HoursBlocked { get; set; }
    public decimal HoursAvailable { get; set; }
    public decimal? Occupancy { get; set; }
}

/// <summary>Occupancy per bookable space: hours used out of hours available (§9.3).</summary>
internal sealed class DeskOccupancyReport : ReportHandler<SpaceRow>
{
    public override string Key => "desk-occupancy";
    public override string Name => "Desk occupancy";
    public override string Description => "Occupancy per space, per day, as hours used out of hours available.";
    public override string Group => "operations";
    public override string Chip => "occupancy";
    public override string[] Keywords => ["occupancy", "utilisation", "utilization", "busy", "desks", "cabins", "spaces"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "spaces";
    public override string DefaultSort => "occupancy:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Multi("types", "Space type", lookups.WorkspaceTypes, allLabel: "All types"),
        Analytics.Filters.Toggle("internal", "Include internal bookings")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(SpaceRow.Space), "Space", "text", Searchable: true),
        new(nameof(SpaceRow.Type), "Type", "text"),
        new(nameof(SpaceRow.Capacity), "Seats", "number", Visible: false),
        new(nameof(SpaceRow.HoursBooked), "Hours booked", "hours", Total.Sum),
        new(nameof(SpaceRow.HoursBlocked), "Hours blocked", "hours", Total.Sum, Visible: false),
        new(nameof(SpaceRow.HoursAvailable), "Hours available", "hours", Total.Sum),
        new(nameof(SpaceRow.Occupancy), "Occupancy", "percent", Total.Custom)
    ];

    private static Task<OccupancyResult> OccupancyAsync(ReportRunContext ctx, CancellationToken ct) =>
        OccupancyCalculator.ComputeAsync(ctx.Db, ctx.From, ctx.To, ctx.LocationId, ctx.Ints("types"), null,
            ctx.Flag("internal"), ct);

    protected override async Task<IQueryable<SpaceRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var occupancy = await OccupancyAsync(ctx, ct);
        return FromList(occupancy.Spaces.Select(s =>
        {
            var days = occupancy.Days.Where(d => d.WorkspaceId == s.WorkspaceId).ToList();
            var booked = days.Sum(d => d.Booked);
            var available = days.Sum(d => d.Available);
            return new SpaceRow
            {
                Id = s.WorkspaceId,
                Space = $"{s.Code} · {s.Name}",
                Type = s.TypeName,
                Capacity = s.Capacity,
                HoursBooked = Math.Round(booked, 2),
                HoursBlocked = Math.Round(days.Sum(d => d.Blocked), 2),
                HoursAvailable = Math.Round(available, 2),
                Occupancy = available == 0 ? null : Math.Round(booked / available * 100m, 1)
            };
        }));
    }

    protected override Task CustomTotalsAsync(ReportRunContext ctx, IQueryable<SpaceRow> rows,
        Dictionary<string, object?> totals, CancellationToken ct)
    {
        var available = ReportText.Num(totals, "hoursAvailable");
        totals["occupancy"] = available == 0 ? null : Math.Round(ReportText.Num(totals, "hoursBooked") / available * 100m, 1);
        return Task.CompletedTask;
    }

    protected override Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<SpaceRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        var booked = list.Sum(r => r.HoursBooked);
        var available = list.Sum(r => r.HoursAvailable);
        var ranked = list.Where(r => r.Occupancy != null).OrderByDescending(r => r.Occupancy).ToList();
        return Task.FromResult(new List<Figure>
        {
            new("occupancy", "Average occupancy", available == 0 ? null : Math.Round(booked / available * 100m, 1),
                "percent"),
            new("busiest", "Busiest space", ranked.FirstOrDefault()?.Occupancy, "percent", Compare: false,
                Sub: ranked.FirstOrDefault()?.Space),
            new("quietest", "Quietest space", ranked.LastOrDefault()?.Occupancy, "percent", Compare: false,
                Sub: ranked.LastOrDefault()?.Space),
            new("hours", "Hours booked", booked, "hours")
        });
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<SpaceRow> rows,
        CancellationToken ct)
    {
        var occupancy = await OccupancyAsync(ctx, ct);
        var byDay = occupancy.Days.GroupBy(d => d.Day).ToDictionary(g => g.Key, g =>
        {
            var available = g.Sum(d => d.Available);
            return (available == 0 ? 0m : Math.Round(g.Sum(d => d.Booked) / available * 100m, 1), Math.Round(g.Sum(d => d.Booked), 1));
        });
        var points = ChartDtoBuilder.DailyPoints(ctx.From, ctx.To, byDay);
        var list = rows.Where(r => r.Occupancy != null).OrderByDescending(r => r.Occupancy).ToList();
        return
        [
            new ChartDto
            {
                Id = "per-day", Kind = "columns", Title = "Occupancy each day", ValueFormat = "percent",
                Value2Format = "hours", Value2Label = "booked", Points = points,
                HighlightIndex = ChartDtoBuilder.Highest(points)
            },
            new ChartDto
            {
                Id = "spaces", Kind = "hbars", Title = "Occupancy by space",
                Subtitle = "Colour shows the band: under 40%, 40–80%, over 80%",
                ValueFormat = "percent",
                Slices = list.Select(r => new ChartSliceDto(r.Space, r.Occupancy ?? 0, r.Occupancy ?? 0, 1,
                    $"{ReportMath.Hours(r.HoursBooked)} of {ReportMath.Hours(r.HoursAvailable)}",
                    OccupancyCalculator.Band(r.Occupancy ?? 0))).ToList()
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Occupancy between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "spaces")}, {ReportMath.Percent(totals.GetValueOrDefault("occupancy") as decimal?)} booked overall.";
}

internal sealed class HourRow
{
    public long Id { get; set; }
    public int Order { get; set; }
    public string Day { get; set; } = "";
    public string Hour { get; set; } = "";
    public decimal? Occupancy { get; set; }
    public decimal People { get; set; }
}

internal sealed class BusyHoursReport : ReportHandler<HourRow>
{
    public override string Key => "busy-hours";
    public override string Name => "Busy hours heatmap";
    public override string Description => "Weekday against hour, with the average occupancy of each slot.";
    public override string Group => "operations";
    public override string Chip => "occupancy";
    public override string[] Keywords => ["busy", "hours", "heatmap", "peak", "quiet", "when", "weekday"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "slots";
    public override string DefaultSort => "order:asc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Multi("types", "Space type", lookups.WorkspaceTypes, allLabel: "All types")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(HourRow.Order), "Slot", "number", Visible: false),
        new(nameof(HourRow.Day), "Weekday", "text"),
        new(nameof(HourRow.Hour), "Hour", "text"),
        new(nameof(HourRow.Occupancy), "Average occupancy", "percent"),
        new(nameof(HourRow.People), "Average people", "number")
    ];

    private static async Task<ChartDtoBuilder.Heat> HeatAsync(ReportRunContext ctx, CancellationToken ct) =>
        OccupancyCalculator.Heatmap(await OccupancyCalculator.ComputeAsync(ctx.Db, ctx.From, ctx.To, ctx.LocationId,
            ctx.Ints("types"), null, includeInternal: false, ct));

    protected override async Task<IQueryable<HourRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var heat = await HeatAsync(ctx, ct);
        var rows = new List<HourRow>();
        for (var d = 0; d < heat.Weekdays.Count; d++)
        {
            for (var h = 0; h < heat.Hours.Count; h++)
            {
                var (percent, people) = heat.Cells[d][h];
                rows.Add(new HourRow
                {
                    Id = rows.Count + 1,
                    Order = rows.Count + 1,
                    Day = ChartDtoBuilder.FullWeekday(heat.Weekdays[d]),
                    Hour = $"{OccupancyCalculator.HourLabel(heat.Hours[h])}–{OccupancyCalculator.HourLabel(heat.Hours[h] + 1)}",
                    Occupancy = percent,
                    People = people
                });
            }
        }
        return FromList(rows);
    }

    protected override Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<HourRow> rows,
        CancellationToken ct)
    {
        var list = rows.Where(r => r.Occupancy != null).ToList();
        var busiest = list.OrderByDescending(r => r.Occupancy).FirstOrDefault();
        var quietest = list.OrderBy(r => r.Occupancy).FirstOrDefault();
        var peakDay = list.GroupBy(r => r.Day).Select(g => new { Day = g.Key, Avg = g.Average(r => r.Occupancy) })
            .OrderByDescending(g => g.Avg).FirstOrDefault();
        return Task.FromResult(new List<Figure>
        {
            new("busiest", "Busiest slot", busiest?.Occupancy, "percent", Compare: false,
                Sub: busiest is null ? null : $"{busiest.Day} {busiest.Hour}"),
            new("quietest", "Quietest slot", quietest?.Occupancy, "percent", Compare: false,
                Sub: quietest is null ? null : $"{quietest.Day} {quietest.Hour}"),
            new("average", "Average across slots", list.Count == 0 ? null : Math.Round(list.Average(r => r.Occupancy!.Value), 1),
                "percent"),
            new("peak", "Busiest day", peakDay?.Avg is { } avg ? Math.Round(avg, 1) : null, "percent", Compare: false,
                Sub: peakDay?.Day)
        });
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<HourRow> rows,
        CancellationToken ct)
    {
        var heat = await HeatAsync(ctx, ct);
        return
        [
            ChartDtoBuilder.Heatmap("heatmap", "When the space is busy",
                "Average occupancy by weekday and hour — the darker the cell, the busier the hour", heat) with
            {
                Note = ChartDtoBuilder.QuietestSentence(heat)
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Average occupancy by weekday and hour, {ReportMath.Range(ctx.From, ctx.To)}.";
}

internal sealed class VisitRow
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Person { get; set; } = "";
    public string Kind { get; set; } = "";
    public DateOnly Date { get; set; }
    public DateTime In { get; set; }
    public DateTime? Out { get; set; }
    public decimal? Hours { get; set; }
    public string Method { get; set; } = "";
    public string? Workspace { get; set; }
    public string? Booking { get; set; }
    public string Corrected { get; set; } = "";
}

/// <summary>Who came in and when. Regalis records no Wi-Fi sessions, so the matching
/// Wi-Fi column the spec mentions has nothing to show and is left out.</summary>
internal sealed class CheckInLogReport : ReportHandler<VisitRow>
{
    public override string Key => "check-in-log";
    public override string Name => "Check-in / check-out log";
    public override string Description => "Who came in, when they came in and when they left, with the workspace and booking.";
    public override string Group => "operations";
    public override string Chip => "operations";
    public override string[] Keywords => ["check-in", "check in", "attendance", "visits", "presence", "access"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "visits";
    public override string DefaultSort => "in:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Select("kind", "Who", Analytics.Filters.Options(("customer", "Customers"),
            ("staff", "Staff")), allLabel: "Everyone"),
        Analytics.Filters.Customer()
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(VisitRow.Person), "Person", "text", Searchable: true),
        new(nameof(VisitRow.Kind), "Who", "text"),
        new(nameof(VisitRow.In), "Came in", "datetime"),
        new(nameof(VisitRow.Out), "Left", "datetime"),
        new(nameof(VisitRow.Hours), "Stayed", "hours", Total.Sum),
        new(nameof(VisitRow.Method), "How", "text"),
        new(nameof(VisitRow.Workspace), "Workspace", "text", Searchable: true),
        new(nameof(VisitRow.Booking), "Booking", "text", Searchable: true, Visible: false),
        new(nameof(VisitRow.Corrected), "Corrected", "text", Visible: false)
    ];

    protected override Task<IQueryable<VisitRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var visits = db.Attendances.AsNoTracking().Where(a => a.AttendanceDate >= ctx.From && a.AttendanceDate <= ctx.To);
        if (ctx.LocationId is { } location)
        {
            visits = visits.Where(a => a.LocationId == location);
        }
        if (ctx.CustomerId is { } customer)
        {
            visits = visits.Where(a => a.UserId == customer);
        }
        if (ctx.One("kind") is { } kind)
        {
            var subject = kind == "staff" ? AttendanceSubjectType.Staff : AttendanceSubjectType.Customer;
            visits = visits.Where(a => a.SubjectType == subject);
        }
        return Task.FromResult(
            from a in visits
            from u in db.Users.Where(u => u.UserId == a.UserId)
            from w in db.Workspaces.Where(w => w.WorkspaceId == a.WorkspaceId).DefaultIfEmpty()
            from b in db.Bookings.Where(b => b.BookingId == a.BookingId).DefaultIfEmpty()
            select new VisitRow
            {
                Id = a.AttendanceId,
                UserId = a.UserId,
                Person = u.FullName,
                Kind = a.SubjectType == AttendanceSubjectType.Staff ? "Staff" : "Customer",
                Date = a.AttendanceDate,
                In = a.CheckInUtc,
                Out = a.CheckOutUtc,
                Hours = a.TotalMinutes == null ? null : (decimal?)a.TotalMinutes / 60m,
                Method = a.CheckInMethod == AccessMethod.Qr ? "QR"
                    : a.CheckInMethod == AccessMethod.Card ? "Card"
                    : a.CheckInMethod == AccessMethod.Nfc ? "NFC"
                    : a.CheckInMethod == AccessMethod.App ? "App" : "Manual",
                Workspace = w == null ? null : w.WorkspaceName,
                Booking = b == null ? null : b.BookingNumber,
                Corrected = a.IsCorrected ? "Yes" : ""
            });
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<VisitRow> rows,
        CancellationToken ct)
    {
        var count = await CountAsync(rows, ct);
        var stay = await rows.Where(r => r.Hours != null).AverageAsync(r => r.Hours, ct);
        return
        [
            new("visits", "Visits", count, "count"),
            new("people", "People", await rows.Select(r => r.UserId).Distinct().CountAsync(ct), "count"),
            new("stay", "Average stay", stay, "hours"),
            new("open", "Not checked out", await rows.CountAsync(r => r.Out == null, ct), "count", Compare: false)
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<VisitRow> rows,
        CancellationToken ct)
    {
        var byDay = await rows.GroupBy(r => r.Date).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync(ct);
        var points = ChartDtoBuilder.DailyPoints(ctx.From, ctx.To, byDay.ToDictionary(d => d.Day, d => ((decimal)d.Count, 0m)));
        var arrivals = (await rows.Select(r => r.In).ToListAsync(ct))
            .GroupBy(t => IstClock.ToIst(t).Hour).ToDictionary(g => g.Key, g => g.Count());
        var hourPoints = arrivals.Count == 0 ? [] : Enumerable.Range(arrivals.Keys.Min(), arrivals.Keys.Max() - arrivals.Keys.Min() + 1)
            .Select(h => new ChartPointDto(OccupancyCalculator.HourLabel(h), null, arrivals.GetValueOrDefault(h)))
            .ToList();
        return
        [
            new ChartDto
            {
                Id = "per-day", Kind = "columns", Title = "Visits each day", ValueFormat = "count", Points = points,
                HighlightIndex = ChartDtoBuilder.Highest(points)
            },
            new ChartDto
            {
                Id = "arrivals", Kind = "columns", Title = "When people arrive", ValueFormat = "count",
                Points = hourPoints, HighlightIndex = ChartDtoBuilder.Highest(hourPoints)
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Every check-in between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "visits")}.";
}

internal sealed class TicketRow
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public string Title { get; set; } = "";
    public string Area { get; set; } = "";
    public string Category { get; set; } = "";
    public string Priority { get; set; } = "";
    public string AssignedTo { get; set; } = "";
    public DateTime Raised { get; set; }
    public DateTime Due { get; set; }
    public string Status { get; set; } = "";
    public DateTime? Closed { get; set; }
    public decimal? HoursTaken { get; set; }
}

/// <summary>Maintenance work raised in the period. A work item is tied to an area or
/// workspace rather than to an asset record, so that is what the Area column shows.</summary>
internal sealed class MaintenanceTicketsReport : ReportHandler<TicketRow>
{
    public override string Key => "maintenance-tickets";
    public override string Name => "Maintenance tickets";
    public override string Description => "Maintenance work raised, with the area, priority, who it was assigned to, when it was closed and how long it took.";
    public override string Group => "operations";
    public override string Chip => "operations";
    public override string[] Keywords => ["maintenance", "tickets", "repairs", "work", "housekeeping", "jobs"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "tickets";
    public override string DefaultSort => "raised:desc";

    private static readonly (MaintenanceWorkStatus Status, string Key, string Label)[] Statuses =
    [
        (MaintenanceWorkStatus.Pending, "pending", "Pending"),
        (MaintenanceWorkStatus.InProgress, "inprogress", "In progress"),
        (MaintenanceWorkStatus.Done, "done", "Done"),
        (MaintenanceWorkStatus.Verified, "verified", "Verified"),
        (MaintenanceWorkStatus.Rework, "rework", "Rework"),
        (MaintenanceWorkStatus.Overdue, "overdue", "Overdue"),
        (MaintenanceWorkStatus.Cancelled, "cancelled", "Cancelled")
    ];

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Multi("status", "Status",
            Statuses.Select(s => new ReportOption(s.Key, s.Label)).ToList(), allLabel: "Any status"),
        Analytics.Filters.Multi("priority", "Priority", Analytics.Filters.Options(("3", "High"), ("2", "Medium"),
            ("1", "Low")), allLabel: "Any priority")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(TicketRow.Number), "Ticket", "text", Searchable: true),
        new(nameof(TicketRow.Title), "Title", "text", Searchable: true),
        new(nameof(TicketRow.Area), "Area", "text", Searchable: true),
        new(nameof(TicketRow.Category), "Category", "text", Visible: false),
        new(nameof(TicketRow.Priority), "Priority", "status"),
        new(nameof(TicketRow.AssignedTo), "Assigned to", "text", Searchable: true),
        new(nameof(TicketRow.Raised), "Raised", "datetime"),
        new(nameof(TicketRow.Due), "Due", "datetime", Visible: false),
        new(nameof(TicketRow.Status), "Status", "status"),
        new(nameof(TicketRow.Closed), "Closed", "datetime"),
        new(nameof(TicketRow.HoursTaken), "Took", "hours")
    ];

    protected override Task<IQueryable<TicketRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var items = db.MaintenanceWorkItems.AsNoTracking()
            .Where(m => m.CreatedDate >= ctx.FromUtc && m.CreatedDate < ctx.ToUtc);
        if (ctx.LocationId is { } location)
        {
            items = items.Where(m => m.LocationId == location);
        }
        var statuses = ctx.Multi("status").Select(k => Statuses.FirstOrDefault(s => s.Key == k))
            .Where(s => s.Label is not null).Select(s => s.Status).ToList();
        if (statuses.Count > 0)
        {
            items = items.Where(m => statuses.Contains(m.Status));
        }
        var priorities = ctx.Ints("priority").Select(p => (MaintenancePriority)p).ToList();
        if (priorities.Count > 0)
        {
            items = items.Where(m => priorities.Contains(m.Priority));
        }
        return Task.FromResult(
            from m in items
            from u in db.Users.Where(u => u.UserId == m.AssignedToUserId).DefaultIfEmpty()
            from w in db.Workspaces.Where(w => w.WorkspaceId == m.WorkspaceId).DefaultIfEmpty()
            select new TicketRow
            {
                Id = m.MaintenanceWorkItemId,
                Number = m.WorkNumber,
                Title = m.Title,
                Area = w != null ? w.WorkspaceName : m.AreaName,
                Category = m.Category,
                Priority = m.Priority == MaintenancePriority.High ? "High"
                    : m.Priority == MaintenancePriority.Medium ? "Medium" : "Low",
                AssignedTo = u == null ? "—" : u.FullName,
                Raised = m.CreatedDate,
                Due = m.DueUtc,
                Status = m.Status == MaintenanceWorkStatus.Pending ? "Pending"
                    : m.Status == MaintenanceWorkStatus.InProgress ? "In progress"
                    : m.Status == MaintenanceWorkStatus.Done ? "Done"
                    : m.Status == MaintenanceWorkStatus.Verified ? "Verified"
                    : m.Status == MaintenanceWorkStatus.Rework ? "Rework"
                    : m.Status == MaintenanceWorkStatus.Overdue ? "Overdue" : "Cancelled",
                Closed = m.VerifiedUtc ?? m.CompletedUtc,
                HoursTaken = (m.VerifiedUtc ?? m.CompletedUtc) == null ? null
                    : (decimal?)EF.Functions.DateDiffMinute(m.CreatedDate, (m.VerifiedUtc ?? m.CompletedUtc)!.Value) / 60m
            });
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<TicketRow> rows,
        CancellationToken ct)
    {
        var took = await rows.Where(r => r.HoursTaken != null).AverageAsync(r => r.HoursTaken, ct);
        return
        [
            new("raised", "Raised", await CountAsync(rows, ct), "count", UpIsGood: false),
            new("closed", "Closed", await rows.CountAsync(r => r.Closed != null, ct), "count"),
            new("overdue", "Overdue now", await rows.CountAsync(r => r.Status == "Overdue", ct), "count",
                UpIsGood: false, Compare: false),
            new("took", "Average time to close", took, "hours", UpIsGood: false)
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<TicketRow> rows,
        CancellationToken ct)
    {
        var list = await rows.Select(r => new { r.Raised, r.Priority, r.Status }).ToListAsync(ct);
        string[] priorities = ["High", "Medium", "Low"];
        var (buckets, byWeek) = ChartDtoBuilder.Buckets(ctx.From, ctx.To);
        var stacks = buckets.Select(bucket =>
        {
            var values = priorities.Select(p => (decimal)list.Count(r => r.Priority == p &&
                IstClock.IstDateOf(r.Raised) >= bucket.Start && IstClock.IstDateOf(r.Raised) <= bucket.End)).ToList();
            return new ChartStackDto(bucket.Label, bucket.Start.ToString("yyyy-MM-dd"), values, values.Sum());
        }).ToList();
        var byStatus = list.GroupBy(r => r.Status).Select(g => (Label: g.Key, Count: (decimal)g.Count()))
            .OrderByDescending(g => g.Count).ToList();
        var folded = byStatus.Take(4).Select((s, i) => (s.Label, s.Count, i + 1)).ToList();
        if (byStatus.Count > 4)
        {
            folded.Add(("Other", byStatus.Skip(4).Sum(s => s.Count), 0));
        }
        var slices = ChartDtoBuilder.Slices(folded);
        return
        [
            new ChartDto
            {
                Id = "raised", Kind = "stackedcolumns",
                Title = byWeek ? "Tickets raised each week" : "Tickets raised each day",
                ValueFormat = "count",
                Series = priorities.Select((p, i) => new ChartSeriesDto(p, i + 1)).ToList(),
                Stacks = stacks
            },
            new ChartDto
            {
                Id = "status", Kind = "doughnut", Title = "Where they stand", ValueFormat = "count",
                Slices = slices, Total = slices.Sum(s => s.Value)
            }
        ];
    }

    public override async Task<string?> LiveFigureAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var open = await ctx.Db.MaintenanceWorkItems.AsNoTracking().CountAsync(m =>
            m.Status == MaintenanceWorkStatus.Pending || m.Status == MaintenanceWorkStatus.InProgress ||
            m.Status == MaintenanceWorkStatus.Rework || m.Status == MaintenanceWorkStatus.Overdue, ct);
        return $"{ReportMath.Count(open)} open";
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Maintenance tickets raised between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "tickets")}.";
}

internal sealed class AssetRow
{
    public long Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Location { get; set; } = "";
    public DateOnly? Purchased { get; set; }
    public decimal? Cost { get; set; }
    public decimal? Depreciation { get; set; }
    public decimal? CurrentValue { get; set; }
    public string Status { get; set; } = "";
    public string Condition { get; set; } = "";
}

/// <summary>
/// Every asset, valued straight-line over its category's useful life as of the end of
/// the range. Scrapped and lost assets are worth nothing; an asset whose category has no
/// useful life is held at cost. Assets with no purchase date are always listed.
/// </summary>
internal sealed class AssetRegisterReport : ReportHandler<AssetRow>
{
    public override string Key => "asset-register";
    public override string Name => "Asset register";
    public override string Description => "Every asset with location, purchase date, cost, depreciation and current value.";
    public override string Group => "operations";
    public override string Chip => "operations";
    public override string[] Keywords => ["assets", "register", "depreciation", "value", "equipment", "inventory"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "assets";
    public override string DefaultRange => "all";
    public override string DefaultSort => "code:asc";

    private static readonly (AssetStatus Status, string Key, string Label)[] Statuses =
    [
        (AssetStatus.InStore, "store", "In store"),
        (AssetStatus.InUse, "use", "In use"),
        (AssetStatus.InRepair, "repair", "In repair"),
        (AssetStatus.Scrapped, "scrapped", "Scrapped"),
        (AssetStatus.Lost, "lost", "Lost"),
        (AssetStatus.ReturnedToSupplier, "returned", "Returned")
    ];

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Select("category", "Category", lookups.AssetCategories, allLabel: "All categories"),
        Analytics.Filters.Multi("status", "Status", Statuses.Select(s => new ReportOption(s.Key, s.Label)).ToList(),
            ["store", "use", "repair"], "Any status")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(AssetRow.Code), "Code", "text", Searchable: true),
        new(nameof(AssetRow.Name), "Asset", "text", Searchable: true),
        new(nameof(AssetRow.Category), "Category", "text"),
        new(nameof(AssetRow.Location), "Location", "text", Searchable: true),
        new(nameof(AssetRow.Purchased), "Purchased", "date"),
        new(nameof(AssetRow.Cost), "Cost", "money", Total.Sum),
        new(nameof(AssetRow.Depreciation), "Depreciation", "money", Total.Sum),
        new(nameof(AssetRow.CurrentValue), "Current value", "money", Total.Sum),
        new(nameof(AssetRow.Status), "Status", "status"),
        new(nameof(AssetRow.Condition), "Condition", "text", Visible: false)
    ];

    protected override async Task<IQueryable<AssetRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var details = db.AssetDetails.AsNoTracking()
            .Where(d => d.PurchaseDate == null || (d.PurchaseDate >= ctx.From && d.PurchaseDate <= ctx.To));
        if (ctx.Int("category") is { } category)
        {
            details = details.Where(d => d.InventoryItem.AssetCategoryId == category);
        }
        var statuses = ctx.Multi("status").Select(k => Statuses.FirstOrDefault(s => s.Key == k))
            .Where(s => s.Label is not null).Select(s => s.Status).ToList();
        if (statuses.Count > 0)
        {
            details = details.Where(d => statuses.Contains(d.Status));
        }
        var raw = await (from d in details
            from l in db.AssetLocations.Where(l => l.AssetLocationId == d.AssetLocationId).DefaultIfEmpty()
            select new
            {
                d.InventoryItemId,
                d.InventoryItem.ItemCode,
                d.InventoryItem.ItemName,
                Category = d.InventoryItem.AssetCategory != null ? d.InventoryItem.AssetCategory.CategoryName : "Uncategorised",
                Life = d.InventoryItem.AssetCategory != null ? d.InventoryItem.AssetCategory.UsefulLifeYears : null,
                Location = l != null ? l.LocationName : "—",
                d.PurchaseDate,
                d.InventoryItem.UnitCost,
                d.Status,
                d.Condition
            }).ToListAsync(ct);

        return FromList(raw.Select(a =>
        {
            decimal? depreciation = null;
            if (a.UnitCost is { } cost)
            {
                if (a.Status is AssetStatus.Scrapped or AssetStatus.Lost or AssetStatus.ReturnedToSupplier)
                {
                    depreciation = cost;
                }
                else if (a.Life is { } life and > 0 && a.PurchaseDate is { } bought && bought <= ctx.To)
                {
                    var used = (decimal)(ctx.To.DayNumber - bought.DayNumber) / (life * 365.25m);
                    depreciation = Math.Round(cost * Math.Min(1m, used), 2, MidpointRounding.AwayFromZero);
                }
                else
                {
                    depreciation = 0m;
                }
            }
            return new AssetRow
            {
                Id = a.InventoryItemId,
                Code = a.ItemCode,
                Name = a.ItemName,
                Category = a.Category,
                Location = a.Location,
                Purchased = a.PurchaseDate,
                Cost = a.UnitCost,
                Depreciation = depreciation,
                CurrentValue = a.UnitCost - depreciation,
                Status = Statuses.First(s => s.Status == a.Status).Label,
                Condition = a.Condition switch
                {
                    AssetCondition.Good => "Good",
                    AssetCondition.Fair => "Fair",
                    AssetCondition.NeedsAttention => "Needs attention",
                    _ => "Damaged"
                }
            };
        }));
    }

    protected override Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<AssetRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        return Task.FromResult(new List<Figure>
        {
            new("assets", "Assets", list.Count, "count", Compare: false,
                Sub: list.Count(r => r.Cost is null) is var missing and > 0 ? $"{ReportText.Plural(missing, "assets")} without a cost" : null),
            new("cost", "Cost", list.Sum(r => r.Cost ?? 0), "money", Compare: false),
            new("value", "Current value", list.Sum(r => r.CurrentValue ?? 0), "money", Compare: false,
                Sub: $"As of {ReportMath.Day(ctx.To)}"),
            new("depreciation", "Depreciated", list.Sum(r => r.Depreciation ?? 0), "money", Compare: false)
        });
    }

    protected override Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<AssetRow> rows,
        CancellationToken ct)
    {
        var byCategory = rows.ToList().GroupBy(r => r.Category)
            .Select(g => (Label: g.Key, Value: g.Sum(r => r.CurrentValue ?? 0)))
            .OrderByDescending(g => g.Value).ToList();
        if (byCategory.Count == 0)
        {
            return Task.FromResult(new List<ChartDto>());
        }
        var folded = byCategory.Take(5).Select((c, i) => (c.Label, c.Value, i + 1)).ToList();
        if (byCategory.Count > 5)
        {
            folded.Add(("Other", byCategory.Skip(5).Sum(c => c.Value), 0));
        }
        var slices = ChartDtoBuilder.Slices(folded);
        return Task.FromResult(new List<ChartDto>
        {
            new()
            {
                Id = "by-category", Kind = "hbars", Title = "Current value by category", ValueFormat = "money",
                Slices = slices, Total = slices.Sum(s => s.Value)
            }
        });
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"The asset register as of {ReportMath.Day(ctx.To)} — {ReportText.Plural(count, "assets")}, worth {ReportMath.Money(ReportText.Num(totals, "currentValue"))} today.";
}

internal sealed class PurchaseOrderRow
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public DateOnly Date { get; set; }
    public string Supplier { get; set; } = "";
    public string Status { get; set; } = "";
    public decimal Taxable { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public DateOnly? Expected { get; set; }
    public decimal? Delivered { get; set; }
}

internal sealed class PurchaseOrdersReport : ReportHandler<PurchaseOrderRow>
{
    public override string Key => "purchase-orders";
    public override string Name => "Purchase orders";
    public override string Description => "Purchase orders by supplier, with the value, the status and what was delivered.";
    public override string Group => "operations";
    public override string Chip => "operations";
    public override string[] Keywords => ["purchase", "orders", "po", "suppliers", "vendors", "procurement", "delivered"];
    public override string Permission => PermissionCodes.Report.View;
    public override string RowNoun => "purchase orders";
    public override string DefaultSort => "date:desc";

    private static readonly (PurchaseOrderStatus Status, string Key, string Label)[] Statuses =
    [
        (PurchaseOrderStatus.Draft, "draft", "Draft"),
        (PurchaseOrderStatus.PendingApproval, "pending", "Awaiting approval"),
        (PurchaseOrderStatus.Approved, "approved", "Approved"),
        (PurchaseOrderStatus.Sent, "sent", "Sent"),
        (PurchaseOrderStatus.PartiallyReceived, "part", "Part received"),
        (PurchaseOrderStatus.Received, "received", "Received"),
        (PurchaseOrderStatus.Cancelled, "cancelled", "Cancelled")
    ];

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Multi("status", "Status", Statuses.Select(s => new ReportOption(s.Key, s.Label)).ToList(),
            allLabel: "Any status"),
        Analytics.Filters.Select("supplier", "Supplier", lookups.Suppliers, allLabel: "All suppliers")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(PurchaseOrderRow.Number), "PO", "text", Searchable: true),
        new(nameof(PurchaseOrderRow.Date), "Date", "date"),
        new(nameof(PurchaseOrderRow.Supplier), "Supplier", "text", Searchable: true),
        new(nameof(PurchaseOrderRow.Status), "Status", "status"),
        new(nameof(PurchaseOrderRow.Taxable), "Taxable value", "money", Total.Sum, Visible: false),
        new(nameof(PurchaseOrderRow.Tax), "Tax", "money", Total.Sum, Visible: false),
        new(nameof(PurchaseOrderRow.Total), "Value", "money", Total.Sum),
        new(nameof(PurchaseOrderRow.Expected), "Expected", "date"),
        new(nameof(PurchaseOrderRow.Delivered), "Delivered", "percent")
    ];

    protected override string? LinkFor(PurchaseOrderRow row) => $"/admin/billing/purchase-orders/{row.Id}";

    protected override Task<IQueryable<PurchaseOrderRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var orders = ctx.Db.PurchaseOrders.AsNoTracking().Where(p => p.PoDate >= ctx.From && p.PoDate <= ctx.To);
        if (ctx.LocationId is { } location)
        {
            orders = orders.Where(p => p.LocationId == location);
        }
        if (ctx.Int("supplier") is { } supplier)
        {
            orders = orders.Where(p => p.SupplierId == supplier);
        }
        var statuses = ctx.Multi("status").Select(k => Statuses.FirstOrDefault(s => s.Key == k))
            .Where(s => s.Label is not null).Select(s => s.Status).ToList();
        if (statuses.Count > 0)
        {
            orders = orders.Where(p => statuses.Contains(p.Status));
        }
        return Task.FromResult(orders.Select(p => new PurchaseOrderRow
        {
            Id = p.PurchaseOrderId,
            Number = p.PoNumber,
            Date = p.PoDate,
            Supplier = p.Supplier.SupplierName,
            Status = p.Status == PurchaseOrderStatus.Draft ? "Draft"
                : p.Status == PurchaseOrderStatus.PendingApproval ? "Awaiting approval"
                : p.Status == PurchaseOrderStatus.Approved ? "Approved"
                : p.Status == PurchaseOrderStatus.Sent ? "Sent"
                : p.Status == PurchaseOrderStatus.PartiallyReceived ? "Part received"
                : p.Status == PurchaseOrderStatus.Received ? "Received" : "Cancelled",
            Taxable = p.TaxableAmount,
            Tax = p.TaxAmount,
            Total = p.TotalAmount,
            Expected = p.ExpectedDeliveryDate,
            Delivered = p.Lines.Sum(l => l.Quantity) == 0 ? null
                : (decimal?)Math.Round(p.Lines.Sum(l => l.ReceivedQuantity) * 100m / p.Lines.Sum(l => l.Quantity), 1)
        }));
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<PurchaseOrderRow> rows,
        CancellationToken ct)
    {
        string[] awaiting = ["Approved", "Sent", "Part received"];
        return
        [
            new("orders", "Purchase orders", await CountAsync(rows, ct), "count"),
            new("value", "Value", await SumAsync(rows.Where(r => r.Status != "Cancelled"), r => r.Total, ct), "money",
                Sub: "Cancelled orders left out"),
            new("awaiting", "Awaiting delivery", await rows.CountAsync(r => awaiting.Contains(r.Status), ct), "count",
                Compare: false),
            new("received", "Received in full", await SumAsync(rows.Where(r => r.Status == "Received"), r => r.Total, ct),
                "money")
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<PurchaseOrderRow> rows,
        CancellationToken ct)
    {
        var bySupplier = await rows.Where(r => r.Status != "Cancelled").GroupBy(r => r.Supplier)
            .Select(g => new { Supplier = g.Key, Value = g.Sum(r => r.Total) })
            .OrderByDescending(g => g.Value).ToListAsync(ct);
        if (bySupplier.Count == 0)
        {
            return [];
        }
        var folded = bySupplier.Take(5).Select((s, i) => (s.Supplier, s.Value, i + 1)).ToList();
        if (bySupplier.Count > 5)
        {
            folded.Add(("Other suppliers", bySupplier.Skip(5).Sum(s => s.Value), 0));
        }
        var slices = ChartDtoBuilder.Slices(folded);
        var byStatus = await rows.GroupBy(r => r.Status).Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        var statusSlices = ChartDtoBuilder.Slices(byStatus.OrderByDescending(s => s.Count).Take(5)
            .Select((s, i) => (s.Status, (decimal)s.Count, i + 1)));
        return
        [
            new ChartDto
            {
                Id = "suppliers", Kind = "hbars", Title = "Value by supplier", ValueFormat = "money",
                Slices = slices, Total = slices.Sum(s => s.Value)
            },
            new ChartDto
            {
                Id = "status", Kind = "doughnut", Title = "Where they stand", ValueFormat = "count",
                Slices = statusSlices, Total = statusSlices.Sum(s => s.Value)
            }
        ];
    }

    public override async Task<string?> LiveFigureAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var waiting = await ctx.Db.PurchaseOrders.AsNoTracking()
            .CountAsync(p => p.Status == PurchaseOrderStatus.PendingApproval, ct);
        return waiting == 0 ? null : $"{ReportMath.Count(waiting)} awaiting approval";
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Purchase orders raised between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "purchase orders")}, {ReportMath.Money(ReportText.Num(totals, "total"))} in all.";
}
