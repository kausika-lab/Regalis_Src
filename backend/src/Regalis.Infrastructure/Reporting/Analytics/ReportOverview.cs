using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Reporting.Analytics;
using Regalis.Domain.Entities.Access;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Reporting;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Reporting.Analytics;

/// <summary>A version number the overview cache keys on. It moves whenever a booking,
/// payment, invoice, refund, pass or customer is saved, so a cached overview never
/// outlives the data behind it (spec §14.2: clear the cache when a payment or booking
/// is recorded).</summary>
public sealed class ReportCacheVersion
{
    private long _version;
    public long Current => Interlocked.Read(ref _version);
    public void Bump() => Interlocked.Increment(ref _version);
}

public sealed class ReportCacheInterceptor(ReportCacheVersion version) : SaveChangesInterceptor
{
    private bool _relevant;

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Inspect(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Inspect(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        Flush();
        return base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        Flush();
        return base.SavedChanges(eventData, result);
    }

    private void Inspect(DbContext? context)
    {
        if (context is null)
        {
            return;
        }
        _relevant |= context.ChangeTracker.Entries().Any(e =>
            e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted &&
            e.Entity is Booking or Payment or Invoice or WalletTransaction or CustomerPass or User or WorkspaceBlock
                or Attendance);
    }

    private void Flush()
    {
        if (_relevant)
        {
            version.Bump();
            _relevant = false;
        }
    }
}

/// <summary>
/// Totals each IST day, per location, per category — the one calculation behind both
/// the nightly rollup and the live figures, so a rolled-up day and a live day can
/// never disagree.
/// </summary>
internal static class MetricsCalculator
{
    /// <summary>The first IST day anything happened. Days before it have nothing to roll
    /// up, so they count as built — or a range reaching back past the first booking
    /// (a financial year, its previous period) would be calculated live for ever.</summary>
    public static async Task<DateOnly> FirstActivityAsync(RegalisDbContext db, DateOnly today, CancellationToken ct)
    {
        var firstBooking = await db.Bookings.AsNoTracking().MinAsync(b => (DateOnly?)b.BookingDate, ct);
        var firstPayment = await db.Payments.AsNoTracking().Where(p => p.PaidUtc != null).MinAsync(p => p.PaidUtc, ct);
        var firstCustomer = await db.Users.AsNoTracking().Where(u => u.UserType == UserType.Customer)
            .MinAsync(u => (DateTime?)u.CreatedDate, ct);
        var firstWallet = await db.WalletTransactions.AsNoTracking().MinAsync(t => (DateTime?)t.TransactionUtc, ct);
        return new[]
            {
                firstBooking,
                firstPayment is null ? null : IstClock.IstDateOf(firstPayment.Value),
                firstCustomer is null ? null : IstClock.IstDateOf(firstCustomer.Value),
                firstWallet is null ? null : IstClock.IstDateOf(firstWallet.Value)
            }
            .Where(d => d is not null).Select(d => d!.Value).DefaultIfEmpty(today).Min();
    }

    public static async Task<List<DailyMetric>> ComputeAsync(RegalisDbContext db, DateOnly from, DateOnly to,
        int? locationId, ReportCategories categories, DateTime nowUtc, CancellationToken ct)
    {
        var cells = new Dictionary<(DateOnly, int, string), DailyMetric>();
        DailyMetric Cell(DateOnly day, int location, string category)
        {
            if (!cells.TryGetValue((day, location, category), out var cell))
            {
                cell = new DailyMetric { MetricDate = day, LocationId = location, Category = category, BuiltUtc = nowUtc };
                cells[(day, location, category)] = cell;
            }
            return cell;
        }

        foreach (var row in await RevenueFacts.ByDayAsync(db, from, to, locationId, null, ct))
        {
            Cell(row.Day, row.LocationId ?? 0, categories.Of(row.Kind, row.WorkspaceTypeId)).RevenuePaise +=
                ReportMath.ToPaise(row.Amount);
        }

        var bookings = BookingRules.ExcludeInternal(db.Bookings.AsNoTracking())
            .Where(b => b.BookingDate >= from && b.BookingDate <= to);
        if (locationId is not null)
        {
            bookings = bookings.Where(b => b.LocationId == locationId);
        }
        var counted = await bookings
            .Where(b => BookingRules.Kept.Contains(b.Status) || b.Status == BookingStatus.Cancelled)
            .GroupBy(b => new { b.BookingDate, b.LocationId, b.WorkspaceTypeId, Cancelled = b.Status == BookingStatus.Cancelled })
            .Select(g => new { g.Key.BookingDate, g.Key.LocationId, g.Key.WorkspaceTypeId, g.Key.Cancelled, Count = g.Count() })
            .ToListAsync(ct);
        foreach (var g in counted)
        {
            var cell = Cell(g.BookingDate, g.LocationId, categories.TypeName(g.WorkspaceTypeId));
            if (g.Cancelled)
            {
                cell.CancelledCount += g.Count;
            }
            else
            {
                cell.BookingsCount += g.Count;
            }
        }

        var occupancy = await OccupancyCalculator.ComputeAsync(db, from, to, locationId, null, null, false, ct);
        var spaces = occupancy.Spaces.ToDictionary(s => s.WorkspaceId);
        foreach (var day in occupancy.Days)
        {
            var space = spaces[day.WorkspaceId];
            var cell = Cell(day.Day, space.LocationId, space.TypeName);
            cell.HoursBooked += Math.Round(day.Booked, 2);
            cell.HoursAvailable += Math.Round(day.Available, 2);
        }

        // New customers do not belong to a location; they sit on each day's marker row.
        var joined = await db.Users.AsNoTracking()
            .Where(u => u.UserType == UserType.Customer &&
                        u.CreatedDate >= ReportMath.IstStartUtc(from) && u.CreatedDate < ReportMath.IstStartUtc(to.AddDays(1)))
            .Select(u => u.CreatedDate).ToListAsync(ct);
        var joinedByDay = joined.GroupBy(IstClock.IstDateOf).ToDictionary(g => g.Key, g => g.Count());
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            Cell(day, 0, DailyMetric.DayMarker).NewCustomers = joinedByDay.GetValueOrDefault(day);
        }
        return cells.Values.ToList();
    }
}

internal sealed class DailyMetricsService(
    RegalisDbContext db,
    IDateTimeProvider clock,
    ILogger<DailyMetricsService> logger) : IDailyMetricsService
{
    public async Task<int> RebuildAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        // Only completed days are rolled up; today is always calculated live.
        var yesterday = IstClock.IstDateOf(clock.UtcNow).AddDays(-1);
        if (to > yesterday)
        {
            to = yesterday;
        }
        if (from > to)
        {
            return 0;
        }
        var categories = await ReportCategories.LoadAsync(db, ct);
        var built = 0;
        for (var start = from; start <= to; start = start.AddDays(31))
        {
            var end = start.AddDays(30) < to ? start.AddDays(30) : to;
            var rows = await MetricsCalculator.ComputeAsync(db, start, end, null, categories, clock.UtcNow, ct);
            var strategy = db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await db.Database.BeginTransactionAsync(ct);
                await db.DailyMetrics.Where(m => m.MetricDate >= start && m.MetricDate <= end).ExecuteDeleteAsync(ct);
                db.DailyMetrics.AddRange(rows);
                await db.SaveChangesAsync(ct);
                await transaction.CommitAsync(ct);
            });
            db.ChangeTracker.Clear();
            built += end.DayNumber - start.DayNumber + 1;
        }
        return built;
    }

    public async Task RunNightlyAsync(CancellationToken ct = default)
    {
        var yesterday = IstClock.IstDateOf(clock.UtcNow).AddDays(-1);
        // Yesterday, and the day before for anything recorded late.
        await RebuildAsync(yesterday.AddDays(-1), yesterday, ct);

        // Then fill any day that was never built, back to the first activity.
        var first = await MetricsCalculator.FirstActivityAsync(db, yesterday, ct);
        var built = await db.DailyMetrics.AsNoTracking()
            .Where(m => m.Category == DailyMetric.DayMarker && m.MetricDate >= first)
            .Select(m => m.MetricDate).ToListAsync(ct);
        var builtSet = built.ToHashSet();
        var missing = new List<DateOnly>();
        for (var day = first; day <= yesterday; day = day.AddDays(1))
        {
            if (!builtSet.Contains(day))
            {
                missing.Add(day);
            }
        }
        if (missing.Count > 0)
        {
            logger.LogInformation("Daily metrics: filling {Count} unbuilt day(s) from {First}", missing.Count, missing[0]);
            await RebuildAsync(missing[0], missing[^1], ct);
        }
    }
}

/// <summary>
/// The overview (spec §3): every card and chart in one response. Completed days come
/// from RPT_DailyMetric; today is calculated live. If any completed day was never rolled
/// up, the whole range is calculated live, a warning is logged and the page says so —
/// it never shows a partial figure as if it were current (§7.5).
/// </summary>
internal sealed class ReportOverviewService(
    RegalisDbContext db,
    ICurrentUser user,
    IDateTimeProvider clock,
    IMemoryCache cache,
    ReportCacheVersion cacheVersion,
    ReportFileBuilder files,
    ILogger<ReportOverviewService> logger) : IReportOverviewService
{
    private sealed record Saved(string Preset, DateOnly? From, DateOnly? To, int? LocationId, bool Compare);

    private DateOnly Today => IstClock.IstDateOf(clock.UtcNow);

    public async Task<OverviewDto> GetAsync(OverviewRequest request, CancellationToken ct = default)
    {
        if (!user.HasPermission(PermissionCodes.Report.View))
        {
            throw new ForbiddenException("You do not have access to Reports & Analysis.");
        }
        var (preset, from, to, locationId, compare) = await ResolveAsync(request, ct);
        var canFinance = user.HasPermission(PermissionCodes.Report.Finance);
        var canCustomers = user.HasPermission(PermissionCodes.Report.Customers);
        var key = $"rpt-overview:{user.UserId}:{preset}:{from}:{to}:{locationId}:{compare}:{canFinance}:{canCustomers}:{cacheVersion.Current}";
        if (cache.TryGetValue(key, out OverviewDto? cached) && cached is not null)
        {
            return cached;
        }
        var dto = await BuildAsync(preset, from, to, locationId, compare, canFinance, canCustomers, ct);
        cache.Set(key, dto, TimeSpan.FromMinutes(5));
        return dto;
    }

    private async Task<(string Preset, DateOnly From, DateOnly To, int? LocationId, bool Compare)> ResolveAsync(
        OverviewRequest request, CancellationToken ct)
    {
        var preference = await db.ReportPreferences
            .FirstOrDefaultAsync(p => p.UserId == user.UserId && p.ReportKey == ReportPreference.OverviewKey, ct);
        Saved? saved = null;
        if (preference?.FiltersJson is { } json)
        {
            try
            {
                saved = JsonSerializer.Deserialize<Saved>(json);
            }
            catch (JsonException)
            {
                saved = null;
            }
        }
        var asked = request.Preset is not null || request.From is not null || request.LocationId is not null ||
                    request.Compare is not null;
        var preset = (request.Preset ?? saved?.Preset ?? "30d").ToLowerInvariant();
        var locationId = request.Preset is null && request.LocationId is null ? saved?.LocationId : request.LocationId;
        if (locationId == 0)
        {
            locationId = null;
        }
        var compare = request.Compare ?? saved?.Compare ?? true;
        var today = Today;
        (DateOnly From, DateOnly To) range = preset switch
        {
            "7d" => (today.AddDays(-6), today),
            "90d" => (today.AddDays(-89), today),
            "fy" => (ReportMath.FinancialYear(today).From, today),
            "custom" => (request.From ?? saved?.From ?? today.AddDays(-29), request.To ?? saved?.To ?? today),
            _ => (today.AddDays(-29), today)
        };
        if (preset is not ("7d" or "30d" or "90d" or "fy" or "custom"))
        {
            preset = "30d";
        }
        if (range.From > range.To)
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "The start date is after the end date.", "from")]);
        }
        if (ReportMath.DaysIn(range.From, range.To) > 3700)
        {
            throw new AppValidationException([new ApiError(ErrorCodes.ValidationError,
                "Choose a range of ten years or less.", "from")]);
        }

        // Remembered per admin, in the database, so it follows them to another browser.
        if (asked || preference is null)
        {
            if (preference is null)
            {
                preference = new ReportPreference { UserId = user.UserId, ReportKey = ReportPreference.OverviewKey };
                db.ReportPreferences.Add(preference);
            }
            preference.FiltersJson = JsonSerializer.Serialize(new Saved(preset,
                preset == "custom" ? range.From : null, preset == "custom" ? range.To : null, locationId, compare));
            preference.UpdatedUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        return (preset, range.From, range.To, locationId, compare);
    }

    /// <summary>Metric rows for a range: rolled-up days plus today, or all live.</summary>
    private async Task<(List<DailyMetric> Rows, bool Live)> MetricsAsync(DateOnly from, DateOnly to, int? locationId,
        ReportCategories categories, CancellationToken ct)
    {
        var today = Today;
        var rows = new List<DailyMetric>();
        var live = false;
        var lastComplete = to < today ? to : today.AddDays(-1);
        if (from <= lastComplete)
        {
            var first = await cache.GetOrCreateAsync("rpt-first-activity", entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(1);
                return MetricsCalculator.FirstActivityAsync(db, today, ct);
            });
            // Only days on or after the first activity need a rolled-up row.
            var needFrom = from > first ? from : first;
            var days = needFrom > lastComplete ? 0 : ReportMath.DaysIn(needFrom, lastComplete);
            var markers = days == 0 ? 0 : await db.DailyMetrics.AsNoTracking()
                .CountAsync(m => m.Category == DailyMetric.DayMarker && m.MetricDate >= needFrom && m.MetricDate <= lastComplete, ct);
            if (markers == days)
            {
                var stored = db.DailyMetrics.AsNoTracking().Where(m => m.MetricDate >= from && m.MetricDate <= lastComplete);
                if (locationId is not null)
                {
                    stored = stored.Where(m => m.LocationId == locationId || m.Category == DailyMetric.DayMarker);
                }
                rows.AddRange(await stored.ToListAsync(ct));
            }
            else
            {
                logger.LogWarning("Daily metrics missing for {Missing} of {Days} day(s) between {From} and {To}; calculating live",
                    days - markers, days, from, lastComplete);
                live = true;
                rows.AddRange(await MetricsCalculator.ComputeAsync(db, from, lastComplete, locationId, categories,
                    clock.UtcNow, ct));
            }
        }
        if (to >= today && from <= today)
        {
            rows.AddRange(await MetricsCalculator.ComputeAsync(db, today, today, locationId, categories, clock.UtcNow, ct));
        }
        return (rows, live);
    }

    private static decimal Revenue(IEnumerable<DailyMetric> rows) =>
        ReportMath.FromPaise(rows.Where(r => r.Category != DailyMetric.DayMarker).Sum(r => r.RevenuePaise));

    private static decimal? Occupancy(IEnumerable<DailyMetric> rows)
    {
        var list = rows.ToList();
        var available = list.Sum(r => r.HoursAvailable);
        return available == 0 ? null : Math.Round(list.Sum(r => r.HoursBooked) / available * 100m, 0);
    }

    private async Task<OverviewDto> BuildAsync(string preset, DateOnly from, DateOnly to, int? locationId, bool compare,
        bool canFinance, bool canCustomers, CancellationToken ct)
    {
        var categories = await ReportCategories.LoadAsync(db, ct);
        var locations = await db.Locations.AsNoTracking().OrderBy(l => l.LocationName)
            .Select(l => new ReportOption(l.LocationId.ToString(), l.LocationName)).ToListAsync(ct);
        var (prevFrom, prevTo) = ReportMath.PreviousPeriod(from, to);
        var days = ReportMath.DaysIn(from, to);
        var (now, liveNow) = await MetricsAsync(from, to, locationId, categories, ct);
        var (before, livePrev) = await MetricsAsync(prevFrom, prevTo, locationId, categories, ct);

        // ── Cards ──
        var buckets = SparkBuckets(from, to);
        List<decimal?> Spark(Func<IEnumerable<DailyMetric>, decimal?> measure) =>
            buckets.Select(b => measure(now.Where(r => r.MetricDate >= b.Start && r.MetricDate <= b.End))).ToList();
        decimal Bookings(IEnumerable<DailyMetric> rows) => rows.Sum(r => r.BookingsCount);
        decimal NewCustomers(IEnumerable<DailyMetric> rows) =>
            rows.Where(r => r.Category == DailyMetric.DayMarker).Sum(r => r.NewCustomers);
        var periodWords = $"in the previous {days} days";
        KpiCardDto Card(string key, string label, decimal? value, decimal? previous, string format, int slot,
            string reportKey, List<decimal?> spark)
        {
            var (kind, change) = ReportMath.Compare(value, previous);
            var text = $"{SvgCharts.Format(previous, format)} {periodWords}";
            return new KpiCardDto(key, label, value, format, previous, kind, change, text, spark, slot, reportKey);
        }
        var cards = new List<KpiCardDto>();
        if (canFinance)
        {
            cards.Add(Card("revenue", "Revenue", Revenue(now), Revenue(before), "money", 1, "revenue-by-month",
                Spark(r => Revenue(r))));
        }
        cards.Add(Card("bookings", "Bookings", Bookings(now), Bookings(before), "count", 2, "booking",
            Spark(r => Bookings(r))));
        cards.Add(Card("customers", "New customers", NewCustomers(now), NewCustomers(before), "count", 3,
            canCustomers ? "new-signups" : "booking", Spark(r => NewCustomers(r))));
        cards.Add(Card("occupancy", "Average occupancy", Occupancy(now), Occupancy(before), "percent", 4,
            "desk-occupancy", Spark(Occupancy)));

        // ── Revenue over time and by category ──
        RevenueSeriesDto? revenue = null;
        ChartDto? byCategory = null;
        string? mover = null;
        if (canFinance)
        {
            revenue = RevenueSeries(from, to, prevFrom, prevTo, now, before);
            var catNow = now.Where(r => r.Category != DailyMetric.DayMarker).GroupBy(r => r.Category)
                .ToDictionary(g => g.Key, g => ReportMath.FromPaise(g.Sum(r => r.RevenuePaise)));
            var catBefore = before.Where(r => r.Category != DailyMetric.DayMarker).GroupBy(r => r.Category)
                .ToDictionary(g => g.Key, g => ReportMath.FromPaise(g.Sum(r => r.RevenuePaise)));
            var names = categories.Ordered.Concat(catNow.Keys.Where(k => categories.SlotOf(k) == 0))
                .Distinct().Where(n => categories.Ordered.Contains(n) || catNow.GetValueOrDefault(n) != 0).ToList();
            var values = names.Select(n => catNow.GetValueOrDefault(n)).ToList();
            var shares = ReportMath.SharesTo100(values.Select(v => Math.Max(0, v)).ToList());
            byCategory = new ChartDto
            {
                Id = "by-category", Kind = "hbars", Title = "Revenue by category",
                Subtitle = "Share of the period's revenue", ValueFormat = "money",
                Slices = names.Select((n, i) => new ChartSliceDto(n, values[i], shares[i], categories.SlotOf(n)))
                    .OrderByDescending(s => s.Value).ToList(),
                Total = values.Sum()
            };
            mover = RevenueByCategoryReport.BiggestMover(
                names.Select(n => (n, catNow.GetValueOrDefault(n), catBefore.GetValueOrDefault(n))), days);
        }

        // ── Bookings by type, each week ──
        var bookingsByType = await BookingsByTypeAsync(from, to, locationId, categories, ct);

        // ── When the space is busy, and the busiest spaces ──
        var occupancy = await OccupancyCalculator.ComputeAsync(db, from, to, locationId, null, null, false, ct);
        var heat = OccupancyCalculator.Heatmap(occupancy);
        var heatmap = ChartDtoBuilder.Heatmap("heatmap", "When the space is busy",
            "Occupancy by weekday and hour — the darker the cell, the busier the hour", heat);
        var busiest = new ChartDto
        {
            Id = "busiest", Kind = "hbars", Title = "Busiest workspaces",
            Subtitle = "Hours booked out of hours available", ValueFormat = "percent",
            Slices = occupancy.Spaces.Select(s =>
                {
                    var spaceDays = occupancy.Days.Where(d => d.WorkspaceId == s.WorkspaceId).ToList();
                    var available = spaceDays.Sum(d => d.Available);
                    var percent = available == 0 ? 0 : Math.Round(spaceDays.Sum(d => d.Booked) / available * 100m, 1);
                    return new ChartSliceDto(s.Name, percent, percent, 1,
                        $"{ReportMath.Hours(spaceDays.Sum(d => d.Booked))} of {ReportMath.Hours(available)}",
                        OccupancyCalculator.Band(percent));
                })
                .OrderByDescending(s => s.Value).ToList()
        };
        var busiestNote = await QuietSpaceAsync(to, locationId, ct);

        // ── Money owed and top customers ──
        MoneyOwedDto? owed = null;
        if (canFinance)
        {
            var grouped = await MoneyOwedReport.Owed(db, Today, Today, locationId, null)
                .GroupBy(r => r.Bucket)
                .Select(g => new { Bucket = g.Key, Balance = g.Sum(r => r.Balance), Count = g.Count() })
                .ToListAsync(ct);
            var slices = ChartDtoBuilder.Slices(MoneyOwedReport.Buckets.Select((b, i) =>
                (b, grouped.FirstOrDefault(g => g.Bucket == b)?.Balance ?? 0m, i + 1)));
            owed = new MoneyOwedDto(slices, grouped.Sum(g => g.Balance), grouped.Sum(g => g.Count));
        }
        TopCustomersDto? top = null;
        if (canFinance && canCustomers)
        {
            var ranked = await TopCustomersReport.RankAsync(db, from, to, locationId, ct);
            var rows = ranked.Take(5).Select(r => new TopCustomerDto(r.Id, r.Customer, r.Company, r.Revenue, r.Bookings)).ToList();
            var rest = ranked.Skip(5).ToList();
            var total = ranked.Sum(r => r.Revenue);
            // A share of the money that came in: a customer whose refunds outweigh their
            // payments would otherwise push the top five past 100%.
            var positive = ranked.Where(r => r.Revenue > 0).Sum(r => r.Revenue);
            var topShare = positive <= 0 ? null : (decimal?)Math.Round(rows.Where(r => r.Revenue > 0).Sum(r => r.Revenue) * 100m / positive, 0);
            top = new TopCustomersDto(rows,
                rest.Count == 0 ? null : new TopCustomerDto(null, $"Everyone else ({rest.Count})", null,
                    rest.Sum(r => r.Revenue), rest.Sum(r => r.Bookings)),
                total,
                topShare is null ? "No revenue in this period."
                    : $"The top {Math.Min(5, rows.Count)} brought in {topShare}% of the period's revenue.");
        }

        var visible = ReportRegistry.All.Where(h => ReportRegistry.CanSee(user, h)).ToList();
        var shortcuts = new[] { "booking", "revenue-by-month", "money-owed", "desk-occupancy", "gst-summary",
                "customer-list", "cancellations", "busy-hours" }
            .Select(k => visible.FirstOrDefault(h => h.Key == k)).Where(h => h is not null).Take(4)
            .Select(h => new ShortcutDto(h!.Key, h.Name, h.Description)).ToList();

        var hasData = now.Any(r => r.RevenuePaise != 0 || r.BookingsCount > 0 || r.NewCustomers > 0 || r.HoursBooked > 0);
        var periodText = compare
            ? $"{ReportMath.Range(from, to)}, compared with the {days} days before it."
            : $"{ReportMath.Range(from, to)}.";
        return new OverviewDto(preset, from, to, prevFrom, prevTo, days, periodText, compare, locationId, locations,
            liveNow || livePrev, hasData, cards, revenue, byCategory, mover, bookingsByType, heatmap with
            {
                Note = ChartDtoBuilder.QuietestSentence(heat)
            }, ChartDtoBuilder.QuietestSentence(heat), busiest, busiestNote, owed, top, shortcuts, canFinance, canCustomers,
            user.HasPermission(PermissionCodes.Report.Export));
    }

    /// <summary>Up to 14 equal slices of the range for a card's sparkline.</summary>
    private static List<(DateOnly Start, DateOnly End)> SparkBuckets(DateOnly from, DateOnly to)
    {
        var days = ReportMath.DaysIn(from, to);
        var count = Math.Min(14, days);
        var result = new List<(DateOnly, DateOnly)>();
        for (var i = 0; i < count; i++)
        {
            var start = from.AddDays(days * i / count);
            var end = from.AddDays(days * (i + 1) / count - 1);
            result.Add((start, end));
        }
        return result;
    }

    private static RevenueSeriesDto RevenueSeries(DateOnly from, DateOnly to, DateOnly prevFrom, DateOnly prevTo,
        List<DailyMetric> now, List<DailyMetric> before)
    {
        Dictionary<string, List<RevenuePointDto>> Group(DateOnly a, DateOnly b, List<DailyMetric> rows)
        {
            var byDay = rows.Where(r => r.Category != DailyMetric.DayMarker).GroupBy(r => r.MetricDate)
                .ToDictionary(g => g.Key, g => (Revenue: ReportMath.FromPaise(g.Sum(r => r.RevenuePaise)),
                    Bookings: g.Sum(r => r.BookingsCount)));
            var daily = new List<RevenuePointDto>();
            for (var d = a; d <= b; d = d.AddDays(1))
            {
                var (rev, count) = byDay.GetValueOrDefault(d);
                daily.Add(new RevenuePointDto(d.ToString("yyyy-MM-dd"), ChartDtoBuilder.DayLabel(d), rev, count));
            }
            var weekly = daily.GroupBy(p => ReportMath.WeekStart(DateOnly.Parse(p.Date, CultureInfo.InvariantCulture)))
                .Select(g => new RevenuePointDto(g.First().Date, $"Week of {g.First().Label}", g.Sum(p => p.Revenue),
                    g.Sum(p => p.Bookings))).ToList();
            var monthly = daily.GroupBy(p => p.Date[..7])
                .Select(g => new RevenuePointDto(g.Key + "-01",
                    ChartDtoBuilder.MonthLabel(DateOnly.Parse(g.Key + "-01", CultureInfo.InvariantCulture)),
                    g.Sum(p => p.Revenue), g.Sum(p => p.Bookings))).ToList();
            return new Dictionary<string, List<RevenuePointDto>>
            {
                ["daily"] = daily, ["weekly"] = weekly, ["monthly"] = monthly
            };
        }
        var days = ReportMath.DaysIn(from, to);
        var current = Group(from, to, now);
        return new RevenueSeriesDto(days <= 60 ? "daily" : days <= 366 ? "weekly" : "monthly", current,
            Group(prevFrom, prevTo, before), current["daily"].Sum(p => p.Revenue));
    }

    private async Task<ChartDto> BookingsByTypeAsync(DateOnly from, DateOnly to, int? locationId,
        ReportCategories categories, CancellationToken ct)
    {
        var bookings = BookingRules.ExcludeInternal(db.Bookings.AsNoTracking())
            .Where(b => BookingRules.Kept.Contains(b.Status) && b.BookingDate >= from && b.BookingDate <= to);
        var passes = db.CustomerPasses.AsNoTracking()
            .Where(p => p.Status != CustomerPassStatus.Cancelled &&
                        p.PurchaseUtc >= ReportMath.IstStartUtc(from) && p.PurchaseUtc < ReportMath.IstStartUtc(to.AddDays(1)));
        if (locationId is not null)
        {
            bookings = bookings.Where(b => b.LocationId == locationId);
            passes = passes.Where(p => p.LocationId == locationId);
        }
        var counts = await bookings.GroupBy(b => new { b.BookingDate, b.WorkspaceTypeId })
            .Select(g => new { g.Key.BookingDate, g.Key.WorkspaceTypeId, Count = g.Count() }).ToListAsync(ct);
        var sold = (await passes.Select(p => p.PurchaseUtc).ToListAsync(ct)).Select(IstClock.IstDateOf).ToList();

        var series = categories.Ordered.Where(c => c != ReportCategories.Services)
            .Select(c => new ChartSeriesDto(c, categories.SlotOf(c))).ToList();
        var (buckets, byWeek) = ChartDtoBuilder.Buckets(from, to);
        var stacks = buckets.Select(bucket =>
        {
            var values = series.Select(s => s.Name == ReportCategories.Passes
                ? sold.Count(d => d >= bucket.Start && d <= bucket.End)
                : (decimal)counts.Where(c => categories.TypeName(c.WorkspaceTypeId) == s.Name &&
                                             c.BookingDate >= bucket.Start && c.BookingDate <= bucket.End)
                    .Sum(c => c.Count)).ToList();
            return new ChartStackDto(byWeek ? $"Wk {bucket.Label}" : bucket.Label, bucket.Start.ToString("yyyy-MM-dd"),
                values, values.Sum());
        }).ToList();
        return new ChartDto
        {
            Id = "bookings-by-type", Kind = "stackedcolumns",
            Title = byWeek ? "Bookings by type, each week" : "Bookings by type, each day",
            Subtitle = byWeek ? "One column per week, with the total above it" : "Under three weeks, so one column per day",
            ValueFormat = "count", Series = series, Stacks = stacks
        };
    }

    /// <summary>The space that has been under 40% for three weeks or more, longest first.</summary>
    private async Task<string?> QuietSpaceAsync(DateOnly to, int? locationId, CancellationToken ct)
    {
        var lastWeekEnd = ReportMath.WeekStart(to).AddDays(-1);
        var firstWeek = lastWeekEnd.AddDays(-12 * 7 + 1);
        var weekly = await OccupancyCalculator.ComputeAsync(db, firstWeek, lastWeekEnd, locationId, null, null, false, ct);
        (string Name, int Weeks)? quietest = null;
        foreach (var space in weekly.Spaces)
        {
            var streak = 0;
            for (var week = ReportMath.WeekStart(lastWeekEnd); week >= firstWeek; week = week.AddDays(-7))
            {
                var inWeek = weekly.Days.Where(d => d.WorkspaceId == space.WorkspaceId && d.Day >= week && d.Day <= week.AddDays(6)).ToList();
                var available = inWeek.Sum(d => d.Available);
                if (available == 0 || inWeek.Sum(d => d.Booked) / available >= 0.4m)
                {
                    break;
                }
                streak++;
            }
            if (streak >= 3 && (quietest is null || streak > quietest.Value.Weeks))
            {
                quietest = (space.Name, streak);
            }
        }
        return quietest is { } q
            ? q.Weeks >= 12
                ? $"{q.Name} has been under 40% for 12 weeks or more."
                : $"{q.Name} has been under 40% for {q.Weeks} weeks running."
            : "No space has been under 40% for three weeks running.";
    }

    // ── The summary PDF (spec §3.1 Export summary) ──────────────────────────

    public async Task<ReportDownload> ExportPdfAsync(OverviewRequest request, CancellationToken ct = default)
    {
        if (!user.HasPermission(PermissionCodes.Report.Export))
        {
            throw new ForbiddenException("You do not have permission to export reports.");
        }
        var o = await GetAsync(request, ct);
        var (logo, company) = await files.LetterheadAsync(ct);
        var charts = new List<ChartDto>();
        if (o.Revenue is { } revenue)
        {
            var grouping = revenue.DefaultGrouping;
            charts.Add(new ChartDto
            {
                Id = "revenue", Kind = "line", Title = "Revenue over time", ValueFormat = "money",
                Points = revenue.Current[grouping].Select(p => new ChartPointDto(p.Label, p.Date, p.Revenue)).ToList(),
                Previous = o.Compare
                    ? revenue.Previous[grouping].Select(p => new ChartPointDto(p.Label, p.Date, p.Revenue)).ToList()
                    : null
            });
        }
        if (o.RevenueByCategory is { } category)
        {
            charts.Add(category);
        }
        charts.Add(o.BookingsByType);
        charts.Add(o.Heatmap);
        charts.Add(o.Busiest with { Slices = o.Busiest.Slices?.Take(6).ToList() });
        if (o.MoneyOwed is { } owed)
        {
            charts.Add(new ChartDto
            {
                Id = "owed", Kind = "hbars", Title = $"Money owed — {ReportMath.Money(owed.Total)} on {ReportText.Plural(owed.Invoices, "invoices")}",
                ValueFormat = "money", Slices = owed.Buckets, Total = owed.Total
            });
        }
        var exportedAt = IstClock.ToIst(clock.UtcNow).ToString("d MMM yyyy, HH:mm", CultureInfo.InvariantCulture) + " IST";

        var pdf = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(26);
            page.DefaultTextStyle(s => s.FontSize(8.5f).FontColor("#1F1B16"));
            page.Header().PaddingBottom(8).Row(row =>
            {
                PdfLogo.Compose(row, logo);
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(company).FontSize(9).FontColor("#6B655C");
                    c.Item().Text("Reports & analysis").FontSize(15).Bold();
                    c.Item().Text(o.PeriodText).FontColor("#6B655C");
                });
                row.ConstantItem(200).AlignRight().Text($"Exported {exportedAt}").FontColor("#6B655C");
            });
            page.Content().Column(col =>
            {
                if (o.CalculatedLive)
                {
                    col.Item().PaddingBottom(4).Text("Figures calculated live.").Italic().FontColor("#6B655C");
                }
                col.Item().PaddingBottom(10).Row(row =>
                {
                    foreach (var card in o.Cards)
                    {
                        row.RelativeItem().Border(0.5f).BorderColor("#E6E1D8").Padding(8).Column(c =>
                        {
                            c.Item().Text(card.Label).FontColor("#6B655C");
                            c.Item().Text(SvgCharts.Format(card.Value, card.Format)).FontSize(16).Bold();
                            if (o.Compare)
                            {
                                c.Item().Text(card.ChangeKind switch
                                {
                                    "percent" => $"{(card.Change >= 0 ? "▲" : "▼")} {Math.Abs(card.Change!.Value):0.0}%",
                                    "absolute" => $"{(card.Change >= 0 ? "▲" : "▼")} {SvgCharts.Format(Math.Abs(card.Change!.Value), card.Format)}",
                                    _ => "No comparison"
                                }).FontColor(card.ChangeKind is "percent" or "absolute"
                                    ? card.Change >= 0 ? "#0F7D55" : "#C2452A" : "#6B655C");
                                c.Item().Text(card.PreviousText).FontSize(7.5f).FontColor("#6B655C");
                            }
                        });
                        row.ConstantItem(8);
                    }
                });
                foreach (var chart in charts)
                {
                    var svg = SvgCharts.Render(chart, 780, 180);
                    if (svg is null)
                    {
                        continue;
                    }
                    col.Item().PaddingTop(4).Text(chart.Title).Bold().FontSize(10);
                    col.Item().PaddingBottom(8).Svg(svg).FitWidth();
                }
                if (o.TopCustomers is { } top)
                {
                    col.Item().PaddingTop(4).Text("Top customers").Bold().FontSize(10);
                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(cd =>
                        {
                            cd.RelativeColumn(3);
                            cd.RelativeColumn(2);
                            cd.RelativeColumn(1);
                        });
                        table.Header(h =>
                        {
                            h.Cell().Background("#F3EEE4").Padding(4).Text("Customer").Bold();
                            h.Cell().Background("#F3EEE4").Padding(4).AlignRight().Text("Revenue").Bold();
                            h.Cell().Background("#F3EEE4").Padding(4).AlignRight().Text("Bookings").Bold();
                        });
                        foreach (var r in top.Rows.Concat(top.EveryoneElse is null ? [] : [top.EveryoneElse]))
                        {
                            table.Cell().BorderBottom(0.5f).BorderColor("#EEE9E0").Padding(3.5f).Text(r.Name);
                            table.Cell().BorderBottom(0.5f).BorderColor("#EEE9E0").Padding(3.5f).AlignRight().Text(ReportMath.Money(r.Revenue));
                            table.Cell().BorderBottom(0.5f).BorderColor("#EEE9E0").Padding(3.5f).AlignRight().Text(ReportMath.Count(r.Bookings));
                        }
                    });
                    col.Item().PaddingTop(4).Text(top.Sentence).FontColor("#6B655C");
                }
            });
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ");
                text.CurrentPageNumber();
                text.Span(" of ");
                text.TotalPages();
            });
        })).GeneratePdf();
        return new ReportDownload(pdf, "application/pdf", $"reports-and-analysis_{o.From:yyyy-MM-dd}_{o.To:yyyy-MM-dd}.pdf");
    }
}
