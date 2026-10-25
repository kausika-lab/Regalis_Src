using Microsoft.EntityFrameworkCore;
using Regalis.Application.Reporting.Analytics;
using Regalis.Domain.Enums;
using Regalis.Shared;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Reporting.Analytics;

internal sealed class RevenueMonthRow
{
    public long Id { get; set; }
    public DateOnly Month { get; set; }
    public decimal Received { get; set; }
    public decimal Refunds { get; set; }
    public decimal Revenue { get; set; }
    public decimal LastYear { get; set; }
    public decimal? Change { get; set; }
}

/// <summary>Revenue (money received, net of refunds) per month, beside the same months a year earlier.</summary>
internal sealed class RevenueByMonthReport : ReportHandler<RevenueMonthRow>
{
    public override string Key => "revenue-by-month";
    public override string Name => "Revenue by month";
    public override string Description => "Revenue per month for the selected range, with the same months last year beside them.";
    public override string Group => "revenue";
    public override string[] Keywords => ["revenue", "money", "income", "monthly", "last year", "trend"];
    public override string Permission => PermissionCodes.Report.Finance;
    public override string RowNoun => "months";
    public override string DefaultSort => "month:asc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
        [Analytics.Filters.Dates(), Analytics.Filters.Location(lookups)];

    protected override List<Col> Cols =>
    [
        new(nameof(RevenueMonthRow.Month), "Month", "month"),
        new(nameof(RevenueMonthRow.Received), "Payments received", "money", Total.Sum),
        new(nameof(RevenueMonthRow.Refunds), "Refunds", "money", Total.Sum),
        new(nameof(RevenueMonthRow.Revenue), "Revenue", "money", Total.Sum),
        new(nameof(RevenueMonthRow.LastYear), "Same month last year", "money", Total.Sum),
        new(nameof(RevenueMonthRow.Change), "Change", "percent", Total.Custom)
    ];

    private static async Task<List<RevenueMonthRow>> BuildAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var now = await RevenueFacts.ByDayAsync(ctx.Db, ctx.From, ctx.To, ctx.LocationId, null, ct);
        var before = await RevenueFacts.ByDayAsync(ctx.Db, ctx.From.AddYears(-1), ctx.To.AddYears(-1),
            ctx.LocationId, null, ct);
        var rows = new List<RevenueMonthRow>();
        for (var month = new DateOnly(ctx.From.Year, ctx.From.Month, 1); month <= ctx.To; month = month.AddMonths(1))
        {
            var end = month.AddMonths(1).AddDays(-1);
            var inMonth = now.Where(r => r.Day >= month && r.Day <= end).ToList();
            var lastYear = before.Where(r => r.Day >= month.AddYears(-1) && r.Day <= end.AddYears(-1)).Sum(r => r.Amount);
            var revenue = inMonth.Sum(r => r.Amount);
            rows.Add(new RevenueMonthRow
            {
                Id = rows.Count + 1,
                Month = month,
                Received = inMonth.Where(r => r.Amount > 0).Sum(r => r.Amount),
                Refunds = inMonth.Where(r => r.Amount < 0).Sum(r => r.Amount),
                Revenue = revenue,
                LastYear = lastYear,
                Change = ReportMath.Compare(revenue, lastYear) is ("percent", var change) ? change : null
            });
        }
        return rows;
    }

    protected override async Task<IQueryable<RevenueMonthRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct) =>
        FromList(await BuildAsync(ctx, ct));

    protected override Task CustomTotalsAsync(ReportRunContext ctx, IQueryable<RevenueMonthRow> rows,
        Dictionary<string, object?> totals, CancellationToken ct)
    {
        totals["change"] = ReportMath.Compare(ReportText.Num(totals, "revenue"), ReportText.Num(totals, "lastYear"))
            is ("percent", var change) ? change : null;
        return Task.CompletedTask;
    }

    protected override Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<RevenueMonthRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        var revenue = list.Sum(r => r.Revenue);
        var lastYear = list.Sum(r => r.LastYear);
        var best = list.OrderByDescending(r => r.Revenue).FirstOrDefault();
        var (kind, change) = ReportMath.Compare(revenue, lastYear);
        return Task.FromResult(new List<Figure>
        {
            new("revenue", "Revenue", revenue, "money", Sub: "Money received, net of refunds"),
            new("best", "Best month", best?.Revenue, "money", Compare: false,
                Sub: best is { Revenue: > 0 } ? ChartDtoBuilder.MonthLabel(best.Month) : null),
            new("average", "Average per month", list.Count == 0 ? null : revenue / list.Count, "money"),
            new("lastYear", "Same months last year", lastYear, "money", Compare: false,
                Sub: kind == "percent" ? $"{(change >= 0 ? "Up" : "Down")} {Math.Abs(change!.Value):0.0}% on last year"
                    : "No comparison")
        });
    }

    protected override Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<RevenueMonthRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        return Task.FromResult(new List<ChartDto>
        {
            new()
            {
                Id = "months", Kind = "line", Title = "Revenue each month",
                Subtitle = "The dashed line is the same month last year",
                ValueFormat = "money",
                Points = list.Select(r => new ChartPointDto(ChartDtoBuilder.MonthLabel(r.Month),
                    r.Month.ToString("yyyy-MM-dd"), r.Revenue)).ToList(),
                Previous = list.Select(r => new ChartPointDto(ChartDtoBuilder.MonthLabel(r.Month.AddYears(-1)),
                    r.Month.AddYears(-1).ToString("yyyy-MM-dd"), r.LastYear)).ToList()
            }
        });
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Revenue between {ReportMath.Range(ctx.From, ctx.To)} — {ReportMath.Money(ReportText.Num(totals, "revenue"))} over {ReportText.Plural(count, "months")}.";
}

internal sealed class RevenueCategoryRow
{
    public long Id { get; set; }
    public string Category { get; set; } = "";
    public int Slot { get; set; }
    public decimal Revenue { get; set; }
    public decimal? Share { get; set; }
    public decimal Previous { get; set; }
    public decimal? Change { get; set; }
}

internal sealed class RevenueByCategoryReport : ReportHandler<RevenueCategoryRow>
{
    public override string Key => "revenue-by-category";
    public override string Name => "Revenue by category";
    public override string Description => "Revenue split by cabins, desks, meeting rooms, passes and services.";
    public override string Group => "revenue";
    public override string[] Keywords => ["revenue", "category", "split", "desks", "cabins", "passes", "services"];
    public override string Permission => PermissionCodes.Report.Finance;
    public override string RowNoun => "categories";
    public override string DefaultSort => "revenue:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
        [Analytics.Filters.Dates(), Analytics.Filters.Location(lookups)];

    protected override List<Col> Cols =>
    [
        new(nameof(RevenueCategoryRow.Category), "Category", "text"),
        new(nameof(RevenueCategoryRow.Revenue), "Revenue", "money", Total.Sum),
        new(nameof(RevenueCategoryRow.Share), "Share", "percent", Total.Custom),
        new(nameof(RevenueCategoryRow.Previous), "Previous period", "money", Total.Sum),
        new(nameof(RevenueCategoryRow.Change), "Change", "percent", Total.Custom)
    ];

    internal static List<(string Category, decimal Amount)> ByCategory(ReportCategories categories,
        IEnumerable<RevenueDayRow> rows) =>
        rows.GroupBy(r => categories.Of(r.Kind, r.WorkspaceTypeId))
            .Select(g => (g.Key, g.Sum(r => r.Amount))).ToList();

    protected override async Task<IQueryable<RevenueCategoryRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var (prevFrom, prevTo) = ReportMath.PreviousPeriod(ctx.From, ctx.To);
        var now = ByCategory(ctx.Categories, await RevenueFacts.ByDayAsync(ctx.Db, ctx.From, ctx.To, ctx.LocationId, null, ct));
        var before = ByCategory(ctx.Categories, await RevenueFacts.ByDayAsync(ctx.Db, prevFrom, prevTo, ctx.LocationId, null, ct));
        var names = ctx.Categories.Ordered.Concat([ReportCategories.Other])
            .Where(n => n != ReportCategories.Other || now.Any(x => x.Category == n) || before.Any(x => x.Category == n))
            .ToList();
        var revenue = names.Select(n => now.Where(x => x.Category == n).Sum(x => x.Amount)).ToList();
        var shares = ReportMath.SharesTo100(revenue);
        return FromList(names.Select((name, i) =>
        {
            var previous = before.Where(x => x.Category == name).Sum(x => x.Amount);
            return new RevenueCategoryRow
            {
                Id = i + 1,
                Category = name,
                Slot = ctx.Categories.SlotOf(name),
                Revenue = revenue[i],
                Share = shares[i],
                Previous = previous,
                Change = ReportMath.Compare(revenue[i], previous) is ("percent", var c) ? c : null
            };
        }));
    }

    protected override Task CustomTotalsAsync(ReportRunContext ctx, IQueryable<RevenueCategoryRow> rows,
        Dictionary<string, object?> totals, CancellationToken ct)
    {
        totals["share"] = ReportText.Num(totals, "revenue") > 0 ? 100m : 0m;
        totals["change"] = ReportMath.Compare(ReportText.Num(totals, "revenue"), ReportText.Num(totals, "previous"))
            is ("percent", var c) ? c : null;
        return Task.CompletedTask;
    }

    /// <summary>"Meeting Room grew the most: up ₹12,400 (18.2%) on the previous 30 days."</summary>
    internal static string? BiggestMover(IEnumerable<(string Category, decimal Now, decimal Before)> rows, int days)
    {
        var mover = rows.Where(r => r.Now != r.Before)
            .OrderByDescending(r => Math.Abs(r.Now - r.Before)).FirstOrDefault();
        if (mover.Category is null)
        {
            return null;
        }
        var delta = mover.Now - mover.Before;
        var (kind, change) = ReportMath.Compare(mover.Now, mover.Before);
        var percent = kind == "percent" ? $" ({Math.Abs(change!.Value):0.0}%)" : "";
        return delta > 0
            ? $"Biggest mover: {mover.Category}, up {ReportMath.Money(delta)}{percent} on the previous {days} days."
            : $"Biggest mover: {mover.Category}, down {ReportMath.Money(-delta)}{percent} on the previous {days} days.";
    }

    protected override Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<RevenueCategoryRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        var top = list.OrderByDescending(r => r.Revenue).FirstOrDefault();
        return Task.FromResult(new List<Figure>
        {
            new("revenue", "Revenue", list.Sum(r => r.Revenue), "money"),
            new("top", "Largest category", top?.Revenue, "money", Compare: false,
                Sub: top is { Revenue: > 0 } ? $"{top.Category}, {ReportMath.Percent(top.Share)} of revenue" : null),
            new("mover", "Biggest mover", null, "money", Compare: false,
                Sub: BiggestMover(list.Select(r => (r.Category, r.Revenue, r.Previous)), ctx.Days) ?? "No change")
        });
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<RevenueCategoryRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        var days = await RevenueFacts.ByDayAsync(ctx.Db, ctx.From, ctx.To, ctx.LocationId, null, ct);
        var series = list.Where(r => r.Slot > 0).Select(r => new ChartSeriesDto(r.Category, r.Slot)).ToList();
        var hasOther = list.Any(r => r.Slot == 0 && r.Revenue != 0);
        if (hasOther)
        {
            series.Add(new ChartSeriesDto(ReportCategories.Other, 0));
        }
        var (buckets, byWeek) = ChartDtoBuilder.Buckets(ctx.From, ctx.To);
        var stacks = buckets.Select(bucket =>
        {
            var inBucket = days.Where(d => d.Day >= bucket.Start && d.Day <= bucket.End).ToList();
            var values = series.Select(s => s.Slot == 0
                ? inBucket.Where(d => ctx.Categories.SlotOf(ctx.Categories.Of(d.Kind, d.WorkspaceTypeId)) == 0).Sum(d => d.Amount)
                : inBucket.Where(d => ctx.Categories.Of(d.Kind, d.WorkspaceTypeId) == s.Name).Sum(d => d.Amount)).ToList();
            return new ChartStackDto(bucket.Label, bucket.Start.ToString("yyyy-MM-dd"), values, values.Sum());
        }).ToList();
        return
        [
            new ChartDto
            {
                Id = "share", Kind = "hbars", Title = "Where the money came from", ValueFormat = "money",
                Slices = list.OrderByDescending(r => r.Revenue)
                    .Select(r => new ChartSliceDto(r.Category, r.Revenue, r.Share ?? 0, r.Slot)).ToList(),
                Total = list.Sum(r => r.Revenue)
            },
            new ChartDto
            {
                Id = "over-time", Kind = "stackedcolumns",
                Title = byWeek ? "Revenue by category, each week" : "Revenue by category, each day",
                ValueFormat = "money", Series = series, Stacks = stacks
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Revenue between {ReportMath.Range(ctx.From, ctx.To)} by category — {ReportMath.Money(ReportText.Num(totals, "revenue"))} in all.";
}

internal sealed class GstRow
{
    public long Id { get; set; }
    public decimal Rate { get; set; }
    public string Supply { get; set; } = "";
    public int Invoices { get; set; }
    public decimal Taxable { get; set; }
    public decimal Cgst { get; set; }
    public decimal Sgst { get; set; }
    public decimal Igst { get; set; }
    public decimal Tax { get; set; }
    public decimal Invoiced { get; set; }
}

/// <summary>Taxable value and output tax by rate, split into CGST / SGST / IGST and
/// B2B / B2C, the way GSTR-1 asks for it. Invoiced, never "revenue" (spec §8.2).</summary>
internal sealed class GstSummaryReport : ReportHandler<GstRow>
{
    public override string Key => "gst-summary";
    public override string Name => "GST summary";
    public override string Description => "Taxable value and output tax grouped by rate (0%, 5%, 12%, 18%), split into CGST, SGST and IGST, in the shape GSTR-1 needs.";
    public override string Group => "revenue";
    public override string[] Keywords => ["gst", "tax", "gstr", "gstr-1", "cgst", "sgst", "igst", "output tax"];
    public override string Permission => PermissionCodes.Report.Finance;
    public override string RowNoun => "rate lines";
    public override string DefaultSort => "rate:asc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Select("supply", "Customers", Analytics.Filters.Options(
            ("b2b", "B2B — with a GSTIN"), ("b2c", "B2C — no GSTIN")), allLabel: "B2B and B2C")
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(GstRow.Rate), "Rate", "percent"),
        new(nameof(GstRow.Supply), "Supply", "text"),
        new(nameof(GstRow.Invoices), "Invoices", "number"),
        new(nameof(GstRow.Taxable), "Taxable value", "money", Total.Sum),
        new(nameof(GstRow.Cgst), "CGST", "money", Total.Sum),
        new(nameof(GstRow.Sgst), "SGST", "money", Total.Sum),
        new(nameof(GstRow.Igst), "IGST", "money", Total.Sum),
        new(nameof(GstRow.Tax), "Total tax", "money", Total.Sum),
        new(nameof(GstRow.Invoiced), "Invoiced (with tax)", "money", Total.Sum)
    ];

    private static readonly decimal[] StandardRates = [0m, 5m, 12m, 18m];

    protected override async Task<IQueryable<GstRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var lines = ctx.Db.InvoiceLines.AsNoTracking()
            .Where(l => l.Invoice.InvoiceDate >= ctx.From && l.Invoice.InvoiceDate <= ctx.To &&
                        l.Invoice.Status != InvoiceStatus.Cancelled);
        if (ctx.LocationId is { } location)
        {
            lines = lines.Where(l => l.Invoice.LocationId == location);
        }
        var supply = ctx.One("supply");
        if (supply == "b2b")
        {
            lines = lines.Where(l => l.Invoice.CustomerGstin != null && l.Invoice.CustomerGstin != "");
        }
        else if (supply == "b2c")
        {
            lines = lines.Where(l => l.Invoice.CustomerGstin == null || l.Invoice.CustomerGstin == "");
        }
        var grouped = await lines
            .GroupBy(l => new
            {
                l.TaxPercent,
                B2B = l.Invoice.CustomerGstin != null && l.Invoice.CustomerGstin != "",
                Inter = l.Invoice.IgstAmount > 0
            })
            .Select(g => new
            {
                g.Key.TaxPercent, g.Key.B2B, g.Key.Inter,
                Invoices = g.Select(l => l.InvoiceId).Distinct().Count(),
                Taxable = g.Sum(l => l.LineAmount),
                Tax = g.Sum(l => l.TaxAmount),
                Total = g.Sum(l => l.TotalAmount)
            })
            .ToListAsync(ct);

        var supplies = supply switch
        {
            "b2b" => new[] { true },
            "b2c" => [false],
            _ => [true, false]
        };
        var rates = StandardRates.Concat(grouped.Select(g => g.TaxPercent)).Distinct().OrderBy(r => r).ToList();
        var rows = new List<GstRow>();
        foreach (var rate in rates)
        {
            foreach (var b2b in supplies)
            {
                var parts = grouped.Where(g => g.TaxPercent == rate && g.B2B == b2b).ToList();
                // Intra-state tax is split equally into CGST and SGST; inter-state is IGST.
                var intraTax = parts.Where(p => !p.Inter).Sum(p => p.Tax);
                var cgst = Math.Round(intraTax / 2m, 2, MidpointRounding.AwayFromZero);
                rows.Add(new GstRow
                {
                    Id = rows.Count + 1,
                    Rate = rate,
                    Supply = b2b ? "B2B" : "B2C",
                    Invoices = parts.Sum(p => p.Invoices),
                    Taxable = parts.Sum(p => p.Taxable),
                    Cgst = cgst,
                    Sgst = intraTax - cgst,
                    Igst = parts.Where(p => p.Inter).Sum(p => p.Tax),
                    Tax = parts.Sum(p => p.Tax),
                    Invoiced = parts.Sum(p => p.Total)
                });
            }
        }
        return FromList(rows);
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<GstRow> rows,
        CancellationToken ct)
    {
        var list = rows.ToList();
        var invoices = ctx.Db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= ctx.From && i.InvoiceDate <= ctx.To && i.Status != InvoiceStatus.Cancelled);
        if (ctx.LocationId is { } location)
        {
            invoices = invoices.Where(i => i.LocationId == location);
        }
        return
        [
            new("taxable", "Taxable value", list.Sum(r => r.Taxable), "money"),
            new("tax", "Output tax", list.Sum(r => r.Tax), "money",
                Sub: $"CGST {ReportMath.Money(list.Sum(r => r.Cgst))} · SGST {ReportMath.Money(list.Sum(r => r.Sgst))} · IGST {ReportMath.Money(list.Sum(r => r.Igst))}"),
            new("invoiced", "Invoiced", list.Sum(r => r.Invoiced), "money", Sub: "Including tax — invoiced, not received"),
            new("invoices", "Invoices", await invoices.CountAsync(ct), "count")
        ];
    }

    protected override Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<GstRow> rows,
        CancellationToken ct)
    {
        var byRate = rows.ToList().GroupBy(r => r.Rate)
            .Select(g => (Label: $"{g.Key:0.##}%", Tax: g.Sum(r => r.Tax))).ToList();
        var slices = ChartDtoBuilder.Slices(byRate.Select(r => (r.Label, r.Tax, 1)));
        return Task.FromResult(new List<ChartDto>
        {
            new()
            {
                Id = "by-rate", Kind = "hbars", Title = "Output tax by rate", ValueFormat = "money",
                Slices = slices, Total = slices.Sum(s => s.Value)
            }
        });
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"GST on invoices raised between {ReportMath.Range(ctx.From, ctx.To)} — {ReportMath.Money(ReportText.Num(totals, "tax"))} output tax on {ReportMath.Money(ReportText.Num(totals, "taxable"))} taxable value.";
}

internal sealed class InvoiceRow
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public DateOnly Date { get; set; }
    public long CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public string? Company { get; set; }
    public string? Gstin { get; set; }
    public decimal Taxable { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Balance { get; set; }
    public DateOnly Due { get; set; }
    public string Status { get; set; } = "";
}

internal sealed class InvoiceRegisterReport : ReportHandler<InvoiceRow>
{
    public override string Key => "invoice-register";
    public override string Name => "Invoice register";
    public override string Description => "Every invoice raised, with the customer, GSTIN, taxable value, tax, total, due date and paid status.";
    public override string Group => "revenue";
    public override string[] Keywords => ["invoice", "invoices", "register", "gstin", "billing", "invoiced"];
    public override string Permission => PermissionCodes.Report.Finance;
    public override string RowNoun => "invoices";
    public override string DefaultSort => "date:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Multi("status", "Paid status", Analytics.Filters.Options(
                ("paid", "Paid"), ("part", "Part-paid"), ("unpaid", "Unpaid"), ("overdue", "Overdue"),
                ("cancelled", "Cancelled")),
            ["paid", "part", "unpaid", "overdue"]),
        Analytics.Filters.Customer()
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(InvoiceRow.Number), "Invoice", "text", Searchable: true),
        new(nameof(InvoiceRow.Date), "Date", "date"),
        new(nameof(InvoiceRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(InvoiceRow.Company), "Company", "text", Searchable: true),
        new(nameof(InvoiceRow.Gstin), "GSTIN", "text", Searchable: true),
        new(nameof(InvoiceRow.Taxable), "Taxable value", "money", Total.Sum),
        new(nameof(InvoiceRow.Tax), "Tax", "money", Total.Sum),
        new(nameof(InvoiceRow.Total), "Total", "money", Total.Sum),
        new(nameof(InvoiceRow.Paid), "Paid", "money", Total.Sum, Visible: false),
        new(nameof(InvoiceRow.Balance), "Balance", "money", Total.Sum),
        new(nameof(InvoiceRow.Due), "Due date", "date"),
        new(nameof(InvoiceRow.Status), "Status", "status")
    ];

    protected override string? LinkFor(InvoiceRow row) => $"/admin/customers/{row.CustomerId}";

    protected override Task<IQueryable<InvoiceRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var today = ctx.Today;
        var invoices = ctx.Db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= ctx.From && i.InvoiceDate <= ctx.To);
        if (ctx.LocationId is { } location)
        {
            invoices = invoices.Where(i => i.LocationId == location);
        }
        if (ctx.CustomerId is { } customer)
        {
            invoices = invoices.Where(i => i.CustomerId == customer);
        }
        var rows = invoices.Select(i => new InvoiceRow
        {
            Id = i.InvoiceId,
            Number = i.InvoiceNumber,
            Date = i.InvoiceDate,
            CustomerId = i.CustomerId,
            Customer = i.CustomerName,
            Company = i.CustomerCompanyName,
            Gstin = i.CustomerGstin,
            Taxable = i.TaxableAmount,
            Tax = i.CgstAmount + i.SgstAmount + i.IgstAmount,
            Total = i.TotalAmount,
            Paid = i.PaidAmount,
            Balance = i.Status == InvoiceStatus.Cancelled || i.Status == InvoiceStatus.Refunded ? 0m : i.OutstandingAmount,
            Due = i.DueDate,
            Status = i.Status == InvoiceStatus.Cancelled ? "Cancelled"
                : i.Status == InvoiceStatus.Refunded ? "Refunded"
                : i.OutstandingAmount <= 0 ? "Paid"
                : i.DueDate < today ? "Overdue"
                : i.PaidAmount > 0 ? "Part-paid" : "Unpaid"
        });
        var statuses = ctx.Multi("status").Select(s => s switch
        {
            "paid" => "Paid",
            "part" => "Part-paid",
            "unpaid" => "Unpaid",
            "overdue" => "Overdue",
            "cancelled" => "Cancelled",
            _ => ""
        }).ToList();
        if (statuses.Contains("Paid"))
        {
            statuses.Add("Refunded");
        }
        if (statuses.Count > 0)
        {
            rows = rows.Where(r => statuses.Contains(r.Status));
        }
        return Task.FromResult(rows);
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<InvoiceRow> rows,
        CancellationToken ct)
    {
        var count = await CountAsync(rows, ct);
        var invoiced = await SumAsync(rows, r => r.Total, ct);
        var collected = await SumAsync(rows, r => r.Paid, ct);
        var balance = await SumAsync(rows, r => r.Balance, ct);
        return
        [
            new("invoices", "Invoices", count, "count"),
            new("invoiced", "Invoiced", invoiced, "money", Sub: "Raised in the period, including tax"),
            new("collected", "Collected", collected, "money", Compare: false,
                Sub: invoiced == 0 ? null : $"{ReportMath.Percent(collected * 100m / invoiced)} of the invoiced value"),
            new("balance", "Still owed", balance, "money", UpIsGood: false, Compare: false)
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<InvoiceRow> rows,
        CancellationToken ct)
    {
        var byDay = await rows.GroupBy(r => r.Date)
            .Select(g => new { Day = g.Key, Value = g.Sum(r => r.Total), Count = g.Count() })
            .ToListAsync(ct);
        var points = ChartDtoBuilder.DailyPoints(ctx.From, ctx.To,
            byDay.ToDictionary(d => d.Day, d => (d.Value, (decimal)d.Count)));
        var byStatus = await rows.GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Value = g.Sum(r => r.Total) })
            .ToListAsync(ct);
        string[] statuses = ["Paid", "Part-paid", "Unpaid", "Overdue"];
        var slices = ChartDtoBuilder.Slices(statuses.Select((s, i) =>
            (s, byStatus.Where(b => b.Status == s || (s == "Paid" && b.Status == "Refunded")).Sum(b => b.Value), i + 1)));
        return
        [
            new ChartDto
            {
                Id = "per-day", Kind = "columns", Title = "Invoiced each day", ValueFormat = "money",
                Value2Format = "count", Value2Label = "invoices", Points = points,
                HighlightIndex = ChartDtoBuilder.Highest(points)
            },
            new ChartDto
            {
                Id = "status", Kind = "stackedbar", Title = "Paid status, by value", ValueFormat = "money",
                Slices = slices, Total = slices.Sum(s => s.Value)
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Every invoice raised between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "invoices")}, {ReportMath.Money(ReportText.Num(totals, "total"))} invoiced.";
}

internal sealed class PaymentRow
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime Paid { get; set; }
    public long CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public string? Invoice { get; set; }
    public string For { get; set; } = "";
    public string Mode { get; set; } = "";
    public string? Reference { get; set; }
    public decimal Amount { get; set; }
}

internal sealed class PaymentsReceivedReport : ReportHandler<PaymentRow>
{
    public override string Key => "payments-received";
    public override string Name => "Payments received";
    public override string Description => "Every payment with date, customer, invoice, mode (UPI, card, bank transfer, cash, wallet) and reference.";
    public override string Group => "revenue";
    public override string[] Keywords => ["payments", "received", "collections", "upi", "card", "cash", "money"];
    public override string Permission => PermissionCodes.Report.Finance;
    public override string RowNoun => "payments";
    public override string DefaultSort => "paid:desc";

    private static List<ReportOption> Modes => Analytics.Filters.Options(
        ("upi", "UPI"), ("card", "Card"), ("bank", "Bank transfer"), ("cash", "Cash"), ("wallet", "Wallet"),
        ("other", "Other"));

    private static List<ReportOption> Purposes => Analytics.Filters.Options(
        ("booking", "Bookings"), ("pass", "Passes"), ("service", "Services"), ("invoice", "Invoices"),
        ("topup", "Wallet top-ups"));

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Multi("mode", "Payment mode", Modes, allLabel: "All modes"),
        Analytics.Filters.Multi("purpose", "For", Purposes, allLabel: "Everything"),
        Analytics.Filters.Customer()
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(PaymentRow.Number), "Payment", "text", Searchable: true),
        new(nameof(PaymentRow.Paid), "Date", "datetime"),
        new(nameof(PaymentRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(PaymentRow.Invoice), "Invoice", "text", Searchable: true),
        new(nameof(PaymentRow.For), "For", "text"),
        new(nameof(PaymentRow.Mode), "Mode", "text"),
        new(nameof(PaymentRow.Reference), "Reference", "text", Searchable: true),
        new(nameof(PaymentRow.Amount), "Amount", "money", Total.Sum)
    ];

    protected override string? LinkFor(PaymentRow row) => $"/admin/customers/{row.CustomerId}";

    protected override Task<IQueryable<PaymentRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var payments = db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Success && p.PaidUtc >= ctx.FromUtc && p.PaidUtc < ctx.ToUtc);
        if (ctx.CustomerId is { } customer)
        {
            payments = payments.Where(p => p.CustomerId == customer);
        }
        var methods = ctx.Multi("mode").SelectMany(m => m switch
        {
            "upi" => new[] { PaymentMethod.Upi },
            "card" => [PaymentMethod.CreditCard, PaymentMethod.DebitCard],
            "bank" => [PaymentMethod.NetBanking],
            "cash" => [PaymentMethod.Cash],
            "wallet" => [PaymentMethod.Wallet],
            "other" => [PaymentMethod.Other],
            _ => []
        }).ToList();
        if (methods.Count > 0)
        {
            payments = payments.Where(p => methods.Contains(p.PaymentMethod));
        }
        var rows =
            from p in payments
            from u in db.Users.Where(u => u.UserId == p.CustomerId)
            from inv in db.Invoices.Where(i => i.InvoiceId == p.InvoiceId).DefaultIfEmpty()
            select new PaymentRow
            {
                Id = p.PaymentId,
                Number = p.PaymentNumber,
                Paid = p.PaidUtc!.Value,
                CustomerId = p.CustomerId,
                Customer = u.FullName,
                Invoice = inv == null ? null : inv.InvoiceNumber,
                For = p.BookingId != null ? "Booking"
                    : p.CustomerPassId != null ? "Pass"
                    : p.ServiceOrderId != null ? "Service"
                    : p.InvoiceId != null ? "Invoice" : "Wallet top-up",
                Mode = p.PaymentMethod == PaymentMethod.Wallet ? "Wallet"
                    : p.PaymentMethod == PaymentMethod.Upi ? "UPI"
                    : p.PaymentMethod == PaymentMethod.CreditCard ? "Credit card"
                    : p.PaymentMethod == PaymentMethod.DebitCard ? "Debit card"
                    : p.PaymentMethod == PaymentMethod.NetBanking ? "Bank transfer"
                    : p.PaymentMethod == PaymentMethod.Cash ? "Cash" : "Other",
                Reference = p.GatewayPaymentId,
                Amount = p.Amount
            };
        var purposes = ctx.Multi("purpose").Select(p => p switch
        {
            "booking" => "Booking",
            "pass" => "Pass",
            "service" => "Service",
            "invoice" => "Invoice",
            "topup" => "Wallet top-up",
            _ => ""
        }).ToList();
        if (purposes.Count > 0)
        {
            rows = rows.Where(r => purposes.Contains(r.For));
        }
        return Task.FromResult(rows);
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<PaymentRow> rows,
        CancellationToken ct)
    {
        var count = await CountAsync(rows, ct);
        var received = await SumAsync(rows, r => r.Amount, ct);
        var topUps = await SumAsync(rows.Where(r => r.For == "Wallet top-up"), r => r.Amount, ct);
        return
        [
            new("payments", "Payments", count, "count"),
            new("received", "Received", received, "money"),
            new("topups", "Wallet top-ups", topUps, "money", Sub: "Not revenue until the wallet is spent"),
            new("average", "Average payment", count == 0 ? null : received / count, "money")
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<PaymentRow> rows,
        CancellationToken ct)
    {
        var paid = await rows.Select(r => new { r.Paid, r.Amount, r.Mode }).ToListAsync(ct);
        var byDay = paid.GroupBy(r => IstClock.IstDateOf(r.Paid))
            .ToDictionary(g => g.Key, g => (g.Sum(r => r.Amount), (decimal)g.Count()));
        var points = ChartDtoBuilder.DailyPoints(ctx.From, ctx.To, byDay);
        var modes = paid.GroupBy(r => r.Mode == "Credit card" || r.Mode == "Debit card" ? "Card" : r.Mode)
            .Select(g => (Label: g.Key, Value: g.Sum(r => r.Amount)))
            .OrderByDescending(g => g.Value).ToList();
        var folded = modes.Take(4).ToList();
        if (modes.Count > 4)
        {
            folded.Add(("Other", modes.Skip(4).Sum(m => m.Value)));
        }
        var slices = ChartDtoBuilder.Slices(folded.Select((m, i) => (m.Label, m.Value, m.Label == "Other" ? 0 : i + 1)));
        return
        [
            new ChartDto
            {
                Id = "per-day", Kind = "columns", Title = "Received each day", ValueFormat = "money",
                Value2Format = "count", Value2Label = "payments", Points = points,
                HighlightIndex = ChartDtoBuilder.Highest(points)
            },
            new ChartDto
            {
                Id = "modes", Kind = "stackedbar", Title = "How it was paid", ValueFormat = "money",
                Slices = slices, Total = slices.Sum(s => s.Value)
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Every payment received between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "payments")}, {ReportMath.Money(ReportText.Num(totals, "amount"))} in all.";
}

internal sealed class OwedRow
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public long CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public string? Company { get; set; }
    public string? Phone { get; set; }
    public string Email { get; set; } = "";
    public DateOnly Raised { get; set; }
    public DateOnly Due { get; set; }
    public int DaysLate { get; set; }
    public string Bucket { get; set; } = "";
    public decimal Total { get; set; }
    public decimal Paid { get; set; }
    public decimal Balance { get; set; }
}

/// <summary>
/// Unpaid and part-paid invoices, aged by due date on the unpaid balance (spec §9.3).
/// An invoice is owed regardless of when it was raised, so the range only sets the
/// latest invoice date: opened from the overview, it shows exactly the overview's card.
/// </summary>
internal sealed class MoneyOwedReport : ReportHandler<OwedRow>
{
    public override string Key => "money-owed";
    public override string Name => "Money owed to us";
    public override string Description => "Unpaid and part-paid invoices grouped into not due, 1–30, 31–60 and over 60 days late, with contact details.";
    public override string Group => "revenue";
    public override string[] Keywords => ["money", "owed", "outstanding", "overdue", "ageing", "aging", "debtors", "receivables"];
    public override string Permission => PermissionCodes.Report.Finance;
    public override string RowNoun => "invoices";
    public override string DefaultSort => "daysLate:desc";

    internal static readonly string[] Buckets = ["Not due yet", "1–30 days late", "31–60 days late", "Over 60 days late"];

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
    [
        Analytics.Filters.Dates(),
        Analytics.Filters.Location(lookups),
        Analytics.Filters.Multi("bucket", "How late", Analytics.Filters.Options(
            ("0", Buckets[0]), ("1", Buckets[1]), ("2", Buckets[2]), ("3", Buckets[3])), allLabel: "Any"),
        Analytics.Filters.Customer()
    ];

    protected override List<Col> Cols =>
    [
        new(nameof(OwedRow.Number), "Invoice", "text", Searchable: true),
        new(nameof(OwedRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(OwedRow.Company), "Company", "text", Searchable: true),
        new(nameof(OwedRow.Phone), "Phone", "text", Searchable: true),
        new(nameof(OwedRow.Email), "Email", "text", Visible: false, Searchable: true),
        new(nameof(OwedRow.Raised), "Raised", "date"),
        new(nameof(OwedRow.Due), "Due date", "date"),
        new(nameof(OwedRow.DaysLate), "Days late", "number"),
        new(nameof(OwedRow.Bucket), "Age", "status"),
        new(nameof(OwedRow.Total), "Total", "money", Total.Sum, Visible: false),
        new(nameof(OwedRow.Paid), "Paid", "money", Total.Sum, Visible: false),
        new(nameof(OwedRow.Balance), "Balance", "money", Total.Sum)
    ];

    protected override string? LinkFor(OwedRow row) => $"/admin/customers/{row.CustomerId}";

    internal static IQueryable<OwedRow> Owed(Persistence.RegalisDbContext db, DateOnly raisedBy, DateOnly today,
        int? locationId, long? customerId)
    {
        var invoices = db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate <= raisedBy && i.OutstandingAmount > 0 &&
                        i.Status != InvoiceStatus.Cancelled && i.Status != InvoiceStatus.Refunded);
        if (locationId is { } location)
        {
            invoices = invoices.Where(i => i.LocationId == location);
        }
        if (customerId is { } customer)
        {
            invoices = invoices.Where(i => i.CustomerId == customer);
        }
        var late30 = today.AddDays(-30);
        var late60 = today.AddDays(-60);
        return invoices.Select(i => new OwedRow
        {
            Id = i.InvoiceId,
            Number = i.InvoiceNumber,
            CustomerId = i.CustomerId,
            Customer = i.CustomerName,
            Company = i.CustomerCompanyName,
            Phone = i.CustomerMobile,
            Email = i.CustomerEmail,
            Raised = i.InvoiceDate,
            Due = i.DueDate,
            DaysLate = i.DueDate >= today ? 0 : EF.Functions.DateDiffDay(i.DueDate, today),
            Bucket = i.DueDate >= today ? "Not due yet"
                : i.DueDate >= late30 ? "1–30 days late"
                : i.DueDate >= late60 ? "31–60 days late" : "Over 60 days late",
            Total = i.TotalAmount,
            Paid = i.PaidAmount,
            Balance = i.OutstandingAmount
        });
    }

    protected override Task<IQueryable<OwedRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var rows = Owed(ctx.Db, ctx.To, ctx.Today, ctx.LocationId, ctx.CustomerId);
        var buckets = ctx.Ints("bucket").Where(b => b is >= 0 and < 4).Select(b => Buckets[b]).ToList();
        if (buckets.Count > 0)
        {
            rows = rows.Where(r => buckets.Contains(r.Bucket));
        }
        return Task.FromResult(rows);
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<OwedRow> rows,
        CancellationToken ct)
    {
        var count = await CountAsync(rows, ct);
        var owed = await SumAsync(rows, r => r.Balance, ct);
        var overdue = await SumAsync(rows.Where(r => r.DaysLate > 0), r => r.Balance, ct);
        var over60 = await SumAsync(rows.Where(r => r.DaysLate > 60), r => r.Balance, ct);
        var oldest = count == 0 ? (int?)null : await rows.MaxAsync(r => r.DaysLate, ct);
        return
        [
            new("owed", "Owed to us", owed, "money", UpIsGood: false, Compare: false,
                Sub: ReportText.Plural(count, "invoices")),
            new("overdue", "Overdue", overdue, "money", UpIsGood: false, Compare: false),
            new("over60", "Over 60 days late", over60, "money", UpIsGood: false, Compare: false),
            new("oldest", "Oldest", oldest, "count", Compare: false,
                Sub: oldest is null or 0 ? "Nothing is late" : "days past due")
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<OwedRow> rows,
        CancellationToken ct)
    {
        var grouped = await rows.GroupBy(r => r.Bucket)
            .Select(g => new { Bucket = g.Key, Balance = g.Sum(r => r.Balance) })
            .ToListAsync(ct);
        var slices = ChartDtoBuilder.Slices(Buckets.Select((b, i) =>
            (b, grouped.FirstOrDefault(g => g.Bucket == b)?.Balance ?? 0m, i + 1)));
        return
        [
            new ChartDto
            {
                Id = "ageing", Kind = "hbars", Title = "Money owed, by how late", ValueFormat = "money",
                Slices = slices, Total = slices.Sum(s => s.Value)
            }
        ];
    }

    public override async Task<string?> LiveFigureAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var owed = await Owed(ctx.Db, ctx.Today, ctx.Today, null, null).SumAsync(r => (decimal?)r.Balance, ct) ?? 0m;
        return $"{ReportMath.Money(owed)} owed";
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Every unpaid or part-paid invoice raised up to {ReportMath.Day(ctx.To)} — {ReportText.Plural(count, "invoices")}, {ReportMath.Money(ReportText.Num(totals, "balance"))} owed.";
}

internal sealed class RefundRow
{
    public long Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime Date { get; set; }
    public long CustomerId { get; set; }
    public string Customer { get; set; } = "";
    public string? Original { get; set; }
    public string? Reason { get; set; }
    public string ApprovedBy { get; set; } = "";
    public string RefundedTo { get; set; } = "";
    public decimal Amount { get; set; }
}

/// <summary>Refunds paid back. Regalis refunds to the customer's wallet and issues no
/// separate credit notes, so every refund is a wallet credit with its original booking
/// or pass.</summary>
internal sealed class RefundsReport : ReportHandler<RefundRow>
{
    public override string Key => "refunds";
    public override string Name => "Refunds & credit notes";
    public override string Description => "Every refund with the reason, who approved it and the original booking or pass. Refunds go to the customer's wallet.";
    public override string Group => "revenue";
    public override string[] Keywords => ["refund", "refunds", "credit note", "credit notes", "cancelled", "money back"];
    public override string Permission => PermissionCodes.Report.Finance;
    public override string RowNoun => "refunds";
    public override string DefaultSort => "date:desc";

    public override List<ReportFilterDefinition> Filters(ReportLookups lookups) =>
        [Analytics.Filters.Dates(), Analytics.Filters.Customer()];

    protected override List<Col> Cols =>
    [
        new(nameof(RefundRow.Number), "Refund", "text", Searchable: true),
        new(nameof(RefundRow.Date), "Date", "datetime"),
        new(nameof(RefundRow.Customer), "Customer", "text", Searchable: true),
        new(nameof(RefundRow.Original), "Original", "text", Searchable: true),
        new(nameof(RefundRow.Reason), "Reason", "text", Searchable: true),
        new(nameof(RefundRow.ApprovedBy), "Approved by", "text"),
        new(nameof(RefundRow.RefundedTo), "Refunded to", "text", Visible: false),
        new(nameof(RefundRow.Amount), "Amount", "money", Total.Sum)
    ];

    protected override string? LinkFor(RefundRow row) => $"/admin/customers/{row.CustomerId}";

    protected override Task<IQueryable<RefundRow>> QueryAsync(ReportRunContext ctx, CancellationToken ct)
    {
        var db = ctx.Db;
        var credits = db.WalletTransactions.AsNoTracking()
            .Where(t => (t.Category == WalletCategory.Refund || t.Category == WalletCategory.Cancellation) &&
                        t.Direction == WalletDirection.Credit &&
                        t.TransactionUtc >= ctx.FromUtc && t.TransactionUtc < ctx.ToUtc);
        if (ctx.CustomerId is { } customer)
        {
            credits = credits.Where(t => t.CustomerId == customer);
        }
        return Task.FromResult(
            from t in credits
            from u in db.Users.Where(u => u.UserId == t.CustomerId)
            from approver in db.Users.Where(x => x.UserId == t.PerformedByUserId).DefaultIfEmpty()
            from b in db.Bookings.Where(x => t.ReferenceType == "Booking" && x.BookingId == t.ReferenceId).DefaultIfEmpty()
            from cp in db.CustomerPasses.Where(x => t.ReferenceType == "CustomerPass" && x.CustomerPassId == t.ReferenceId).DefaultIfEmpty()
            select new RefundRow
            {
                Id = t.WalletTransactionId,
                Number = t.TransactionNumber,
                Date = t.TransactionUtc,
                CustomerId = t.CustomerId,
                Customer = u.FullName,
                Original = b != null ? b.BookingNumber : cp != null ? cp.PassNumber : null,
                Reason = b != null && b.CancellationReason != null ? b.CancellationReason : t.Narration,
                ApprovedBy = approver == null ? "System"
                    : approver.UserId == t.CustomerId ? "Customer (self-service)" : approver.FullName,
                RefundedTo = "Wallet",
                Amount = t.Amount
            });
    }

    protected override async Task<List<Figure>> FiguresAsync(ReportRunContext ctx, IQueryable<RefundRow> rows,
        CancellationToken ct)
    {
        var count = await CountAsync(rows, ct);
        var amount = await SumAsync(rows, r => r.Amount, ct);
        var largest = count == 0 ? (decimal?)null : await rows.MaxAsync(r => r.Amount, ct);
        return
        [
            new("refunds", "Refunds", count, "count", UpIsGood: false),
            new("amount", "Refunded", amount, "money", UpIsGood: false, Sub: "Credited to customer wallets"),
            new("average", "Average refund", count == 0 ? null : amount / count, "money", UpIsGood: false),
            new("largest", "Largest", largest, "money", Compare: false)
        ];
    }

    protected override async Task<List<ChartDto>> ChartsAsync(ReportRunContext ctx, IQueryable<RefundRow> rows,
        CancellationToken ct)
    {
        var list = await rows.Select(r => new { r.Date, r.Amount }).ToListAsync(ct);
        var byDay = list.GroupBy(r => IstClock.IstDateOf(r.Date))
            .ToDictionary(g => g.Key, g => (g.Sum(r => r.Amount), (decimal)g.Count()));
        var points = ChartDtoBuilder.DailyPoints(ctx.From, ctx.To, byDay);
        return
        [
            new ChartDto
            {
                Id = "per-day", Kind = "columns", Title = "Refunded each day", ValueFormat = "money",
                Value2Format = "count", Value2Label = "refunds", Points = points,
                HighlightIndex = ChartDtoBuilder.Highest(points)
            }
        ];
    }

    public override string SentenceFor(ReportRunContext ctx, int count, Dictionary<string, object?> totals) =>
        $"Every refund paid back between {ReportMath.Range(ctx.From, ctx.To)} — {ReportText.Plural(count, "refunds")}, {ReportMath.Money(ReportText.Num(totals, "amount"))} in all.";
}
