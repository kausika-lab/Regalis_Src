using Microsoft.EntityFrameworkCore;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Reporting.Analytics;

/// <summary>
/// The categories money and bookings are split into, each with a fixed palette slot
/// (spec §10.1: colour follows the thing, not its rank). Workspace types that have at
/// least one space come first, in their display order, then passes, then services.
/// Slots run 1–5; anything after the fifth folds into "Other" (slot 0) rather than
/// taking a sixth colour. The list does not depend on the date range or the filters,
/// so removing a category never repaints the others.
/// </summary>
internal sealed class ReportCategories
{
    public const string Passes = "Passes & memberships";
    public const string Services = "Services & coffee";
    public const string Other = "Other";

    private readonly Dictionary<int, string> _typeNames;
    private readonly Dictionary<string, int> _slots;

    private ReportCategories(Dictionary<int, string> typeNames, List<string> ordered)
    {
        _typeNames = typeNames;
        Ordered = ordered;
        _slots = ordered.Select((name, index) => (name, index))
            .ToDictionary(x => x.name, x => x.index < 5 ? x.index + 1 : 0);
    }

    /// <summary>Every category in palette order, "Other" excluded.</summary>
    public List<string> Ordered { get; }

    public static async Task<ReportCategories> LoadAsync(RegalisDbContext db, CancellationToken ct)
    {
        var types = await db.WorkspaceTypes.AsNoTracking()
            .OrderBy(t => t.DisplayOrder).ThenBy(t => t.WorkspaceTypeId)
            .Select(t => new { t.WorkspaceTypeId, t.TypeName, HasSpaces = t.Workspaces.Any() })
            .ToListAsync(ct);
        var names = types.ToDictionary(t => t.WorkspaceTypeId, t => t.TypeName);
        var ordered = types.Where(t => t.HasSpaces).Select(t => t.TypeName).Distinct().ToList();
        ordered.Add(Passes);
        ordered.Add(Services);
        return new ReportCategories(names, ordered);
    }

    public int SlotOf(string category) => _slots.GetValueOrDefault(category, 0);

    public string TypeName(int? workspaceTypeId) =>
        workspaceTypeId is { } id && _typeNames.TryGetValue(id, out var name) ? name : Other;

    public string Of(byte kind, int? workspaceTypeId) => kind switch
    {
        RevenueKinds.Booking => TypeName(workspaceTypeId),
        RevenueKinds.Pass => Passes,
        RevenueKinds.Service => Services,
        _ => Other
    };
}

internal static class RevenueKinds
{
    public const byte Booking = 1;
    public const byte Pass = 2;
    public const byte Service = 3;
    public const byte Other = 9;
}

/// <summary>One payment received, or one refund paid back (negative), with what it was for.</summary>
internal sealed class RevenueFact
{
    public long Id { get; set; }
    public DateTime Utc { get; set; }
    public decimal Amount { get; set; }
    public long CustomerId { get; set; }
    public int? LocationId { get; set; }
    public int? WorkspaceTypeId { get; set; }
    public byte Kind { get; set; }
    public PaymentMethod? Method { get; set; }
}

/// <summary>Revenue grouped by IST day and category.</summary>
internal sealed record RevenueDayRow(DateOnly Day, byte Kind, int? WorkspaceTypeId, int? LocationId, decimal Amount);

/// <summary>
/// Revenue means money received, net of refunds, in IST (spec §8.2):
///   + successful payments, on the day they were paid, for a booking, a pass, a service
///     order or an invoice — wallet top-ups are NOT revenue;
///   + the wallet spent on a booking, pass or service order, on the day it was spent,
///     read from the wallet debit (the matching wallet-method payment is skipped so a
///     spend is never counted twice);
///   − refunds, on the day they were paid back. Regalis refunds to the customer's
///     wallet (TRN_WalletTransaction, category Refund / Cancellation, credit).
/// Every query applies its filters in SQL and aggregates there.
/// </summary>
internal static class RevenueFacts
{
    public static IQueryable<RevenueFact> Payments(RegalisDbContext db, DateTime fromUtc, DateTime toUtc)
    {
        // Wallet-method payments are left out: the wallet debit beside them is the spend
        // (see WalletSpends), and older records carry only the debit.
        return from p in db.Payments.AsNoTracking()
            where p.Status == PaymentStatus.Success && p.PaidUtc >= fromUtc && p.PaidUtc < toUtc &&
                  p.PaymentMethod != PaymentMethod.Wallet &&
                  (p.BookingId != null || p.CustomerPassId != null || p.ServiceOrderId != null ||
                   p.InvoiceId != null)
            from b in db.Bookings.Where(x => x.BookingId == p.BookingId).DefaultIfEmpty()
            from cp in db.CustomerPasses.Where(x => x.CustomerPassId == p.CustomerPassId).DefaultIfEmpty()
            from so in db.ServiceOrders.Where(x => x.ServiceOrderId == p.ServiceOrderId).DefaultIfEmpty()
            from inv in db.Invoices.Where(x => x.InvoiceId == p.InvoiceId).DefaultIfEmpty()
            from ib in db.Bookings.Where(x => inv != null && x.BookingId == inv.BookingId).DefaultIfEmpty()
            select new RevenueFact
            {
                Id = p.PaymentId,
                Utc = p.PaidUtc!.Value,
                Amount = p.Amount,
                CustomerId = p.CustomerId,
                Method = p.PaymentMethod,
                Kind = b != null || ib != null ? RevenueKinds.Booking
                    : cp != null || (inv != null && inv.CustomerPassId != null) ? RevenueKinds.Pass
                    : so != null || (inv != null && inv.ServiceOrderId != null) ? RevenueKinds.Service
                    : RevenueKinds.Other,
                WorkspaceTypeId = b != null ? (int?)b.WorkspaceTypeId : ib != null ? (int?)ib.WorkspaceTypeId : null,
                LocationId = b != null ? (int?)b.LocationId
                    : cp != null ? (int?)cp.LocationId
                    : so != null ? (int?)so.LocationId
                    : inv != null ? (int?)inv.LocationId
                    : null
            };
    }

    /// <summary>The wallet spent on a booking, pass or service order — revenue on the day
    /// it is spent (spec §8.2), from the wallet ledger itself.</summary>
    public static IQueryable<RevenueFact> WalletSpends(RegalisDbContext db, DateTime fromUtc, DateTime toUtc)
    {
        return from t in db.WalletTransactions.AsNoTracking()
            where t.Direction == WalletDirection.Debit &&
                  (t.Category == WalletCategory.Booking || t.Category == WalletCategory.DayPass ||
                   t.Category == WalletCategory.MonthlyPass || t.Category == WalletCategory.ServiceOrder) &&
                  t.TransactionUtc >= fromUtc && t.TransactionUtc < toUtc
            from b in db.Bookings.Where(x => t.ReferenceType == "Booking" && x.BookingId == t.ReferenceId)
                .DefaultIfEmpty()
            from cp in db.CustomerPasses.Where(x => t.ReferenceType == "CustomerPass" && x.CustomerPassId == t.ReferenceId)
                .DefaultIfEmpty()
            select new RevenueFact
            {
                Id = t.WalletTransactionId,
                Utc = t.TransactionUtc,
                Amount = t.Amount,
                CustomerId = t.CustomerId,
                Method = PaymentMethod.Wallet,
                Kind = t.Category == WalletCategory.Booking ? RevenueKinds.Booking
                    : t.Category == WalletCategory.ServiceOrder ? RevenueKinds.Service : RevenueKinds.Pass,
                WorkspaceTypeId = b != null ? (int?)b.WorkspaceTypeId : null,
                LocationId = b != null ? (int?)b.LocationId : cp != null ? (int?)cp.LocationId : null
            };
    }

    public static IEnumerable<IQueryable<RevenueFact>> All(RegalisDbContext db, DateTime fromUtc, DateTime toUtc) =>
        [Payments(db, fromUtc, toUtc), WalletSpends(db, fromUtc, toUtc), Refunds(db, fromUtc, toUtc)];

    public static IQueryable<RevenueFact> Refunds(RegalisDbContext db, DateTime fromUtc, DateTime toUtc)
    {
        return from t in db.WalletTransactions.AsNoTracking()
            where (t.Category == WalletCategory.Refund || t.Category == WalletCategory.Cancellation) &&
                  t.Direction == WalletDirection.Credit &&
                  t.TransactionUtc >= fromUtc && t.TransactionUtc < toUtc
            from b in db.Bookings.Where(x => t.ReferenceType == "Booking" && x.BookingId == t.ReferenceId)
                .DefaultIfEmpty()
            from cp in db.CustomerPasses.Where(x => t.ReferenceType == "CustomerPass" && x.CustomerPassId == t.ReferenceId)
                .DefaultIfEmpty()
            select new RevenueFact
            {
                Id = t.WalletTransactionId,
                Utc = t.TransactionUtc,
                Amount = -t.Amount,
                CustomerId = t.CustomerId,
                Method = PaymentMethod.Wallet,
                Kind = b != null ? RevenueKinds.Booking : cp != null ? RevenueKinds.Pass : RevenueKinds.Other,
                WorkspaceTypeId = b != null ? (int?)b.WorkspaceTypeId : null,
                LocationId = b != null ? (int?)b.LocationId : cp != null ? (int?)cp.LocationId : null
            };
    }

    public static IQueryable<RevenueFact> Filter(IQueryable<RevenueFact> facts, int? locationId, long? customerId)
    {
        if (locationId is not null)
        {
            facts = facts.Where(f => f.LocationId == locationId);
        }
        if (customerId is not null)
        {
            facts = facts.Where(f => f.CustomerId == customerId);
        }
        return facts;
    }

    /// <summary>Net revenue per IST day and category, payments and refunds together.</summary>
    public static async Task<List<RevenueDayRow>> ByDayAsync(RegalisDbContext db, DateOnly from, DateOnly to,
        int? locationId, long? customerId, CancellationToken ct)
    {
        var fromUtc = ReportMath.IstStartUtc(from);
        var toUtc = ReportMath.IstStartUtc(to.AddDays(1));
        var rows = new List<RevenueDayRow>();
        foreach (var source in All(db, fromUtc, toUtc))
        {
            var grouped = await Filter(source, locationId, customerId)
                .GroupBy(f => new { Day = f.Utc.AddMinutes(330).Date, f.Kind, f.WorkspaceTypeId, f.LocationId })
                .Select(g => new { g.Key.Day, g.Key.Kind, g.Key.WorkspaceTypeId, g.Key.LocationId, Amount = g.Sum(x => x.Amount) })
                .ToListAsync(ct);
            rows.AddRange(grouped.Select(g =>
                new RevenueDayRow(DateOnly.FromDateTime(g.Day), g.Kind, g.WorkspaceTypeId, g.LocationId, g.Amount)));
        }
        return rows;
    }

    /// <summary>Net revenue per customer in the range.</summary>
    public static async Task<Dictionary<long, decimal>> ByCustomerAsync(RegalisDbContext db, DateOnly from,
        DateOnly to, int? locationId, CancellationToken ct)
    {
        var fromUtc = ReportMath.IstStartUtc(from);
        var toUtc = ReportMath.IstStartUtc(to.AddDays(1));
        var totals = new Dictionary<long, decimal>();
        foreach (var source in All(db, fromUtc, toUtc))
        {
            var grouped = await Filter(source, locationId, null)
                .GroupBy(f => f.CustomerId)
                .Select(g => new { CustomerId = g.Key, Amount = g.Sum(x => x.Amount) })
                .ToListAsync(ct);
            foreach (var g in grouped)
            {
                totals[g.CustomerId] = totals.GetValueOrDefault(g.CustomerId) + g.Amount;
            }
        }
        return totals;
    }

    public static async Task<decimal> TotalAsync(RegalisDbContext db, DateOnly from, DateOnly to,
        int? locationId, long? customerId, CancellationToken ct)
    {
        var fromUtc = ReportMath.IstStartUtc(from);
        var toUtc = ReportMath.IstStartUtc(to.AddDays(1));
        decimal total = 0;
        foreach (var source in All(db, fromUtc, toUtc))
        {
            total += await Filter(source, locationId, customerId).SumAsync(f => (decimal?)f.Amount, ct) ?? 0m;
        }
        return total;
    }
}

/// <summary>Which bookings count (spec §8.5).</summary>
internal static class BookingRules
{
    /// <summary>Counted as a booking and in occupancy: kept, including no-shows.
    /// Pending holds and expired holds were never bookings; cancelled ones are counted
    /// separately.</summary>
    public static readonly BookingStatus[] Kept =
        [BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Completed, BookingStatus.NoShow];

    /// <summary>Internal and staff bookings: the "customer" is not a customer account.</summary>
    public static IQueryable<Booking> ExcludeInternal(IQueryable<Booking> bookings) =>
        bookings.Where(b => b.Customer.UserType == UserType.Customer);

    /// <summary>The booking report's status options → stored statuses.</summary>
    public static BookingStatus[] StatusesFor(string option) => option switch
    {
        "confirmed" => [BookingStatus.Confirmed, BookingStatus.CheckedIn],
        "completed" => [BookingStatus.Completed],
        "cancelled" => [BookingStatus.Cancelled],
        "noshow" => [BookingStatus.NoShow],
        "pending" => [BookingStatus.Pending],
        "expired" => [BookingStatus.Expired],
        _ => []
    };

    public static string StatusLabel(BookingStatus status) => status switch
    {
        BookingStatus.Confirmed => "Confirmed",
        BookingStatus.CheckedIn => "Confirmed",
        BookingStatus.Completed => "Completed",
        BookingStatus.Cancelled => "Cancelled",
        BookingStatus.NoShow => "No-show",
        BookingStatus.Pending => "Awaiting payment",
        BookingStatus.Expired => "Expired",
        _ => status.ToString()
    };

    public static string PaymentModeGroup(PaymentMethod method) => method switch
    {
        PaymentMethod.Wallet => "Wallet",
        PaymentMethod.Upi or PaymentMethod.CreditCard or PaymentMethod.DebitCard or PaymentMethod.NetBanking => "Online",
        _ => "Desk / invoice"
    };

    public static string PaymentMethodLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Wallet => "Wallet",
        PaymentMethod.Upi => "UPI",
        PaymentMethod.CreditCard => "Credit card",
        PaymentMethod.DebitCard => "Debit card",
        PaymentMethod.NetBanking => "Bank transfer",
        PaymentMethod.Cash => "Cash",
        _ => "Other"
    };
}

/// <summary>One bookable space with the opening hours of its location.</summary>
internal sealed record OccupancySpace(
    int WorkspaceId,
    string Code,
    string Name,
    int WorkspaceTypeId,
    string TypeName,
    int LocationId,
    int Capacity,
    TimeOnly Opens,
    TimeOnly Closes,
    HashSet<DayOfWeek> OpenDays);

/// <summary>Per space, per IST day: space-hours booked, blocked and open.</summary>
internal sealed record OccupancyDay(int WorkspaceId, DateOnly Day, decimal Booked, decimal Blocked, decimal Available);

internal sealed class OccupancyResult
{
    public List<OccupancySpace> Spaces { get; init; } = [];
    public List<OccupancyDay> Days { get; init; } = [];
    /// <summary>[weekday, hour] → booked minutes, open minutes, booked seat-minutes, matching days.</summary>
    public Dictionary<(DayOfWeek Day, int Hour), (decimal Booked, decimal Open, decimal Seats, int Days)> Hours { get; init; } = [];
    public int FirstHour { get; init; }
    public int LastHour { get; init; }
    public bool OpensSunday { get; init; }
}

/// <summary>
/// Occupancy = hours booked ÷ hours available (spec §9.3). A booking takes the whole
/// space, so the unit is the space-hour: each bookable space contributes its location's
/// opening hours on its opening days, minus any time it was blocked or under
/// maintenance (so a closed room does not look empty), and a booking contributes the
/// part of it that falls inside opening hours. Cancelled bookings never count; internal
/// bookings count only when asked for.
/// </summary>
internal static class OccupancyCalculator
{
    public static async Task<OccupancyResult> ComputeAsync(RegalisDbContext db, DateOnly from, DateOnly to,
        int? locationId, IReadOnlyCollection<int>? workspaceTypeIds, int? workspaceId, bool includeInternal,
        CancellationToken ct)
    {
        var locations = await db.Locations.AsNoTracking()
            .Select(l => new { l.LocationId, l.WorkingHoursStart, l.WorkingHoursEnd, l.WorkingDays })
            .ToDictionaryAsync(l => l.LocationId, ct);

        var spacesQuery = db.Workspaces.AsNoTracking()
            .Where(w => w.IsActive && w.IsBookable && w.Status != WorkspaceStatus.Disabled);
        if (locationId is not null)
        {
            spacesQuery = spacesQuery.Where(w => w.LocationId == locationId);
        }
        if (workspaceTypeIds is { Count: > 0 })
        {
            spacesQuery = spacesQuery.Where(w => workspaceTypeIds.Contains(w.WorkspaceTypeId));
        }
        if (workspaceId is not null)
        {
            spacesQuery = spacesQuery.Where(w => w.WorkspaceId == workspaceId);
        }
        var rawSpaces = await spacesQuery
            .OrderBy(w => w.WorkspaceCode)
            .Select(w => new
            {
                w.WorkspaceId, w.WorkspaceCode, w.WorkspaceName, w.WorkspaceTypeId,
                w.WorkspaceType.TypeName, w.LocationId, w.Capacity
            })
            .ToListAsync(ct);
        var spaces = rawSpaces
            .Where(w => locations.ContainsKey(w.LocationId))
            .Select(w =>
            {
                var location = locations[w.LocationId];
                return new OccupancySpace(w.WorkspaceId, w.WorkspaceCode, w.WorkspaceName, w.WorkspaceTypeId,
                    w.TypeName, w.LocationId, Math.Max(1, w.Capacity), location.WorkingHoursStart,
                    location.WorkingHoursEnd, ParseDays(location.WorkingDays));
            })
            .ToList();

        var fromUtc = ReportMath.IstStartUtc(from);
        var toUtc = ReportMath.IstStartUtc(to.AddDays(1));
        var ids = spaces.Select(s => s.WorkspaceId).ToList();

        var bookingQuery = db.Bookings.AsNoTracking()
            .Where(b => ids.Contains(b.WorkspaceId) && BookingRules.Kept.Contains(b.Status) &&
                        b.StartUtc < toUtc && fromUtc < b.EndUtc);
        if (!includeInternal)
        {
            bookingQuery = BookingRules.ExcludeInternal(bookingQuery);
        }
        var bookings = (await bookingQuery
                .Select(b => new { b.WorkspaceId, b.StartUtc, b.EndUtc })
                .ToListAsync(ct))
            .GroupBy(b => b.WorkspaceId)
            .ToDictionary(g => g.Key, g => g.Select(b => (b.StartUtc, b.EndUtc)).ToList());
        var blocks = (await db.WorkspaceBlocks.AsNoTracking()
                .Where(k => ids.Contains(k.WorkspaceId) && k.StartUtc < toUtc && fromUtc < k.EndUtc)
                .Select(k => new { k.WorkspaceId, k.StartUtc, End = k.ReleasedUtc ?? k.EndUtc })
                .ToListAsync(ct))
            .GroupBy(k => k.WorkspaceId)
            .ToDictionary(g => g.Key, g => g.Select(k => (k.StartUtc, k.End)).ToList());

        var days = new List<OccupancyDay>();
        var hours = new Dictionary<(DayOfWeek, int), (decimal Booked, decimal Open, decimal Seats, int Days)>();
        var dayCounted = new HashSet<(DateOnly, int)>();
        var firstHour = spaces.Count == 0 ? 9 : spaces.Min(s => s.Opens.Hour);
        var lastHour = spaces.Count == 0 ? 19 : spaces.Max(s => s.Closes.Minute > 0 ? s.Closes.Hour : s.Closes.Hour - 1);

        foreach (var space in spaces)
        {
            var spaceBookings = bookings.GetValueOrDefault(space.WorkspaceId, []);
            var spaceBlocks = blocks.GetValueOrDefault(space.WorkspaceId, []);
            for (var day = from; day <= to; day = day.AddDays(1))
            {
                if (!space.OpenDays.Contains(day.DayOfWeek))
                {
                    continue;
                }
                var openUtc = IstClock.ToUtc(day.ToDateTime(space.Opens));
                var closeUtc = IstClock.ToUtc(day.ToDateTime(space.Closes));
                if (closeUtc <= openUtc)
                {
                    continue;
                }
                var window = (decimal)(closeUtc - openUtc).TotalMinutes;
                var booked = Overlap(spaceBookings, openUtc, closeUtc);
                var blocked = Overlap(spaceBlocks, openUtc, closeUtc);
                days.Add(new OccupancyDay(space.WorkspaceId, day, booked / 60m, blocked / 60m,
                    Math.Max(0, window - blocked) / 60m));

                // The weekday × hour grid, one slot per opening hour.
                for (var slot = openUtc; slot < closeUtc; slot = slot.AddHours(1))
                {
                    var slotEnd = slot.AddHours(1) < closeUtc ? slot.AddHours(1) : closeUtc;
                    var hour = IstClock.ToIst(slot).Hour;
                    var slotBlocked = Overlap(spaceBlocks, slot, slotEnd);
                    var open = Math.Max(0, (decimal)(slotEnd - slot).TotalMinutes - slotBlocked);
                    var slotBooked = Overlap(spaceBookings, slot, slotEnd);
                    var key = (day.DayOfWeek, hour);
                    var cell = hours.GetValueOrDefault(key);
                    var newDay = dayCounted.Add((day, hour)) ? 1 : 0;
                    hours[key] = (cell.Booked + slotBooked, cell.Open + open,
                        cell.Seats + slotBooked * space.Capacity, cell.Days + newDay);
                }
            }
        }

        return new OccupancyResult
        {
            Spaces = spaces,
            Days = days,
            Hours = hours,
            FirstHour = firstHour,
            LastHour = lastHour,
            OpensSunday = spaces.Any(s => s.OpenDays.Contains(DayOfWeek.Sunday))
        };
    }

    /// <summary>Minutes of the spans that fall inside [start, end).</summary>
    private static decimal Overlap(List<(DateTime Start, DateTime End)> spans, DateTime start, DateTime end)
    {
        decimal minutes = 0;
        foreach (var (s, e) in spans)
        {
            var a = s > start ? s : start;
            var b = e < end ? e : end;
            if (b > a)
            {
                minutes += (decimal)(b - a).TotalMinutes;
            }
        }
        return minutes;
    }

    public static HashSet<DayOfWeek> ParseDays(string workingDays) =>
        workingDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => d.ToUpperInvariant() switch
            {
                "MON" => (DayOfWeek?)DayOfWeek.Monday,
                "TUE" => DayOfWeek.Tuesday,
                "WED" => DayOfWeek.Wednesday,
                "THU" => DayOfWeek.Thursday,
                "FRI" => DayOfWeek.Friday,
                "SAT" => DayOfWeek.Saturday,
                "SUN" => DayOfWeek.Sunday,
                _ => null
            })
            .Where(d => d is not null)
            .Select(d => d!.Value)
            .ToHashSet();

    /// <summary>Five steps, light to dark: 0–20, 20–45, 45–65, 65–80, 80+ (spec §3.6).</summary>
    public static int Step(decimal? percent) => percent switch
    {
        null => -1,
        < 20 => 0,
        < 45 => 1,
        < 65 => 2,
        < 80 => 3,
        _ => 4
    };

    /// <summary>The band a busiest-workspace bar takes its colour from.</summary>
    public static string Band(decimal percent) => percent switch
    {
        >= 80 => "high",
        >= 40 => "mid",
        _ => "low"
    };

    public static string WeekdayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Mon",
        DayOfWeek.Tuesday => "Tue",
        DayOfWeek.Wednesday => "Wed",
        DayOfWeek.Thursday => "Thu",
        DayOfWeek.Friday => "Fri",
        DayOfWeek.Saturday => "Sat",
        _ => "Sun"
    };

    public static string HourLabel(int hour) =>
        hour == 0 ? "12am" : hour < 12 ? $"{hour}am" : hour == 12 ? "12pm" : $"{hour - 12}pm";

    /// <summary>The weekday × hour heatmap, averaged across every matching weekday.</summary>
    public static ChartDtoBuilder.Heat Heatmap(OccupancyResult occupancy)
    {
        var weekdays = new List<DayOfWeek>
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday
        };
        if (occupancy.OpensSunday)
        {
            weekdays.Add(DayOfWeek.Sunday);
        }
        var hours = Enumerable.Range(occupancy.FirstHour, Math.Max(0, occupancy.LastHour - occupancy.FirstHour + 1)).ToList();
        var cells = new List<List<(decimal? Percent, decimal People)>>();
        (DayOfWeek Day, int Hour, decimal Percent)? quietest = null;
        (DayOfWeek Day, int Hour, decimal Percent)? busiest = null;
        foreach (var day in weekdays)
        {
            var row = new List<(decimal? Percent, decimal People)>();
            foreach (var hour in hours)
            {
                if (!occupancy.Hours.TryGetValue((day, hour), out var cell) || cell.Open <= 0)
                {
                    row.Add((null, 0));
                    continue;
                }
                var percent = Math.Round(cell.Booked / cell.Open * 100m, 1);
                var people = cell.Days == 0 ? 0 : Math.Round(cell.Seats / 60m / cell.Days, 1);
                row.Add((percent, people));
                if (quietest is null || percent < quietest.Value.Percent)
                {
                    quietest = (day, hour, percent);
                }
                if (busiest is null || percent > busiest.Value.Percent)
                {
                    busiest = (day, hour, percent);
                }
            }
            cells.Add(row);
        }
        return new ChartDtoBuilder.Heat(weekdays, hours, cells, quietest, busiest);
    }
}
