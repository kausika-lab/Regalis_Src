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
        ("monthly-pass", "Monthly Pass Report", "Monthly passes sold in the window.", true),
        ("payment", "Payment Report", "Every payment with method, gateway and status.", true),
        ("invoice", "Invoice Report", "GST invoices with tax break-up.", true),
        ("wallet", "Wallet Report", "Wallet ledger by customer and category.", true),
        ("coffee", "Services & Coffee Report", "Service orders in the window.", true),
        ("ticket", "Support Ticket Report", "Tickets by category, status and response time.", true),
        ("check-in-out", "Check-in/out Report", "Attendance sessions in the window.", true),
        ("qr-access", "QR Access Report", "Every gate decision, granted or denied.", true),
        ("quotation", "Quotation Report", "Quotations raised, approved, rejected and converted.", true),
        ("outstanding", "Outstanding Payment Report", "Invoices still owing, with ageing.", true),
        ("expense", "Expense Report", "Daily company expenses by category and payment mode.", true),
        ("purchase-order", "Purchase Order Report", "Purchase orders with delivery status and value.", true),
        ("inventory", "Inventory Report", "Derived stock per item with value and reorder flags.", true),
        ("incoming-goods", "Incoming Goods Report", "Goods received, with supplier and purchase order.", true),
        ("outgoing-goods", "Outgoing Goods Report", "Goods issued out of stock, with purpose.", true),
        ("bank-statement", "Bank Statement Report", "Bank lines with reconciliation status.", true),
        ("finance-summary", "Financial Summary", "Quotations, invoices, payments, expenses and bank in one view.", true)
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
            "monthly-pass" => await BuildPassAsync(filters, from, to, rowCap, PassType.MonthlyPass, ct),
            "payment" => await BuildPaymentAsync(filters, from, to, rowCap, ct),
            "invoice" => await BuildInvoiceAsync(filters, from, to, rowCap, ct),
            "wallet" => await BuildWalletAsync(filters, from, to, rowCap, ct),
            "coffee" => await BuildServiceOrderAsync(filters, from, to, rowCap, ct),
            "ticket" => await BuildTicketAsync(filters, from, to, rowCap, ct),
            "check-in-out" => await BuildAttendanceAsync(filters, from, to, rowCap, ct),
            "qr-access" => await BuildAccessAsync(filters, from, to, rowCap, ct),
            "quotation" => await BuildQuotationAsync(filters, from, to, rowCap, ct),
            "outstanding" => await BuildOutstandingAsync(filters, from, to, rowCap, ct),
            "expense" => await BuildExpenseAsync(filters, from, to, rowCap, ct),
            "purchase-order" => await BuildPurchaseOrderAsync(filters, from, to, rowCap, ct),
            "inventory" => await BuildInventoryAsync(filters, ct),
            "incoming-goods" => await BuildGoodsMovementAsync(filters, from, to, rowCap,
                InventoryTransactionType.In, ct),
            "outgoing-goods" => await BuildGoodsMovementAsync(filters, from, to, rowCap,
                InventoryTransactionType.Out, ct),
            "bank-statement" => await BuildBankStatementAsync(filters, from, to, rowCap, ct),
            "finance-summary" => await BuildFinanceSummaryAsync(filters, from, to, ct),
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

    // ── 10 · Payments ───────────────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildPaymentAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc = IstClock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var query = db.Payments.AsNoTracking()
            .Where(p => p.CreatedDate >= fromUtc && p.CreatedDate < toUtc);
        if (filters.CustomerId is not null)
        {
            query = query.Where(p => p.CustomerId == filters.CustomerId);
        }
        if (filters.PaymentStatus is not null)
        {
            query = query.Where(p => (byte)p.Status == filters.PaymentStatus);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(p => p.PaymentId)
            .Take(rowCap)
            .Select(p => new
            {
                p.PaymentNumber,
                Customer = db.Users.Where(u => u.UserId == p.CustomerId)
                    .Select(u => u.FullName).First(),
                p.PaymentMethod,
                p.Gateway,
                p.Status,
                p.CreatedDate,
                p.PaidUtc,
                p.Amount,
                p.RefundedAmount
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "Payment", "string"),
            new("customer", "Customer", "string"),
            new("method", "Method", "string"),
            new("gateway", "Gateway", "string"),
            new("status", "Status", "string"),
            new("paid", "Paid (IST)", "datetime"),
            new("amount", "Amount", "money"),
            new("refunded", "Refunded", "money")
        };
        var rows = data.Select(p => Row(
            ("number", p.PaymentNumber),
            ("customer", p.Customer),
            ("method", p.PaymentMethod.ToString()),
            ("gateway", p.Gateway),
            ("status", p.Status.ToString()),
            ("paid", p.PaidUtc),
            ("amount", p.Amount),
            ("refunded", p.RefundedAmount)))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Payments", totalRows.ToString()),
            new("Captured", $"₹ {data.Where(p => p.Status == PaymentStatus.Success).Sum(p => p.Amount):N2}"),
            new("Refunded", $"₹ {data.Sum(p => p.RefundedAmount ?? 0m):N2}"),
            new("Failed", data.Count(p => p.Status == PaymentStatus.Failed).ToString())
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 11 · Invoices ───────────────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildInvoiceAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var query = db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to);
        if (filters.CustomerId is not null)
        {
            query = query.Where(i => i.CustomerId == filters.CustomerId);
        }
        if (filters.LocationId is not null)
        {
            query = query.Where(i => i.LocationId == filters.LocationId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(i => i.InvoiceId)
            .Take(rowCap)
            .Select(i => new
            {
                i.InvoiceNumber, i.CustomerName, i.InvoiceDate, i.TaxableAmount,
                i.CgstAmount, i.SgstAmount, i.TotalAmount, i.OutstandingAmount, i.Status
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "Invoice", "string"),
            new("customer", "Customer", "string"),
            new("date", "Date", "date"),
            new("taxable", "Taxable", "money"),
            new("cgst", "CGST", "money"),
            new("sgst", "SGST", "money"),
            new("total", "Total", "money"),
            new("outstanding", "Outstanding", "money"),
            new("status", "Status", "string")
        };
        var rows = data.Select(i => Row(
            ("number", i.InvoiceNumber),
            ("customer", i.CustomerName),
            ("date", i.InvoiceDate),
            ("taxable", i.TaxableAmount),
            ("cgst", i.CgstAmount),
            ("sgst", i.SgstAmount),
            ("total", i.TotalAmount),
            ("outstanding", i.OutstandingAmount),
            ("status", i.Status.ToString())))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Invoices", totalRows.ToString()),
            new("Invoiced", $"₹ {data.Sum(i => i.TotalAmount):N2}"),
            new("GST collected", $"₹ {data.Sum(i => i.CgstAmount + i.SgstAmount):N2}"),
            new("Outstanding", $"₹ {data.Sum(i => i.OutstandingAmount):N2}")
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 12 · Wallet ─────────────────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildWalletAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc = IstClock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var query = db.WalletTransactions.AsNoTracking()
            .Where(t => t.TransactionUtc >= fromUtc && t.TransactionUtc < toUtc);
        if (filters.CustomerId is not null)
        {
            query = query.Where(t => t.CustomerId == filters.CustomerId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(t => t.WalletTransactionId)
            .Take(rowCap)
            .Select(t => new
            {
                t.TransactionNumber,
                Customer = db.Users.Where(u => u.UserId == t.CustomerId)
                    .Select(u => u.FullName).First(),
                t.Direction, t.Category, t.Amount, t.BalanceAfter, t.TransactionUtc
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "Transaction", "string"),
            new("customer", "Customer", "string"),
            new("when", "When (IST)", "datetime"),
            new("direction", "Direction", "string"),
            new("category", "Category", "string"),
            new("amount", "Amount", "money"),
            new("balance", "Balance after", "money")
        };
        var rows = data.Select(t => Row(
            ("number", t.TransactionNumber),
            ("customer", t.Customer),
            ("when", t.TransactionUtc),
            ("direction", t.Direction.ToString()),
            ("category", t.Category.ToString()),
            ("amount", t.Amount),
            ("balance", t.BalanceAfter)))
            .ToList();
        var credits = data.Where(t => t.Direction == WalletDirection.Credit).Sum(t => t.Amount);
        var debits = data.Where(t => t.Direction == WalletDirection.Debit).Sum(t => t.Amount);
        var summary = new List<ReportSummaryItem>
        {
            new("Transactions", totalRows.ToString()),
            new("Credits", $"₹ {credits:N2}"),
            new("Debits", $"₹ {debits:N2}"),
            new("Net", $"₹ {credits - debits:N2}")
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 13 · Services & Coffee ──────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildServiceOrderAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc = IstClock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var query = db.ServiceOrders.AsNoTracking()
            .Where(o => o.OrderUtc >= fromUtc && o.OrderUtc < toUtc);
        if (filters.CustomerId is not null)
        {
            query = query.Where(o => o.CustomerId == filters.CustomerId);
        }
        if (filters.LocationId is not null)
        {
            query = query.Where(o => o.LocationId == filters.LocationId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(o => o.ServiceOrderId)
            .Take(rowCap)
            .Select(o => new
            {
                o.OrderNumber,
                Customer = db.Users.Where(u => u.UserId == o.CustomerId)
                    .Select(u => u.FullName).First(),
                o.OrderUtc,
                Items = o.Lines.Count,
                o.SubTotal, o.TaxAmount, o.TotalAmount, o.Status
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "Order", "string"),
            new("customer", "Customer", "string"),
            new("when", "Placed (IST)", "datetime"),
            new("items", "Items", "number"),
            new("subtotal", "Sub-total", "money"),
            new("tax", "GST", "money"),
            new("total", "Total", "money"),
            new("status", "Status", "string")
        };
        var rows = data.Select(o => Row(
            ("number", o.OrderNumber),
            ("customer", o.Customer),
            ("when", o.OrderUtc),
            ("items", o.Items),
            ("subtotal", o.SubTotal),
            ("tax", o.TaxAmount),
            ("total", o.TotalAmount),
            ("status", o.Status.ToString())))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Orders", totalRows.ToString()),
            new("Value", $"₹ {data.Sum(o => o.TotalAmount):N2}"),
            new("Delivered", data.Count(o => o.Status == ServiceOrderStatus.Delivered).ToString())
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 14 · Tickets ────────────────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildTicketAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc = IstClock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var query = db.Tickets.AsNoTracking()
            .Where(t => t.CreatedDate >= fromUtc && t.CreatedDate < toUtc);
        if (filters.CustomerId is not null)
        {
            query = query.Where(t => t.CustomerId == filters.CustomerId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(t => t.TicketId)
            .Take(rowCap)
            .Select(t => new
            {
                t.TicketNumber,
                Customer = db.Users.Where(u => u.UserId == t.CustomerId)
                    .Select(u => u.FullName).First(),
                Category = t.TicketCategory.CategoryName,
                IssueType = t.TicketIssueType.IssueTypeName,
                t.Priority, t.Status, t.CreatedDate, t.FirstResponseUtc, t.ResolvedUtc,
                Assignee = t.AssignedToUserId == null
                    ? null
                    : db.Users.Where(u => u.UserId == t.AssignedToUserId)
                        .Select(u => u.FullName).First()
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "Ticket", "string"),
            new("customer", "Customer", "string"),
            new("category", "Category", "string"),
            new("issue", "Issue type", "string"),
            new("priority", "Priority", "string"),
            new("status", "Status", "string"),
            new("raised", "Raised (IST)", "datetime"),
            new("responseHrs", "First response (h)", "number"),
            new("assignee", "Assignee", "string")
        };
        var rows = data.Select(t => Row(
            ("number", t.TicketNumber),
            ("customer", t.Customer),
            ("category", t.Category),
            ("issue", t.IssueType),
            ("priority", t.Priority.ToString()),
            ("status", t.Status.ToString()),
            ("raised", t.CreatedDate),
            ("responseHrs", t.FirstResponseUtc is null
                ? null
                : (object)Math.Round((decimal)(t.FirstResponseUtc.Value - t.CreatedDate).TotalHours, 1)),
            ("assignee", t.Assignee)))
            .ToList();
        var responded = data.Where(t => t.FirstResponseUtc is not null).ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Tickets", totalRows.ToString()),
            new("Open now", data.Count(t => t.Status is not (TicketStatus.Resolved or TicketStatus.Closed))
                .ToString()),
            new("Resolved/closed", data.Count(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed)
                .ToString()),
            new("Avg first response", responded.Count == 0
                ? "—"
                : $"{responded.Average(t => (t.FirstResponseUtc!.Value - t.CreatedDate).TotalHours):0.#} h")
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 15 · Check-in / check-out ───────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildAttendanceAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var query = db.Attendances.AsNoTracking()
            .Where(a => a.AttendanceDate >= from && a.AttendanceDate <= to);
        if (filters.CustomerId is not null)
        {
            query = query.Where(a => a.UserId == filters.CustomerId);
        }
        if (filters.LocationId is not null)
        {
            query = query.Where(a => a.LocationId == filters.LocationId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(a => a.AttendanceId)
            .Take(rowCap)
            .Select(a => new
            {
                Person = db.Users.Where(u => u.UserId == a.UserId).Select(u => u.FullName).First(),
                a.SubjectType, a.AttendanceDate, a.CheckInUtc, a.CheckOutUtc,
                a.TotalMinutes, a.CheckInMethod, a.IsCorrected
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("person", "Person", "string"),
            new("kind", "Kind", "string"),
            new("date", "Date", "date"),
            new("in", "In (IST)", "datetime"),
            new("out", "Out (IST)", "datetime"),
            new("hours", "Hours", "number"),
            new("method", "Method", "string"),
            new("corrected", "Corrected", "string")
        };
        var rows = data.Select(a => Row(
            ("person", a.Person),
            ("kind", a.SubjectType.ToString()),
            ("date", a.AttendanceDate),
            ("in", a.CheckInUtc),
            ("out", a.CheckOutUtc),
            ("hours", a.TotalMinutes is null ? null : (object)Math.Round(a.TotalMinutes.Value / 60m, 1)),
            ("method", a.CheckInMethod.ToString()),
            ("corrected", a.IsCorrected ? "Yes" : "")))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Sessions", totalRows.ToString()),
            new("Total hours", Math.Round(data.Sum(a => a.TotalMinutes ?? 0) / 60m, 1).ToString("0.#")),
            new("Still open", data.Count(a => a.CheckOutUtc is null).ToString()),
            new("Corrected", data.Count(a => a.IsCorrected).ToString())
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 16 · QR access ──────────────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildAccessAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc = IstClock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var query = db.AccessLogs.AsNoTracking()
            .Where(l => l.LoggedUtc >= fromUtc && l.LoggedUtc < toUtc);
        if (filters.CustomerId is not null)
        {
            query = query.Where(l => l.UserId == filters.CustomerId);
        }
        if (filters.LocationId is not null)
        {
            query = query.Where(l => l.LocationId == filters.LocationId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(l => l.AccessLogId)
            .Take(rowCap)
            .Select(l => new
            {
                l.LoggedUtc,
                Person = l.UserId == null
                    ? null
                    : db.Users.Where(u => u.UserId == l.UserId).Select(u => u.FullName).First(),
                l.Direction, l.AccessMethod, l.Result, l.DenyReason, l.DeviceId, l.IsManualOverride
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("when", "When (IST)", "datetime"),
            new("person", "Person", "string"),
            new("direction", "Direction", "string"),
            new("method", "Method", "string"),
            new("result", "Result", "string"),
            new("reason", "Deny reason", "string"),
            new("device", "Device", "string"),
            new("override", "Override", "string")
        };
        var rows = data.Select(l => Row(
            ("when", l.LoggedUtc),
            ("person", l.Person),
            ("direction", l.Direction.ToString()),
            ("method", l.AccessMethod.ToString()),
            ("result", l.Result.ToString()),
            ("reason", l.DenyReason),
            ("device", l.DeviceId),
            ("override", l.IsManualOverride ? "Yes" : "")))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Attempts", totalRows.ToString()),
            new("Granted", data.Count(l => l.Result == AccessResult.Granted).ToString()),
            new("Denied", data.Count(l => l.Result == AccessResult.Denied).ToString()),
            new("Device errors", data.Count(l => l.Result == AccessResult.DeviceError).ToString()),
            new("Overrides", data.Count(l => l.IsManualOverride).ToString())
        };
        return (columns, rows, summary, totalRows);
    }

    private static Dictionary<string, object?> Row(params (string Key, object? Value)[] cells) =>
        cells.ToDictionary(c => c.Key, c => c.Value);

    // ── 17 · Quotation report ───────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildQuotationAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var query = db.Quotations.AsNoTracking()
            .Where(q => q.QuotationDate >= from && q.QuotationDate <= to);
        if (filters.CustomerId is not null)
        {
            query = query.Where(q => q.CustomerId == filters.CustomerId);
        }
        if (filters.LocationId is not null)
        {
            query = query.Where(q => q.LocationId == filters.LocationId);
        }
        if (filters.WorkspaceTypeId is not null)
        {
            // "Service" in the document's filter list: a quotation matches if any of
            // its lines is for that workspace type.
            query = query.Where(q => q.Lines.Any(l => l.WorkspaceTypeId == filters.WorkspaceTypeId));
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(q => q.QuotationId)
            .Take(rowCap)
            .Select(q => new
            {
                q.QuotationNumber, q.CustomerName, q.QuotationDate, q.ValidUntil,
                q.TaxableAmount, q.TotalAmount, q.Status,
                InvoiceNumber = db.Invoices.Where(i => i.InvoiceId == q.InvoiceId)
                    .Select(i => i.InvoiceNumber).FirstOrDefault()
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "Quotation", "string"),
            new("customer", "Customer", "string"),
            new("date", "Date", "date"),
            new("validUntil", "Valid until", "date"),
            new("taxable", "Taxable", "money"),
            new("total", "Total", "money"),
            new("status", "Status", "string"),
            new("invoice", "Invoice", "string")
        };
        var rows = data.Select(q => Row(
            ("number", q.QuotationNumber),
            ("customer", q.CustomerName),
            ("date", q.QuotationDate),
            ("validUntil", q.ValidUntil),
            ("taxable", q.TaxableAmount),
            ("total", q.TotalAmount),
            ("status", q.Status.ToString()),
            ("invoice", q.InvoiceNumber)))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Quotations", totalRows.ToString()),
            new("Quoted", $"₹ {data.Sum(q => q.TotalAmount):N2}"),
            new("Approved", data.Count(q => q.Status == QuotationStatus.Approved).ToString()),
            new("Rejected", data.Count(q => q.Status == QuotationStatus.Rejected).ToString()),
            new("Converted", $"₹ {data.Where(q => q.InvoiceNumber != null).Sum(q => q.TotalAmount):N2}")
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 18 · Outstanding payment report ─────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildOutstandingAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var todayIst = IstClock.IstDateOf(clock.UtcNow);

        // Money still owed: the same predicate InvoiceDto.IsOverdue uses, minus the
        // due-date test, because this report covers everything unpaid, overdue or not.
        var query = db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to
                        && i.OutstandingAmount > 0
                        && i.Status != InvoiceStatus.Cancelled
                        && i.Status != InvoiceStatus.Refunded);
        if (filters.CustomerId is not null)
        {
            query = query.Where(i => i.CustomerId == filters.CustomerId);
        }
        if (filters.LocationId is not null)
        {
            query = query.Where(i => i.LocationId == filters.LocationId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(i => i.DueDate)
            .Take(rowCap)
            .Select(i => new
            {
                i.InvoiceNumber, i.CustomerName, i.CustomerEmail, i.InvoiceDate, i.DueDate,
                i.TotalAmount, i.PaidAmount, i.OutstandingAmount, i.Status
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "Invoice", "string"),
            new("customer", "Customer", "string"),
            new("email", "Email", "string"),
            new("date", "Invoice date", "date"),
            new("due", "Due date", "date"),
            new("ageing", "Days overdue", "number"),
            new("total", "Total", "money"),
            new("paid", "Paid", "money"),
            new("outstanding", "Outstanding", "money"),
            new("status", "Status", "string")
        };
        var rows = data.Select(i => Row(
            ("number", i.InvoiceNumber),
            ("customer", i.CustomerName),
            ("email", i.CustomerEmail),
            ("date", i.InvoiceDate),
            ("due", i.DueDate),
            ("ageing", Math.Max(0, i.DueDate.DayNumber < todayIst.DayNumber
                ? todayIst.DayNumber - i.DueDate.DayNumber : 0)),
            ("total", i.TotalAmount),
            ("paid", i.PaidAmount),
            ("outstanding", i.OutstandingAmount),
            ("status", i.Status.ToString())))
            .ToList();
        var overdue = data.Where(i => i.DueDate < todayIst).ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Invoices owing", totalRows.ToString()),
            new("Total outstanding", $"₹ {data.Sum(i => i.OutstandingAmount):N2}"),
            new("Overdue invoices", overdue.Count.ToString()),
            new("Overdue value", $"₹ {overdue.Sum(i => i.OutstandingAmount):N2}")
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 19 · Expense report ─────────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildExpenseAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var query = db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to);
        if (filters.LocationId is not null)
        {
            query = query.Where(e => e.LocationId == filters.LocationId);
        }
        if (!string.IsNullOrWhiteSpace(filters.Category))
        {
            query = query.Where(e => e.Category == filters.Category);
        }
        if (filters.PaymentMethod is not null)
        {
            query = query.Where(e => (byte)e.PaymentMethod == filters.PaymentMethod);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(e => e.ExpenseDate).ThenBy(e => e.ExpenseId)
            .Take(rowCap)
            .Select(e => new
            {
                e.ExpenseNumber, e.ExpenseDate, e.Category, e.Description, e.Amount,
                e.AdjustmentTotal, NetAmount = e.Amount + e.AdjustmentTotal,
                e.PaymentMethod, e.Remarks,
                EnteredBy = db.Users.Where(u => u.UserId == e.EnteredByUserId)
                    .Select(u => u.FullName).FirstOrDefault(),
                Reconciled = db.BankTransactions.Any(b => b.MatchedExpenseId == e.ExpenseId)
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "Voucher", "string"),
            new("date", "Date", "date"),
            new("category", "Category", "string"),
            new("description", "Description", "string"),
            new("amount", "Amount", "money"),
            new("adjustments", "Adjustments", "money"),
            new("net", "Net amount", "money"),
            new("mode", "Payment mode", "string"),
            new("enteredBy", "Entered by", "string"),
            new("reconciled", "Bank matched", "string"),
            new("remarks", "Remarks", "string")
        };
        var rows = data.Select(e => Row(
            ("number", e.ExpenseNumber),
            ("date", e.ExpenseDate),
            ("category", e.Category),
            ("description", e.Description),
            ("amount", e.Amount),
            ("adjustments", e.AdjustmentTotal),
            ("net", e.NetAmount),
            ("mode", e.PaymentMethod.ToString()),
            ("enteredBy", e.EnteredBy),
            ("reconciled", e.Reconciled ? "Yes" : "No"),
            ("remarks", e.Remarks)))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Entries", totalRows.ToString()),
            new("Total spend", $"₹ {data.Sum(e => e.NetAmount):N2}"),
            new("Categories", data.Select(e => e.Category).Distinct().Count().ToString()),
            new("Unmatched", data.Count(e => !e.Reconciled).ToString())
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 20 · Purchase order report ──────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildPurchaseOrderAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var query = db.PurchaseOrders.AsNoTracking()
            .Where(p => p.PoDate >= from && p.PoDate <= to);
        if (filters.LocationId is not null)
        {
            query = query.Where(p => p.LocationId == filters.LocationId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(p => p.PurchaseOrderId)
            .Take(rowCap)
            .Select(p => new
            {
                p.PoNumber, p.PoDate, p.ExpectedDeliveryDate, p.TaxableAmount, p.TaxAmount,
                p.TotalAmount, p.Status,
                SupplierName = db.Suppliers.Where(s => s.SupplierId == p.SupplierId)
                    .Select(s => s.SupplierName).FirstOrDefault(),
                LineCount = p.Lines.Count,
                Pending = p.Lines.Sum(l => l.Quantity - l.ReceivedQuantity)
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("number", "PO", "string"),
            new("supplier", "Supplier", "string"),
            new("date", "PO date", "date"),
            new("expected", "Expected", "date"),
            new("lines", "Lines", "number"),
            new("pending", "Pending qty", "number"),
            new("taxable", "Taxable", "money"),
            new("tax", "Tax", "money"),
            new("total", "Total", "money"),
            new("status", "Status", "string")
        };
        var rows = data.Select(p => Row(
            ("number", p.PoNumber),
            ("supplier", p.SupplierName),
            ("date", p.PoDate),
            ("expected", p.ExpectedDeliveryDate),
            ("lines", p.LineCount),
            ("pending", p.Pending),
            ("taxable", p.TaxableAmount),
            ("tax", p.TaxAmount),
            ("total", p.TotalAmount),
            ("status", p.Status.ToString())))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Purchase orders", totalRows.ToString()),
            new("PO value", $"₹ {data.Sum(p => p.TotalAmount):N2}"),
            new("Awaiting delivery",
                data.Count(p => p.Status is PurchaseOrderStatus.Sent
                    or PurchaseOrderStatus.PartiallyReceived).ToString()),
            new("Cancelled", data.Count(p => p.Status == PurchaseOrderStatus.Cancelled).ToString())
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 21 · Inventory report (derived stock) ───────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildInventoryAsync(ReportFilterRequest filters, CancellationToken ct)
    {
        // Stock by quantity: asset units are reported on Assets → Current Stock by room.
        var itemsQuery = db.InventoryItems.AsNoTracking().Where(i => i.Kind != ItemKind.Asset);
        if (filters.LocationId is not null)
        {
            itemsQuery = itemsQuery.Where(i => i.LocationId == filters.LocationId);
        }
        if (!string.IsNullOrWhiteSpace(filters.Category))
        {
            itemsQuery = itemsQuery.Where(i => i.Category == filters.Category);
        }
        var items = await itemsQuery.OrderBy(i => i.ItemName).ToListAsync(ct);

        // Stock is derived from the ledger (Invariant 11) — the report reads it the
        // same way the stock screen does, so the two can never disagree.
        var itemIds = items.Select(i => i.InventoryItemId).ToList();
        var sums = await db.InventoryTransactions.AsNoTracking()
            .Where(t => itemIds.Contains(t.InventoryItemId))
            .GroupBy(t => new { t.InventoryItemId, t.TransactionType })
            .Select(g => new { g.Key.InventoryItemId, g.Key.TransactionType, Total = g.Sum(t => t.Quantity) })
            .ToListAsync(ct);

        decimal SumOf(int itemId, InventoryTransactionType type) =>
            sums.Where(s => s.InventoryItemId == itemId && s.TransactionType == type).Sum(s => s.Total);

        var rowsData = items.Select(i =>
        {
            var totalIn = SumOf(i.InventoryItemId, InventoryTransactionType.In)
                          + SumOf(i.InventoryItemId, InventoryTransactionType.OpeningBalance);
            var totalOut = SumOf(i.InventoryItemId, InventoryTransactionType.Out);
            var damage = SumOf(i.InventoryItemId, InventoryTransactionType.Damage);
            var adjustment = SumOf(i.InventoryItemId, InventoryTransactionType.Adjustment);
            var current = i.OpeningQuantity + totalIn + adjustment - totalOut - damage;
            return new
            {
                i.ItemCode, i.ItemName, i.Category, i.Unit, i.OpeningQuantity,
                TotalIn = totalIn, TotalOut = totalOut, Damage = damage, Adjustment = adjustment,
                Current = current, i.ReorderLevel, i.UnitCost,
                Value = current * (i.UnitCost ?? 0m)
            };
        }).ToList();

        var columns = new List<ReportColumn>
        {
            new("code", "Code", "string"),
            new("item", "Item", "string"),
            new("category", "Category", "string"),
            new("unit", "Unit", "string"),
            new("opening", "Opening", "number"),
            new("in", "In", "number"),
            new("out", "Out", "number"),
            new("damage", "Damage", "number"),
            new("adjustment", "Adjustment", "number"),
            new("current", "Current stock", "number"),
            new("reorder", "Reorder level", "number"),
            new("value", "Stock value", "money")
        };
        var rows = rowsData.Select(r => Row(
            ("code", r.ItemCode),
            ("item", r.ItemName),
            ("category", r.Category),
            ("unit", r.Unit),
            ("opening", r.OpeningQuantity),
            ("in", r.TotalIn),
            ("out", r.TotalOut),
            ("damage", r.Damage),
            ("adjustment", r.Adjustment),
            ("current", r.Current),
            ("reorder", r.ReorderLevel),
            ("value", r.Value)))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new("Items", rowsData.Count.ToString()),
            new("Stock value", $"₹ {rowsData.Sum(r => r.Value):N2}"),
            new("Below reorder", rowsData.Count(r => r.Current <= r.ReorderLevel).ToString())
        };
        return (columns, rows, summary, rowsData.Count);
    }

    // ── 22, 23 · Incoming and outgoing goods ────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildGoodsMovementAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            InventoryTransactionType type, CancellationToken ct)
    {
        var query = db.InventoryTransactions.AsNoTracking()
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to
                        && t.TransactionType == type);
        if (filters.LocationId is not null)
        {
            query = query.Where(t => t.LocationId == filters.LocationId);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(t => t.TransactionDate).ThenBy(t => t.InventoryTransactionId)
            .Take(rowCap)
            .Select(t => new
            {
                t.TransactionNumber, t.TransactionDate, t.Quantity, t.UnitCost,
                t.SourceOrDestination, t.ReferenceNumber, t.PurchaseInvoiceNumber, t.Notes,
                ItemCode = db.InventoryItems.Where(i => i.InventoryItemId == t.InventoryItemId)
                    .Select(i => i.ItemCode).FirstOrDefault(),
                ItemName = db.InventoryItems.Where(i => i.InventoryItemId == t.InventoryItemId)
                    .Select(i => i.ItemName).FirstOrDefault(),
                Unit = db.InventoryItems.Where(i => i.InventoryItemId == t.InventoryItemId)
                    .Select(i => i.Unit).FirstOrDefault(),
                SupplierName = db.Suppliers.Where(s => s.SupplierId == t.SupplierId)
                    .Select(s => s.SupplierName).FirstOrDefault(),
                PoNumber = db.PurchaseOrders.Where(p => p.PurchaseOrderId == t.PurchaseOrderId)
                    .Select(p => p.PoNumber).FirstOrDefault(),
                EnteredBy = db.Users.Where(u => u.UserId == t.EnteredByUserId)
                    .Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync(ct);

        var incoming = type == InventoryTransactionType.In;
        var columns = new List<ReportColumn>
        {
            new("number", "Reference", "string"),
            new("date", "Date", "date"),
            new("code", "Code", "string"),
            new("item", "Item", "string"),
            new("quantity", "Quantity", "number"),
            new("unit", "Unit", "string"),
            new("party", incoming ? "Supplier" : "Issued to", "string"),
            new("doc", incoming ? "PO / invoice" : "Reference", "string"),
            new("value", "Value", "money"),
            new("enteredBy", "Entered by", "string"),
            new("notes", incoming ? "Remarks" : "Purpose", "string")
        };
        var rows = data.Select(t => Row(
            ("number", t.TransactionNumber),
            ("date", t.TransactionDate),
            ("code", t.ItemCode),
            ("item", t.ItemName),
            ("quantity", t.Quantity),
            ("unit", t.Unit),
            ("party", t.SupplierName ?? t.SourceOrDestination),
            ("doc", incoming
                ? string.Join(" · ", new[] { t.PoNumber, t.PurchaseInvoiceNumber }
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
                : t.ReferenceNumber),
            ("value", t.Quantity * (t.UnitCost ?? 0m)),
            ("enteredBy", t.EnteredBy),
            ("notes", t.Notes)))
            .ToList();
        var summary = new List<ReportSummaryItem>
        {
            new(incoming ? "Receipts" : "Issues", totalRows.ToString()),
            new("Total quantity", $"{data.Sum(t => t.Quantity):N3}"),
            new("Total value", $"₹ {data.Sum(t => t.Quantity * (t.UnitCost ?? 0m)):N2}")
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 24 · Bank statement report ──────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildBankStatementAsync(ReportFilterRequest filters, DateOnly from, DateOnly to, int rowCap,
            CancellationToken ct)
    {
        var query = db.BankTransactions.AsNoTracking()
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to);
        if (filters.LocationId is not null)
        {
            query = query.Where(t => t.LocationId == filters.LocationId);
        }
        if (!string.IsNullOrWhiteSpace(filters.Category))
        {
            query = query.Where(t => t.Category == filters.Category);
        }

        var totalRows = await query.CountAsync(ct);
        var data = await query
            .OrderBy(t => t.TransactionDate).ThenBy(t => t.BankTransactionId)
            .Take(rowCap)
            .Select(t => new
            {
                t.TransactionDate, t.Direction, t.Amount, t.Narration, t.ReferenceNumber,
                t.Category, t.ReconciliationStatus,
                BankName = db.BankAccounts.Where(a => a.BankAccountId == t.BankAccountId)
                    .Select(a => a.BankName).FirstOrDefault(),
                AccountCode = db.BankAccounts.Where(a => a.BankAccountId == t.BankAccountId)
                    .Select(a => a.AccountCode).FirstOrDefault(),
                MatchedTo = db.Payments.Where(p => p.PaymentId == t.MatchedPaymentId)
                    .Select(p => p.PaymentNumber).FirstOrDefault()
                    ?? db.Expenses.Where(e => e.ExpenseId == t.MatchedExpenseId)
                        .Select(e => e.ExpenseNumber).FirstOrDefault()
            })
            .ToListAsync(ct);

        var columns = new List<ReportColumn>
        {
            new("date", "Date", "date"),
            new("bank", "Bank", "string"),
            new("account", "Account", "string"),
            new("type", "Type", "string"),
            new("credit", "Credit", "money"),
            new("debit", "Debit", "money"),
            new("narration", "Narration", "string"),
            new("reference", "Reference", "string"),
            new("category", "Category", "string"),
            new("status", "Reconciliation", "string"),
            new("matched", "Matched to", "string")
        };
        var rows = data.Select(t => Row(
            ("date", t.TransactionDate),
            ("bank", t.BankName),
            ("account", t.AccountCode),
            ("type", t.Direction.ToString()),
            ("credit", t.Direction == BankTransactionDirection.Credit ? t.Amount : 0m),
            ("debit", t.Direction == BankTransactionDirection.Debit ? t.Amount : 0m),
            ("narration", t.Narration),
            ("reference", t.ReferenceNumber),
            ("category", t.Category),
            ("status", t.ReconciliationStatus.ToString()),
            ("matched", t.MatchedTo)))
            .ToList();
        var credits = data.Where(t => t.Direction == BankTransactionDirection.Credit).Sum(t => t.Amount);
        var debits = data.Where(t => t.Direction == BankTransactionDirection.Debit).Sum(t => t.Amount);
        var summary = new List<ReportSummaryItem>
        {
            new("Lines", totalRows.ToString()),
            new("Total credit", $"₹ {credits:N2}"),
            new("Total debit", $"₹ {debits:N2}"),
            new("Net movement", $"₹ {credits - debits:N2}"),
            new("Reconciled",
                data.Count(t => t.ReconciliationStatus == ReconciliationStatus.Reconciled).ToString()),
            new("Unreconciled",
                data.Count(t => t.ReconciliationStatus != ReconciliationStatus.Reconciled).ToString())
        };
        return (columns, rows, summary, totalRows);
    }

    // ── 25 · Financial summary ──────────────────────────────────────────────

    private async Task<(List<ReportColumn>, List<Dictionary<string, object?>>, List<ReportSummaryItem>, int)>
        BuildFinanceSummaryAsync(ReportFilterRequest filters, DateOnly from, DateOnly to,
            CancellationToken ct)
    {
        var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
        var toUtc = IstClock.ToUtc(to.ToDateTime(TimeOnly.MaxValue));
        var location = filters.LocationId;

        var quotations = await db.Quotations.AsNoTracking()
            .Where(q => q.QuotationDate >= from && q.QuotationDate <= to
                        && (location == null || q.LocationId == location))
            .Select(q => new { q.TotalAmount, q.Status })
            .ToListAsync(ct);

        var invoices = await db.Invoices.AsNoTracking()
            .Where(i => i.InvoiceDate >= from && i.InvoiceDate <= to
                        && (location == null || i.LocationId == location))
            .Select(i => new { i.TotalAmount, i.PaidAmount, i.OutstandingAmount, i.Status })
            .ToListAsync(ct);

        var received = await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Success
                        && p.PaidUtc >= fromUtc && p.PaidUtc <= toUtc)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var expenses = await db.Expenses.AsNoTracking()
            .Where(e => e.ExpenseDate >= from && e.ExpenseDate <= to
                        && (location == null || e.LocationId == location))
            .SumAsync(e => (decimal?)(e.Amount + e.AdjustmentTotal), ct) ?? 0m;

        var poValue = await db.PurchaseOrders.AsNoTracking()
            .Where(p => p.PoDate >= from && p.PoDate <= to
                        && p.Status != PurchaseOrderStatus.Cancelled
                        && (location == null || p.LocationId == location))
            .SumAsync(p => (decimal?)p.TotalAmount, ct) ?? 0m;

        var bank = await db.BankTransactions.AsNoTracking()
            .Where(t => t.TransactionDate >= from && t.TransactionDate <= to
                        && (location == null || t.LocationId == location))
            .Select(t => new { t.Direction, t.Amount, t.ReconciliationStatus })
            .ToListAsync(ct);

        // One row per line of the business, so the summary reads like a statement
        // rather than a wall of cards — and exports to Excel and PDF unchanged.
        var columns = new List<ReportColumn>
        {
            new("area", "Area", "string"),
            new("metric", "Measure", "string"),
            new("count", "Count", "number"),
            new("value", "Value", "money")
        };

        var bankCredits = bank.Where(b => b.Direction == BankTransactionDirection.Credit).Sum(b => b.Amount);
        var bankDebits = bank.Where(b => b.Direction == BankTransactionDirection.Debit).Sum(b => b.Amount);

        var rows = new List<Dictionary<string, object?>>
        {
            Row(("area", "Quotations"), ("metric", "Raised"), ("count", quotations.Count),
                ("value", quotations.Sum(q => q.TotalAmount))),
            Row(("area", "Quotations"), ("metric", "Approved"),
                ("count", quotations.Count(q => q.Status == QuotationStatus.Approved)),
                ("value", quotations.Where(q => q.Status == QuotationStatus.Approved)
                    .Sum(q => q.TotalAmount))),
            Row(("area", "Invoices"), ("metric", "Issued"), ("count", invoices.Count),
                ("value", invoices.Sum(i => i.TotalAmount))),
            Row(("area", "Invoices"), ("metric", "Paid"),
                ("count", invoices.Count(i => i.Status == InvoiceStatus.Paid)),
                ("value", invoices.Sum(i => i.PaidAmount))),
            Row(("area", "Invoices"), ("metric", "Outstanding"),
                ("count", invoices.Count(i => i.OutstandingAmount > 0)),
                ("value", invoices.Sum(i => i.OutstandingAmount))),
            Row(("area", "Payments"), ("metric", "Received"), ("count", null),
                ("value", received)),
            Row(("area", "Expenses"), ("metric", "Recorded"), ("count", null),
                ("value", expenses)),
            Row(("area", "Purchase orders"), ("metric", "Committed"), ("count", null),
                ("value", poValue)),
            Row(("area", "Bank"), ("metric", "Credits"),
                ("count", bank.Count(b => b.Direction == BankTransactionDirection.Credit)),
                ("value", bankCredits)),
            Row(("area", "Bank"), ("metric", "Debits"),
                ("count", bank.Count(b => b.Direction == BankTransactionDirection.Debit)),
                ("value", bankDebits)),
            Row(("area", "Bank"), ("metric", "Reconciled"),
                ("count", bank.Count(b => b.ReconciliationStatus == ReconciliationStatus.Reconciled)),
                ("value", null))
        };

        var summary = new List<ReportSummaryItem>
        {
            new("Money in", $"₹ {received:N2}"),
            new("Money out", $"₹ {expenses:N2}"),
            new("Net", $"₹ {received - expenses:N2}"),
            new("Outstanding", $"₹ {invoices.Sum(i => i.OutstandingAmount):N2}")
        };
        return (columns, rows, summary, rows.Count);
    }
}
