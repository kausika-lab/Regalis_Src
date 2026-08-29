using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Reporting;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Reporting;

/// <summary>
/// The report engine (M24). Every report produces the same tabular shape, so one
/// grid (ReportShell), one Excel exporter and one PDF exporter serve all sixteen.
/// Day 9 ships reports 1–8; the remaining keys are catalogued but flagged
/// unavailable until Day 10.
/// </summary>
public class ReportService(RegalisDbContext db, IDateTimeProvider clock) : IReportService
{
    private const int ViewRowCap = 2000;
    private const int ExportRowCap = 10000;

    private static readonly (string Key, string Title, string Description, bool Available)[] Catalogue =
    [
        ("booking", "Booking Report", "Every booking in the window with status, payment and value.", true),
        ("revenue", "Revenue Report", "Captured payments per day, split by source.", true),
        ("customer", "Customer Report", "The register with booking counts, spend and wallet balance.", true),
        ("occupancy", "Occupancy Report", "Booked hours against available hours per workspace.", true),
        ("desk", "Desk Booking Report", "The booking report scoped to desks.", true),
        ("cabin", "Cabin Booking Report", "The booking report scoped to cabins.", true),
        ("meeting-room", "Meeting Room Report", "The booking report scoped to meeting rooms.", true),
        ("day-pass", "Day Pass Report", "Day passes sold in the window.", true),
        ("monthly-pass", "Monthly Pass Report", "Monthly passes sold in the window.", false),
        ("payment", "Payment Report", "Every payment with method, gateway and status.", false),
        ("invoice", "Invoice Report", "GST invoices with tax break-up.", false),
        ("wallet", "Wallet Report", "Wallet ledger by customer and category.", false),
        ("coffee", "Services & Coffee Report", "Service orders by item and day.", false),
        ("ticket", "Support Ticket Report", "Tickets by category, status and resolution time.", false),
        ("check-in-out", "Check-in/out Report", "Attendance sessions in the window.", false),
        ("qr-access", "QR Access Report", "Every gate decision, granted or denied.", false)
    ];

    public List<ReportInfo> GetCatalogue() =>
        Catalogue.Select(c => new ReportInfo(c.Key, c.Title, c.Description, c.Available)).ToList();

    public async Task<ReportResult> GetAsync(string key, ReportFilterRequest filters,
        CancellationToken ct = default) =>
        await BuildAsync(key, filters, ViewRowCap, ct);

    public async Task<ReportExportResult> ExportAsync(string key, string format,
        ReportFilterRequest filters, CancellationToken ct = default)
    {
        var report = await BuildAsync(key, filters, ExportRowCap, ct);
        return format.ToLowerInvariant() switch
        {
            "xlsx" => new ReportExportResult(ReportExcelExporter.Generate(report),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"{key}-report_{report.From:yyyyMMdd}_{report.To:yyyyMMdd}.xlsx"),
            "pdf" => new ReportExportResult(ReportPdfGenerator.Generate(report),
                "application/pdf",
                $"{key}-report_{report.From:yyyyMMdd}_{report.To:yyyyMMdd}.pdf"),
            _ => throw new BusinessRuleException("format must be xlsx or pdf.")
        };
    }

    private async Task<ReportResult> BuildAsync(string key, ReportFilterRequest filters,
        int rowCap, CancellationToken ct)
    {
        var info = Catalogue.FirstOrDefault(c => c.Key == key);
        if (info.Key is null)
        {
            throw new NotFoundException("Report", key);
        }
        if (!info.Available)
        {
            throw new BusinessRuleException($"{info.Title} arrives with the Day 10 report set.");
        }

        // Default window: the last 30 IST days inclusive.
        var todayIst = IstClock.IstDateOf(clock.UtcNow);
        var to = filters.To ?? todayIst;
        var from = filters.From ?? to.AddDays(-29);
        if (from > to)
        {
            (from, to) = (to, from);
        }

        var (columns, rows, summary, totalRows) = key switch
        {
            "booking" => await BuildBookingAsync(filters, from, to, rowCap, typeCode: null, ct),
            "desk" => await BuildBookingAsync(filters, from, to, rowCap, "DESK", ct),
            "cabin" => await BuildBookingAsync(filters, from, to, rowCap, "CABIN", ct),
            "meeting-room" => await BuildBookingAsync(filters, from, to, rowCap, "MEETING_ROOM", ct),
            "revenue" => await BuildRevenueAsync(filters, from, to, ct),
            "customer" => await BuildCustomerAsync(filters, from, to, rowCap, ct),
            "occupancy" => await BuildOccupancyAsync(filters, from, to, ct),
            "day-pass" => await BuildPassAsync(filters, from, to, rowCap, PassType.DayPass, ct),
            _ => throw new NotFoundException("Report", key)
        };

        return new ReportResult(key, info.Title, info.Description, from, to, columns, rows,
            summary, totalRows, Truncated: totalRows > rows.Count);
    }

    // ── 1, 5, 6, 7 · Booking reports ────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildBookingAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            string? typeCode, CancellationToken ct)
    {
        var query = db.Bookings.AsNoTracking()
            .Where(b => b.BookingDate >= from && b.BookingDate <= to);
        if (typeCode is not null)
        {
            query = query.Where(b => b.Workspace.WorkspaceType.TypeCode == typeCode);
        }
        if (filters.LocationId is not null)
        {
            query = query.Where(b => b.LocationId == filters.LocationId);
        }
        if (filters.WorkspaceTypeId is not null)
        {
            query = query.Where(b => b.Workspace.WorkspaceTypeId == filters.WorkspaceTypeId);
        }
        if (filters.WorkspaceId is not null)
        {
            query = query.Where(b => b.WorkspaceId == filters.WorkspaceId);
        }
        if (filters.CustomerId is not null)
        {
            query = query.Where(b => b.CustomerId == filters.CustomerId);
        }
        if (filters.BookingStatus is not null)
        {
            query = query.Where(b => (byte)b.Status == filters.BookingStatus);
        }
        if (filters.PaymentStatus is not null)
        {
            query = query.Where(b => (byte)b.PaymentStatus == filters.PaymentStatus);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(b => b.StartUtc)
            .Take(rowCap)
            .Select(b => new
            {
                b.BookingNumber,
                Customer = b.Customer.FullName,
                Workspace = b.Workspace.WorkspaceName,
                Type = b.Workspace.WorkspaceType.TypeName,
                b.StartUtc,
                b.EndUtc,
                b.DurationMinutes,
                b.Status,
                b.PaymentStatus,
                b.TotalAmount
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "Booking", "string"),
            new("customer", "Customer", "string"),
            new("workspace", "Workspace", "string"),
            new("type", "Type", "string"),
            new("start", "Start (IST)", "datetime"),
            new("end", "End (IST)", "datetime"),
            new("hours", "Hours", "number"),
            new("status", "Status", "string"),
            new("payment", "Payment", "string"),
            new("amount", "Amount", "money")
        };
        var rows = data.Select(b => Row(
            ("number", b.BookingNumber),
            ("customer", b.Customer),
            ("workspace", b.Workspace),
            ("type", b.Type),
            ("start", b.StartUtc),
            ("end", b.EndUtc),
            ("hours", Math.Round(b.DurationMinutes / 60m, 2)),
            ("status", b.Status.ToString()),
            ("payment", b.PaymentStatus.ToString()),
            ("amount", b.TotalAmount)))
            .ToList();

        var paidRevenue = data
            .Where(b => b.PaymentStatus is BookingPaymentStatus.Paid or BookingPaymentStatus.PartiallyRefunded)
            .Sum(b => b.TotalAmount);
        var summary = new List<ReportSummaryItem>
        {
            new("Bookings", totalRows.ToString()),
            new("Booked hours", Math.Round(data.Sum(b => b.DurationMinutes) / 60m, 1).ToString("0.#")),
            new("Paid value", $"₹ {paidRevenue:N2}"),
            new("Cancelled", data.Count(b => b.Status == BookingStatus.Cancelled).ToString())
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 2 · Revenue ─────────────────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildRevenueAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc = IstClock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var query = db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Success &&
                        p.PaidUtc >= fromUtc && p.PaidUtc < toUtc);
        if (filters.CustomerId is not null)
        {
            query = query.Where(p => p.CustomerId == filters.CustomerId);
        }
        var payments = await query
            .Select(p => new { p.PaidUtc, p.Amount, p.BookingId, p.CustomerPassId, p.ServiceOrderId })
            .ToListAsync(ct);

        var byDay = payments
            .GroupBy(p => IstClock.IstDateOf(p.PaidUtc!.Value))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Date = g.Key,
                Bookings = g.Where(p => p.BookingId != null).Sum(p => p.Amount),
                Passes = g.Where(p => p.CustomerPassId != null).Sum(p => p.Amount),
                Services = g.Where(p => p.ServiceOrderId != null).Sum(p => p.Amount),
                Wallet = g.Where(p => p.BookingId == null && p.CustomerPassId == null &&
                                      p.ServiceOrderId == null).Sum(p => p.Amount),
                Total = g.Sum(p => p.Amount)
            })
            .ToList();

        var columns = new List<ReportColumn>
        {
            new("date", "Date", "date"),
            new("bookings", "Bookings", "money"),
            new("passes", "Passes", "money"),
            new("services", "Services", "money"),
            new("wallet", "Wallet recharges", "money"),
            new("total", "Total", "money")
        };
        var rows = byDay.Select(d => Row(
            ("date", d.Date),
            ("bookings", d.Bookings),
            ("passes", d.Passes),
            ("services", d.Services),
            ("wallet", d.Wallet),
            ("total", d.Total)))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Captured payments", payments.Count.ToString()),
            new("Bookings", $"₹ {byDay.Sum(d => d.Bookings):N2}"),
            new("Passes", $"₹ {byDay.Sum(d => d.Passes):N2}"),
            new("Wallet recharges", $"₹ {byDay.Sum(d => d.Wallet):N2}"),
            new("Total captured", $"₹ {byDay.Sum(d => d.Total):N2}")
        };
        return (columns, rows, summary, byDay.Count);
    }

    // ── 3 · Customers ───────────────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildCustomerAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        // The register is not windowed by default — from/to filter the REGISTRATION
        // date only when the caller sets them explicitly.
        var query = db.Users.AsNoTracking().Where(u => u.UserType == UserType.Customer);
        if (filters.From is not null)
        {
            var fromUtc = IstClock.ToUtc(filters.From.Value.ToDateTime(TimeOnly.MinValue));
            query = query.Where(u => u.CreatedDate >= fromUtc);
        }
        if (filters.To is not null)
        {
            var toUtc = IstClock.ToUtc(filters.To.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(u => u.CreatedDate < toUtc);
        }
        if (filters.CustomerId is not null)
        {
            query = query.Where(u => u.UserId == filters.CustomerId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(u => u.UserId)
            .Take(rowCap)
            .Select(u => new
            {
                u.UserId,
                u.FullName,
                u.Email,
                u.MobileNumber,
                u.CreatedDate,
                u.Status,
                Bookings = db.Bookings.Count(b => b.CustomerId == u.UserId),
                Spend = db.Payments
                    .Where(p => p.CustomerId == u.UserId && p.Status == PaymentStatus.Success)
                    .Sum(p => (decimal?)p.Amount) ?? 0m,
                WalletBalance = db.Wallets.Where(w => w.CustomerId == u.UserId)
                    .Select(w => (decimal?)w.Balance).FirstOrDefault() ?? 0m
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("name", "Customer", "string"),
            new("email", "Email", "string"),
            new("mobile", "Mobile", "string"),
            new("registered", "Registered (IST)", "date"),
            new("bookings", "Bookings", "number"),
            new("spend", "Total spend", "money"),
            new("wallet", "Wallet balance", "money"),
            new("status", "Status", "string")
        };
        var rows = data.Select(u => Row(
            ("name", u.FullName),
            ("email", u.Email),
            ("mobile", u.MobileNumber),
            ("registered", IstClock.IstDateOf(u.CreatedDate)),
            ("bookings", u.Bookings),
            ("spend", u.Spend),
            ("wallet", u.WalletBalance),
            ("status", u.Status.ToString())))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Customers", totalRows.ToString()),
            new("With bookings", data.Count(u => u.Bookings > 0).ToString()),
            new("Total spend", $"₹ {data.Sum(u => u.Spend):N2}"),
            new("Wallet held", $"₹ {data.Sum(u => u.WalletBalance):N2}")
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 4 · Occupancy ───────────────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildOccupancyAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, CancellationToken ct)
    {
        var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc = IstClock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));

        var workspacesQuery = db.Workspaces.AsNoTracking()
            .Include(w => w.WorkspaceType)
            .Where(w => w.IsActive);
        if (filters.LocationId is not null)
        {
            workspacesQuery = workspacesQuery.Where(w => w.LocationId == filters.LocationId);
        }
        if (filters.WorkspaceTypeId is not null)
        {
            workspacesQuery = workspacesQuery.Where(w => w.WorkspaceTypeId == filters.WorkspaceTypeId);
        }
        if (filters.WorkspaceId is not null)
        {
            workspacesQuery = workspacesQuery.Where(w => w.WorkspaceId == filters.WorkspaceId);
        }
        var workspaces = await workspacesQuery.OrderBy(w => w.WorkspaceCode).ToListAsync(ct);
        var workspaceIds = workspaces.Select(w => w.WorkspaceId).ToList();

        var occupiedStatuses = new[]
            { BookingStatus.Confirmed, BookingStatus.CheckedIn, BookingStatus.Completed };
        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => workspaceIds.Contains(b.WorkspaceId) &&
                        occupiedStatuses.Contains(b.Status) &&
                        b.StartUtc < toUtc && fromUtc < b.EndUtc)
            .Select(b => new { b.WorkspaceId, b.StartUtc, b.EndUtc })
            .ToListAsync(ct);
        var blocks = await db.WorkspaceBlocks.AsNoTracking()
            .Where(k => workspaceIds.Contains(k.WorkspaceId) &&
                        k.StartUtc < toUtc && fromUtc < k.EndUtc)
            .Select(k => new { k.WorkspaceId, k.StartUtc, k.EndUtc })
            .ToListAsync(ct);

        // Available hours per workspace: the location working window × working days
        // inside the range.
        var locations = await db.Locations.AsNoTracking().ToListAsync(ct);
        var availableHoursByLocation = locations.ToDictionary(l => l.LocationId, l =>
        {
            var dailyHours = (decimal)(l.WorkingHoursEnd - l.WorkingHoursStart).TotalHours;
            var workingDays = l.WorkingDays.Split(',').ToHashSet();
            var days = 0;
            for (var d = from; d <= to; d = d.AddDays(1))
            {
                if (workingDays.Contains(d.DayOfWeek.ToString()[..3].ToUpperInvariant()))
                {
                    days++;
                }
            }
            return dailyHours * days;
        });

        decimal HoursIn(IEnumerable<(DateTime Start, DateTime End)> spans) =>
            Math.Round(spans.Sum(s =>
                (decimal)((s.End < toUtc ? s.End : toUtc) - (s.Start > fromUtc ? s.Start : fromUtc))
                .TotalMinutes) / 60m, 1);

        var bookingsByWorkspace = bookings.GroupBy(b => b.WorkspaceId)
            .ToDictionary(g => g.Key, g => g.Select(b => (b.StartUtc, b.EndUtc)).ToList());
        var blocksByWorkspace = blocks.GroupBy(k => k.WorkspaceId)
            .ToDictionary(g => g.Key, g => g.Select(k => (k.StartUtc, k.EndUtc)).ToList());

        var columns = new List<ReportColumn>
        {
            new("workspace", "Workspace", "string"),
            new("type", "Type", "string"),
            new("bookedHours", "Booked hours", "number"),
            new("blockedHours", "Blocked hours", "number"),
            new("availableHours", "Available hours", "number"),
            new("occupancy", "Occupancy", "percent")
        };
        var rows = new List<Dictionary<string, object?>>();
        decimal totalBooked = 0, totalAvailable = 0;
        foreach (var w in workspaces)
        {
            var booked = HoursIn(bookingsByWorkspace.GetValueOrDefault(w.WorkspaceId, []));
            var blocked = HoursIn(blocksByWorkspace.GetValueOrDefault(w.WorkspaceId, []));
            var available = availableHoursByLocation.GetValueOrDefault(w.LocationId, 0m);
            totalBooked += booked;
            totalAvailable += available;
            rows.Add(Row(
                ("workspace", $"{w.WorkspaceCode} · {w.WorkspaceName}"),
                ("type", w.WorkspaceType.TypeName),
                ("bookedHours", booked),
                ("blockedHours", blocked),
                ("availableHours", available),
                ("occupancy", available == 0 ? 0m : Math.Round(booked / available * 100m, 1))));
        }
        var summary = new List<ReportSummaryItem>
        {
            new("Workspaces", workspaces.Count.ToString()),
            new("Booked hours", totalBooked.ToString("0.#")),
            new("Overall occupancy",
                totalAvailable == 0 ? "0%" : $"{Math.Round(totalBooked / totalAvailable * 100m, 1)}%")
        };
        return (columns, rows, summary, rows.Count);
    }

    // ── 8 · Passes ──────────────────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildPassAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            PassType passType, CancellationToken ct)
    {
        var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc = IstClock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var query = db.CustomerPasses.AsNoTracking()
            .Where(p => p.PassType == passType && p.PurchaseUtc >= fromUtc && p.PurchaseUtc < toUtc);
        if (filters.LocationId is not null)
        {
            query = query.Where(p => p.LocationId == filters.LocationId);
        }
        if (filters.CustomerId is not null)
        {
            query = query.Where(p => p.CustomerId == filters.CustomerId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(p => p.PurchaseUtc)
            .Take(rowCap)
            .Select(p => new
            {
                p.PassNumber,
                Customer = db.Users.Where(u => u.UserId == p.CustomerId)
                    .Select(u => u.FullName).First(),
                Plan = db.PassPlans.Where(pp => pp.PassPlanId == p.PassPlanId)
                    .Select(pp => pp.PlanName).First(),
                p.PurchaseUtc,
                p.ValidFrom,
                p.ValidTo,
                p.Price,
                p.TaxAmount,
                p.TotalAmount,
                p.Status
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "Pass", "string"),
            new("customer", "Customer", "string"),
            new("plan", "Plan", "string"),
            new("purchased", "Purchased (IST)", "datetime"),
            new("validFrom", "Valid from", "date"),
            new("validTo", "Valid to", "date"),
            new("price", "Price", "money"),
            new("tax", "GST", "money"),
            new("total", "Total", "money"),
            new("status", "Status", "string")
        };
        var rows = data.Select(p => Row(
            ("number", p.PassNumber),
            ("customer", p.Customer),
            ("plan", p.Plan),
            ("purchased", p.PurchaseUtc),
            ("validFrom", p.ValidFrom),
            ("validTo", p.ValidTo),
            ("price", p.Price),
            ("tax", p.TaxAmount),
            ("total", p.TotalAmount),
            ("status", p.Status.ToString())))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Passes sold", totalRows.ToString()),
            new("Active now", data.Count(p => p.Status == CustomerPassStatus.Active).ToString()),
            new("Value", $"₹ {data.Sum(p => p.TotalAmount):N2}")
        };
        return (columns, rows, summary, totalRows);
    }

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] cells) =>
        cells.ToDictionary(c => c.Key, c => c.Value);
}
