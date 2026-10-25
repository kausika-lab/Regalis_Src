using Microsoft.EntityFrameworkCore;
using Regalis.Application.Reporting.Analytics;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Reporting.Analytics;

/// <summary>What the Customers group shares.</summary>
internal static class CustomerFacts
{
    public static IQueryable<User> Customers(RegalisDbContext db) =>
        db.Users.AsNoTracking().Where(u => u.UserType == UserType.Customer);

    /// <summary>Lifetime spend, the revenue rule without refunds: payments for bookings,
    /// passes, services and invoices, plus the wallet when it is spent. Top-ups are not
    /// spend; wallet-method payments are skipped because their wallet debit counts.</summary>
    public static IQueryable<Domain.Entities.Finance.Payment> Spend(RegalisDbContext db) =>
        db.Payments.AsNoTracking().Where(p => p.Status == PaymentStatus.Success &&
                                              p.PaymentMethod != PaymentMethod.Wallet &&
                                              (p.BookingId != null || p.CustomerPassId != null ||
                                               p.ServiceOrderId != null || p.InvoiceId != null));

    public static IQueryable<Domain.Entities.Finance.WalletTransaction> WalletSpend(RegalisDbContext db) =>
        db.WalletTransactions.AsNoTracking().Where(t => t.Direction == WalletDirection.Debit &&
            (t.Category == WalletCategory.Booking || t.Category == WalletCategory.DayPass ||
             t.Category == WalletCategory.MonthlyPass || t.Category == WalletCategory.ServiceOrder));

    /// <summary>Lifetime spend of every customer in the filtered set, as one aggregate.</summary>
    public static async Task<decimal> SpendOfAsync(RegalisDbContext db, IQueryable<long> customerIds, CancellationToken ct) =>
        (await Spend(db).Where(p => customerIds.Contains(p.CustomerId)).SumAsync(p => (decimal?)p.Amount, ct) ?? 0m) +
        (await WalletSpend(db).Where(t => customerIds.Contains(t.CustomerId)).SumAsync(t => (decimal?)t.Amount, ct) ?? 0m);

    public static List<ReportOption> ProfileOptions => Filters.Options(("complete", "Complete"), ("incomplete", "Incomplete"));
}

internal sealed class CustomerRow
{
    public long Id { get; set; }
    public string Customer { get; set; } = "";
    public string Type { get; set; } = "";
    public string? Company { get; set; }
    public string? Gstin { get; set; }
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Profile { get; set; } = "";
    public DateTime Joined { get; set; }
    public decimal Spend { get; set; }
    public string Status { get; set; } = "";
}

/// <summary>The customer register. Regalis keeps no separate KYC record; a profile
/// is complete once the address, date of birth and gender are filled in (the same
/// test the onboarding gate uses).</summary>
internal sealed class CustomerListReport : ReportHandler<CustomerRow>
{
    public override string Key => "customer-list";
    public override string Name => "Customer list";
    public override string Description => "Every customer with type, company, GSTIN, profile (KYC) status, join date and lifetime spend.";
    public override string Group => "customers";
    public override string[] Keywords => ["customers", "members", "register", "gstin", "kyc", "contacts", "phone", "email"];
    public override string Permission => PermissionCodes.Report.Customers;
    public override string RowNoun => "customers";
    public override string DefaultRange => "all";
    public override string DefaultSort => "joined:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Select("type", "Customer type", Analytics.Filters.Options(("company", "Company"),
            ("individual", "Individual")), allLabel: "All types"),
        Analytics.Filters.Select("profile", "Profile (KYC)", CustomerFacts.ProfileOptions, allLabel: "Any")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(CustomerRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(CustomerRow.Type), "Type", "text"),
        new(nameof(CustomerRow.Company), "Company", "text", Searchable: true),
        new(nameof(CustomerRow.Gstin), "GSTIN", "text", Searchable: true),
        new(nameof(CustomerRow.Phone), "Phone", "text", Searchable: true),
        new(nameof(CustomerRow.Email), "Email", "text", Searchable: true),
        new(nameof(CustomerRow.Profile), "Profile (KYC)", "status"),
        new(nameof(CustomerRow.Joined), "Joined", "date"),
        new(nameof(CustomerRow.Spend), "Lifetime spend", "money", Total.Custom),
        new(nameof(CustomerRow.Status), "Status", "status", Visible: false)
    ];

    protected override string? LinkFor(CustomerRow row) => $"/admin/customers/{row.Id}";

    protected override Task<IQueryable<CustomerRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var spend = CustomerFacts.Spend(db);
        var walletSpend = CustomerFacts.WalletSpend(db);
        var rows = CustomerFacts.Customers(db)
            .Where(u => u.CreatedDate >= ctx.FromUtc && u.CreatedDate < ctx.ToUtc)
            .Select(u => new CustomerRow
            {
                Id = u.UserId,
                Customer = u.FullName,
                Type = u.CompanyName != null && u.CompanyName != "" ? "Company" : "Individual",
                Company = u.CompanyName,
                Gstin = u.Gstin,
                Phone = u.MobileNumber,
                Email = u.Email,
                Profile = u.AddressLine1 != null && u.City != null && u.PostalCode != null &&
                          u.DateOfBirth != null && u.Gender != null ? "Complete" : "Incomplete",
                Joined = u.CreatedDate,
                Spend = (spend.Where(p => p.CustomerId == u.UserId).Sum(p => (decimal?)p.Amount) ?? 0m) +
                        (walletSpend.Where(t => t.CustomerId == u.UserId).Sum(t => (decimal?)t.Amount) ?? 0m),
                Status = u.Status == UserStatus.Active ? "Active"
                    : u.Status == UserStatus.Locked ? "Locked"
                    : u.Status == UserStatus.PendingVerification ? "Not verified" : "Inactive"
            });
        if (ctx.One("type") is { } type)
        {
            var label = type == "company" ? "Company" : "Individual";
            rows = rows.Where(r => r.Type == label);
        }
        if (ctx.One("profile") is { } profile)
        {
            var label = profile == "complete" ? "Complete" : "Incomplete";
            rows = rows.Where(r => r.Profile == label);
        }
        return Task.FromResult(rows);
    }

    protected override async Task CustomTotalsAsync(ReportRunContext ctx, IQueryable<CustomerRow> rows,
        Dictionary<string, object?> totals, CancellationToken ct) =>
        totals["spend"] = await CustomerFacts.SpendOfAsync(ctx.Db, rows.Select(r => r.Id), ct);

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<CustomerRow> rows,
        CancellationToken ct)
    {
        var count = await CountAsync(rows, ct);
        var companies = await rows.CountAsync(r => r.Type == "Company", ct);
        var complete = await rows.CountAsync(r => r.Profile == "Complete", ct);
        return
        [
            new("customers", "Customers", count, "count", Compare: false),
            new("companies", "Companies", companies, "count", Compare: false,
                Sub: $"{ReportText.Plural(count - companies, "individuals")}"),
            new("complete", "Profiles complete", count == 0 ? null : Math.Round(complete * 100m / count, 1), "percent",
                Compare: false, Sub: $"{ReportText.Plural(complete, "customers")}"),
            new("spend", "Lifetime spend", await CustomerFacts.SpendOfAsync(ctx.Db, rows.Select(r => r.Id), ct),
                "money", Compare: false)
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<CustomerRow> rows,
        CancellationToken ct)
    {
        var joined = await rows.Select(r => r.Joined).ToListAsync(ct);
        if (joined.Count == 0)
        {
            return [];
        }
        // From the month of the first sign-up in range, so an all-time list does not
        // open on years of empty months.
        var first = IstClock.IstDateOf(joined.Min());
        var firstMonth = new DateOnly(first.Year, first.Month, 1);
        var fromMonth = new DateOnly(ctx.From.Year, ctx.From.Month, 1);
        var start = firstMonth > fromMonth ? firstMonth : fromMonth;
        var byMonth = joined.GroupBy(j => { var d = IstClock.IstDateOf(j); return new DateOnly(d.Year, d.Month, 1); })
            .ToDictionary(g => g.Key, g => g.Count());
        var points = new List<ChartPointDto>();
        for (var month = start; month <= ctx.To; month = month.AddMonths(1))
        {
            points.Add(new ChartPointDto(ChartDtoBuilder.MonthLabel(month), month.ToString("yyyy-MM-dd"),
                byMonth.GetValueOrDefault(month)));
        }
        var companies = joined.Count == 0 ? 0 : await rows.CountAsync(r => r.Type == "Company", ct);
        var types = ChartDtoBuilder.Slices([("Company", companies, 1), ("Individual", joined.Count - companies, 2)]);
        return
        [
            new ChartDto
            {
                Id = "joined", Kind = "columns", Title = "Customers joining each month", ValueFormat = "count",
                Points = points, HighlightIndex = ChartDtoBuilder.Highest(points)
            },
            new ChartDto
            {
                Id = "types", Kind = "doughnut", Title = "Companies and individuals", ValueFormat = "count",
                Slices = types, Total = joined.Count
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        ctx.From <= ReportFilterParser.AllTimeStart
            ? $"Every customer who joined up to {ReportMath.Day(ctx.To)} — {ReportText.Plural(count, "customers")}, {ReportMath.Money(ReportText.Num(totals, "spend"))} spent in all."
            : $"Every customer who joined between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "customers")}, {ReportMath.Money(ReportText.Num(totals, "spend"))} spent in all.";
}

internal sealed class TopCustomerRow
{
    public long Id { get; set; }
    public int Rank { get; set; }
    public string Customer { get; set; } = "";
    public string? Company { get; set; }
    public decimal Revenue { get; set; }
    public int Bookings { get; set; }
    public decimal? Average { get; set; }
    public decimal? Share { get; set; }
}

internal sealed class TopCustomersReport : ReportHandler<TopCustomerRow>
{
    public override string Key => "top-customers";
    public override string Name => "Top customers by spend";
    public override string Description => "Customers ranked by revenue in the period, with bookings and average booking value.";
    public override string Group => "customers";
    public override string[] Keywords => ["top", "customers", "biggest", "spend", "revenue", "ranking", "best"];
    public override string Permission => PermissionCodes.Report.Customers;
    public override string RowNoun => "customers";
    public override string DefaultSort => "rank:asc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
        [Analytics.Filters.Dates(), Analytics.Filters.Location(lookups)];

    protected override List<Col> Cols =>
    [
        new(nameof(TopCustomerRow.Rank), "Rank", "number"),
        new(nameof(TopCustomerRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(TopCustomerRow.Company), "Company", "text", Searchable: true),
        new(nameof(TopCustomerRow.Revenue), "Revenue", "money", Total.Sum),
        new(nameof(TopCustomerRow.Bookings), "Bookings", "number", Total.Sum),
        new(nameof(TopCustomerRow.Average), "Average booking", "money", Total.Custom),
        new(nameof(TopCustomerRow.Share), "Share of revenue", "percent", Total.Custom)
    ];

    protected override string? LinkFor(TopCustomerRow row) => $"/admin/customers/{row.Id}";

    /// <summary>Revenue and bookings per customer — shared with the overview's Top customers.</summary>
    internal static async Task<List<TopCustomerRow>> RankAsync(RegalisDbContext db, DateOnly from, DateOnly to,
        int? locationId, CancellationToken ct)
    {
        var revenue = await RevenueFacts.ByCustomerAsync(db, from, to, locationId, ct);
        var bookingsQuery = BookingRules.ExcludeInternal(db.Bookings.AsNoTracking())
            .Where(b => BookingRules.Kept.Contains(b.Status) && b.BookingDate >= from && b.BookingDate <= to);
        if (locationId is { } location)
        {
            bookingsQuery = bookingsQuery.Where(b => b.LocationId == location);
        }
        var bookings = await bookingsQuery.GroupBy(b => b.CustomerId)
            .Select(g => new { CustomerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.CustomerId, g => g.Count, ct);
        var ids = revenue.Keys.Union(bookings.Keys).ToList();
        var people = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.UserId) && u.UserType == UserType.Customer)
            .Select(u => new { u.UserId, u.FullName, u.CompanyName })
            .ToListAsync(ct);
        var ranked = people
            .Select(p => new TopCustomerRow
            {
                Id = p.UserId,
                Customer = p.FullName,
                Company = p.CompanyName,
                Revenue = revenue.GetValueOrDefault(p.UserId),
                Bookings = bookings.GetValueOrDefault(p.UserId)
            })
            .Where(r => r.Revenue != 0 || r.Bookings > 0)
            .OrderByDescending(r => r.Revenue).ThenByDescending(r => r.Bookings).ThenBy(r => r.Customer)
            .ToList();
        var shares = ReportMath.SharesTo100(ranked.Select(r => Math.Max(0, r.Revenue)).ToList());
        for (var i = 0; i < ranked.Count; i++)
        {
            ranked[i].Rank = i + 1;
            ranked[i].Share = shares[i];
            ranked[i].Average = ranked[i].Bookings == 0 ? null : ranked[i].Revenue / ranked[i].Bookings;
        }
        return ranked;
    }

    protected override async Task<IQueryable<TopCustomerRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct) =>
        FromList(await RankAsync(ctx.Db, ctx.From, ctx.To, ctx.LocationId, ct));

    protected override Task CustomTotalsAsync(ReportRunContext ctx, IQueryable<TopCustomerRow> rows,
        Dictionary<string, object?> totals, CancellationToken ct)
    {
        var bookings = ReportText.Num(totals, "bookings");
        totals["average"] = bookings == 0 ? null : ReportText.Num(totals, "revenue") / bookings;
        totals["share"] = rows.Any() ? 100m : 0m;
        return Task.CompletedTask;
    }

    protected override Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<TopCustomerRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        var revenue = list.Sum(r => r.Revenue);
        var top5 = list.Take(5).Sum(r => r.Revenue);
        return Task.FromResult(new List<Figure>
        {
            new("top5", "Top five's share", revenue <= 0 ? null : Math.Round(top5 * 100m / revenue, 1), "percent",
                Compare: false, Sub: revenue <= 0 ? null : $"{ReportMath.Money(top5)} of {ReportMath.Money(revenue)}"),
            new("customers", "Customers who spent", list.Count(r => r.Revenue > 0), "count"),
            new("revenue", "Revenue", revenue, "money"),
            new("average", "Average per customer", list.Count == 0 ? null : revenue / list.Count, "money")
        });
    }

    protected override Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<TopCustomerRow> rows,
        CancellationToken ct)
    {
        var top = rows.Take(10).ToList();
        if (top.Count == 0)
        {
            return Task.FromResult(new List<ChartDto>());
        }
        return Task.FromResult(new List<ChartDto>
        {
            new()
            {
                Id = "top", Kind = "hbars", Title = "The ten biggest customers", ValueFormat = "money",
                Slices = top.Select(r => new ChartSliceDto(r.Customer, r.Revenue, r.Share ?? 0, 1,
                    ReportText.Plural(r.Bookings, "bookings"))).ToList(),
                Total = rows.Sum(r => r.Revenue)
            }
        });
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Customers ranked by what they paid between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "customers")}, {ReportMath.Money(ReportText.Num(totals, "revenue"))} in all.";
}

internal sealed class SignupRow
{
    public long Id { get; set; }
    public string Customer { get; set; } = "";
    public string? Company { get; set; }
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public DateTime Joined { get; set; }
    public string Source { get; set; } = "";
    public string Booked { get; set; } = "";
    public DateOnly? FirstBooking { get; set; }
    public string Profile { get; set; } = "";
}

internal sealed class NewSignupsReport : ReportHandler<SignupRow>
{
    public override string Key => "new-signups";
    public override string Name => "New sign-ups";
    public override string Description => "Customers who registered in the period, with the source and whether they have booked yet.";
    public override string Group => "customers";
    public override string[] Keywords => ["new", "sign-ups", "signups", "registrations", "joined", "leads"];
    public override string Permission => PermissionCodes.Report.Customers;
    public override string RowNoun => "sign-ups";
    public override string DefaultSort => "joined:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Select("source", "Source", Analytics.Filters.Options(("google", "Google sign-in"),
            ("email", "Email sign-up"), ("staff", "Added by staff")), allLabel: "Any source"),
        Analytics.Filters.Select("booked", "Booked yet", Analytics.Filters.Options(("yes", "Has booked"),
            ("no", "Not yet")), allLabel: "Either")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(SignupRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(SignupRow.Company), "Company", "text", Searchable: true),
        new(nameof(SignupRow.Phone), "Phone", "text", Searchable: true),
        new(nameof(SignupRow.Email), "Email", "text", Searchable: true),
        new(nameof(SignupRow.Joined), "Joined", "datetime"),
        new(nameof(SignupRow.Source), "Source", "text"),
        new(nameof(SignupRow.Booked), "Booked yet", "status"),
        new(nameof(SignupRow.FirstBooking), "First booking", "date"),
        new(nameof(SignupRow.Profile), "Profile (KYC)", "status", Visible: false)
    ];

    protected override string? LinkFor(SignupRow row) => $"/admin/customers/{row.Id}";

    protected override Task<IQueryable<SignupRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var kept = BookingRules.Kept;
        var rows = CustomerFacts.Customers(db)
            .Where(u => u.CreatedDate >= ctx.FromUtc && u.CreatedDate < ctx.ToUtc)
            .Select(u => new SignupRow
            {
                Id = u.UserId,
                Customer = u.FullName,
                Company = u.CompanyName,
                Phone = u.MobileNumber,
                Email = u.Email,
                Joined = u.CreatedDate,
                Source = u.GoogleId != null ? "Google sign-in"
                    : u.CreatedBy != null && u.CreatedBy != u.UserId ? "Added by staff" : "Email sign-up",
                Booked = db.Bookings.Any(b => b.CustomerId == u.UserId && kept.Contains(b.Status)) ? "Yes" : "Not yet",
                FirstBooking = db.Bookings.Where(b => b.CustomerId == u.UserId && kept.Contains(b.Status))
                    .Min(b => (DateOnly?)b.BookingDate),
                Profile = u.AddressLine1 != null && u.City != null && u.PostalCode != null &&
                          u.DateOfBirth != null && u.Gender != null ? "Complete" : "Incomplete"
            });
        if (ctx.One("source") is { } source)
        {
            var label = source switch { "google" => "Google sign-in", "staff" => "Added by staff", _ => "Email sign-up" };
            rows = rows.Where(r => r.Source == label);
        }
        if (ctx.One("booked") is { } booked)
        {
            var label = booked == "yes" ? "Yes" : "Not yet";
            rows = rows.Where(r => r.Booked == label);
        }
        return Task.FromResult(rows);
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<SignupRow> rows,
        CancellationToken ct)
    {
        var count = await CountAsync(rows, ct);
        var booked = await rows.CountAsync(r => r.Booked == "Yes", ct);
        return
        [
            new("signups", "Sign-ups", count, "count"),
            new("booked", "Booked already", count == 0 ? null : Math.Round(booked * 100m / count, 1), "percent",
                Compare: false, Sub: ReportText.Plural(booked, "customers")),
            new("google", "Through Google", await rows.CountAsync(r => r.Source == "Google sign-in", ct), "count"),
            new("staff", "Added by staff", await rows.CountAsync(r => r.Source == "Added by staff", ct), "count")
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<SignupRow> rows,
        CancellationToken ct)
    {
        var joined = await rows.Select(r => r.Joined).ToListAsync(ct);
        var byDay = joined.GroupBy(IstClock.IstDateOf).ToDictionary(g => g.Key, g => ((decimal)g.Count(), 0m));
        var points = ChartDtoBuilder.DailyPoints(ctx.From, ctx.To, byDay);
        return
        [
            new ChartDto
            {
                Id = "per-day", Kind = "columns", Title = "Sign-ups each day", ValueFormat = "count", Points = points,
                HighlightIndex = ChartDtoBuilder.Highest(points)
            }
        ];
    }

    public override async Task<string?> LiveFigureAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var monthStart = new DateOnly(ctx.Today.Year, ctx.Today.Month, 1);
        var count = await CustomerFacts.Customers(ctx.Db)
            .CountAsync(u => u.CreatedDate >= ReportMath.IstStartUtc(monthStart), ct);
        return $"{ReportText.Plural(count, "sign-ups")} this month";
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Customers who registered between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "sign-ups")}.";
}

internal sealed class QuietRow
{
    public long Id { get; set; }
    public string Customer { get; set; } = "";
    public string? Company { get; set; }
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public DateOnly LastBooking { get; set; }
    public int DaysSince { get; set; }
    public int BookingsEver { get; set; }
    public decimal Spend { get; set; }
}

/// <summary>Customers who booked before but have nothing — past or upcoming — within the
/// quiet window before the end of the range.</summary>
internal sealed class GoneQuietReport : ReportHandler<QuietRow>
{
    public override string Key => "gone-quiet";
    public override string Name => "Gone quiet";
    public override string Description => "Customers with no booking in the last 60 days who had booked before, with the date they last came in.";
    public override string Group => "customers";
    public override string[] Keywords => ["quiet", "inactive", "lapsed", "churn", "lost", "win back", "dormant"];
    public override string Permission => PermissionCodes.Report.Customers;
    public override string RowNoun => "customers";
    public override string DefaultSort => "daysSince:asc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Select("quiet", "No booking for", Analytics.Filters.Options(("30", "30 days"),
            ("60", "60 days"), ("90", "90 days"), ("180", "180 days")), "60")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(QuietRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(QuietRow.Company), "Company", "text", Searchable: true),
        new(nameof(QuietRow.Phone), "Phone", "text", Searchable: true),
        new(nameof(QuietRow.Email), "Email", "text", Searchable: true),
        new(nameof(QuietRow.LastBooking), "Last came in", "date"),
        new(nameof(QuietRow.DaysSince), "Days since", "number"),
        new(nameof(QuietRow.BookingsEver), "Bookings ever", "number", Total.Custom),
        new(nameof(QuietRow.Spend), "Lifetime spend", "money", Total.Custom)
    ];

    protected override string? LinkFor(QuietRow row) => $"/admin/customers/{row.Id}";

    private int QuietDays(ReportRunContext ctx) => ctx.Int("quiet") is { } d and > 0 ? d : 60;

    protected override Task<IQueryable<QuietRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var kept = BookingRules.Kept;
        var asOf = ctx.To;
        var cutoff = asOf.AddDays(-QuietDays(ctx));
        var spend = CustomerFacts.Spend(db);
        var walletSpend = CustomerFacts.WalletSpend(db);
        return Task.FromResult(
            from u in CustomerFacts.Customers(db)
            let last = db.Bookings.Where(b => b.CustomerId == u.UserId && kept.Contains(b.Status))
                .Max(b => (DateOnly?)b.BookingDate)
            where last != null && last < cutoff
            select new QuietRow
            {
                Id = u.UserId,
                Customer = u.FullName,
                Company = u.CompanyName,
                Phone = u.MobileNumber,
                Email = u.Email,
                LastBooking = last!.Value,
                DaysSince = EF.Functions.DateDiffDay(last.Value, asOf),
                BookingsEver = db.Bookings.Count(b => b.CustomerId == u.UserId && kept.Contains(b.Status)),
                Spend = (spend.Where(p => p.CustomerId == u.UserId).Sum(p => (decimal?)p.Amount) ?? 0m) +
                        (walletSpend.Where(t => t.CustomerId == u.UserId).Sum(t => (decimal?)t.Amount) ?? 0m)
            });
    }

    protected override async Task CustomTotalsAsync(ReportRunContext ctx, IQueryable<QuietRow> rows,
        Dictionary<string, object?> totals, CancellationToken ct)
    {
        var ids = rows.Select(r => r.Id);
        totals["spend"] = await CustomerFacts.SpendOfAsync(ctx.Db, ids, ct);
        totals["bookingsEver"] = await ctx.Db.Bookings.AsNoTracking()
            .CountAsync(b => ids.Contains(b.CustomerId) && BookingRules.Kept.Contains(b.Status), ct);
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<QuietRow> rows,
        CancellationToken ct)
    {
        var count = await CountAsync(rows, ct);
        var average = count == 0 ? (double?)null : await rows.AverageAsync(r => (double)r.DaysSince, ct);
        return
        [
            new("quiet", "Gone quiet", count, "count", UpIsGood: false, Compare: false,
                Sub: $"No booking for {QuietDays(ctx)} days or more"),
            new("spend", "What they spent", await CustomerFacts.SpendOfAsync(ctx.Db, rows.Select(r => r.Id), ct),
                "money", Compare: false, Sub: "Lifetime, before going quiet"),
            new("days", "Average days since", average is null ? null : (decimal)Math.Round(average.Value), "count",
                Compare: false)
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<QuietRow> rows,
        CancellationToken ct)
    {
        var bands = await rows.GroupBy(r => r.DaysSince <= 90 ? 1 : r.DaysSince <= 180 ? 2 : r.DaysSince <= 365 ? 3 : 4)
            .Select(g => new { Band = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        string[] labels = ["Up to 3 months", "3–6 months", "6–12 months", "Over a year"];
        var slices = ChartDtoBuilder.Slices(labels.Select((l, i) =>
            (l, (decimal)(bands.FirstOrDefault(b => b.Band == i + 1)?.Count ?? 0), i + 1)));
        return
        [
            new ChartDto
            {
                Id = "since", Kind = "hbars", Title = "How long since they came in", ValueFormat = "count",
                Slices = slices, Total = slices.Sum(s => s.Value)
            }
        ];
    }

    public override async Task<string?> LiveFigureAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var count = await CountAsync(await QueryAsync(ctx.WithRange(ctx.Today, ctx.Today), ct), ct);
        return ReportText.Plural(count, "customers");
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Customers who booked before but not since {ReportMath.Day(ctx.To.AddDays(-QuietDays(ctx)))} — {ReportText.Plural(count, "customers")}.";
}

internal sealed class WalletRow
{
    public long Id { get; set; }
    public string Customer { get; set; } = "";
    public string Phone { get; set; } = "";
    public decimal Balance { get; set; }
    public DateTime? LastTopUp { get; set; }
    public DateTime? LastSpend { get; set; }
    public decimal TopUpsInPeriod { get; set; }
}

/// <summary>Money Regalis is still holding, summed from the wallet ledger as of the end
/// of the range — never read from the stored balance.</summary>
internal sealed class WalletBalancesReport : ReportHandler<WalletRow>
{
    public override string Key => "wallet-balances";
    public override string Name => "Wallet balances";
    public override string Description => "Money Regalis is still holding: balance per customer, last top-up and last spend.";
    public override string Group => "customers";
    public override string[] Keywords => ["wallet", "balance", "held", "prepaid", "top-up", "credit"];
    public override string Permission => PermissionCodes.Report.Customers;
    public override string RowNoun => "wallets";
    public override string DefaultSort => "balance:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Toggle("nonzero", "Only wallets with money in them", on: true),
        Analytics.Filters.Customer()
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(WalletRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(WalletRow.Phone), "Phone", "text", Searchable: true),
        new(nameof(WalletRow.Balance), "Balance", "money", Total.Sum),
        new(nameof(WalletRow.LastTopUp), "Last top-up", "datetime"),
        new(nameof(WalletRow.LastSpend), "Last spend", "datetime"),
        new(nameof(WalletRow.TopUpsInPeriod), "Topped up in period", "money", Total.Sum, Visible: false)
    ];

    protected override string? LinkFor(WalletRow row) => $"/admin/customers/{row.Id}";

    protected override async Task<IQueryable<WalletRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var fromUtc = ctx.FromUtc;
        var ledger = db.WalletTransactions.AsNoTracking().Where(t => t.TransactionUtc < ctx.ToUtc);
        if (ctx.CustomerId is { } customer)
        {
            ledger = ledger.Where(t => t.CustomerId == customer);
        }
        var grouped = ledger.GroupBy(t => t.CustomerId).Select(g => new
        {
            CustomerId = g.Key,
            Balance = g.Sum(t => t.Direction == WalletDirection.Credit ? t.Amount : -t.Amount),
            LastTopUp = g.Where(t => t.Category == WalletCategory.Recharge && t.Direction == WalletDirection.Credit)
                .Max(t => (DateTime?)t.TransactionUtc),
            LastSpend = g.Where(t => t.Direction == WalletDirection.Debit).Max(t => (DateTime?)t.TransactionUtc),
            TopUps = g.Where(t => t.Category == WalletCategory.Recharge && t.Direction == WalletDirection.Credit &&
                                  t.TransactionUtc >= fromUtc).Sum(t => (decimal?)t.Amount) ?? 0m
        });
        if (ctx.Flag("nonzero"))
        {
            grouped = grouped.Where(g => g.Balance != 0);
        }
        var rows = await (from g in grouped
            from u in db.Users.Where(u => u.UserId == g.CustomerId)
            select new WalletRow
            {
                Id = g.CustomerId,
                Customer = u.FullName,
                Phone = u.MobileNumber,
                Balance = g.Balance,
                LastTopUp = g.LastTopUp,
                LastSpend = g.LastSpend,
                TopUpsInPeriod = g.TopUps
            }).ToListAsync(ct);
        return FromList(rows);
    }

    protected override Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<WalletRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        return Task.FromResult(new List<Figure>
        {
            new("held", "Money held", list.Sum(r => r.Balance), "money", Compare: false,
                Sub: $"As of {ReportMath.Day(ctx.To)}"),
            new("wallets", "Wallets in credit", list.Count(r => r.Balance > 0), "count", Compare: false),
            new("largest", "Largest balance", list.Count == 0 ? null : list.Max(r => r.Balance), "money", Compare: false),
            new("topups", "Topped up in period", list.Sum(r => r.TopUpsInPeriod), "money", Compare: false)
        });
    }

    protected override Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<WalletRow> rows,
        CancellationToken ct)
    {
        var list = rows.Where(r => r.Balance > 0).OrderByDescending(r => r.Balance).ToList();
        if (list.Count == 0)
        {
            return Task.FromResult(new List<ChartDto>());
        }
        var total = list.Sum(r => r.Balance);
        var top = list.Take(10).ToList();
        var shares = ReportMath.SharesTo100(list.Select(r => r.Balance).ToList());
        return Task.FromResult(new List<ChartDto>
        {
            new()
            {
                Id = "largest", Kind = "hbars", Title = "The largest balances", ValueFormat = "money",
                Slices = top.Select((r, i) => new ChartSliceDto(r.Customer, r.Balance, shares[i], 1)).ToList(),
                Total = total
            }
        });
    }

    public override async Task<string?> LiveFigureAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var held = await ctx.Db.WalletTransactions.AsNoTracking()
            .SumAsync(t => (decimal?)(t.Direction == WalletDirection.Credit ? t.Amount : -t.Amount), ct) ?? 0m;
        return $"{ReportMath.Money(held)} held";
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Wallet balances as of {ReportMath.Day(ctx.To)} — {ReportMath.Money(ReportText.Num(totals, "balance"))} held across {ReportText.Plural(count, "wallets")}.";
}
