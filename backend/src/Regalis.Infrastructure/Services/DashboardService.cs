using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Reporting;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The dashboard aggregates (API_PLAN §12) — each portal's summary in one round
/// trip. Counts are computed with EF aggregate queries rather than the planned
/// usp_GetDashboardSummary; same shape, no stored-proc deployment step.
/// </summary>
public class DashboardService(
    RegalisDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IDashboardService
{
    public async Task<AdminDashboardDto> GetAdminAsync(CancellationToken ct = default)
    {
        // DASHBOARD.VIEW is held by customers too (their own home) — the operational
        // aggregates are admin-portal only (Day 11 hardening finding).
        if (currentUser.UserType == UserType.Customer)
        {
            throw new ForbiddenException();
        }
        var nowUtc = clock.UtcNow;
        var todayIst = IstClock.IstDateOf(nowUtc);
        var monthStartIst = new DateOnly(todayIst.Year, todayIst.Month, 1);
        var todayStartUtc = IstClock.ToUtc(todayIst.ToDateTime(TimeOnly.MinValue));
        var todayEndUtc = IstClock.ToUtc(todayIst.AddDays(1).ToDateTime(TimeOnly.MinValue));
        var monthStartUtc = IstClock.ToUtc(monthStartIst.ToDateTime(TimeOnly.MinValue));

        var bookingsToday = await db.Bookings.CountAsync(b =>
            b.BookingDate == todayIst && b.Status != BookingStatus.Cancelled &&
            b.Status != BookingStatus.Expired, ct);
        var pendingHolds = await db.Bookings.CountAsync(b => b.Status == BookingStatus.Pending, ct);
        var checkedInNow = await db.Attendances.CountAsync(a =>
            a.Status == AttendanceStatus.CheckedIn, ct);
        var revenueToday = await db.Payments
            .Where(p => p.Status == PaymentStatus.Success &&
                        p.PaidUtc >= todayStartUtc && p.PaidUtc < todayEndUtc)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var revenueMonth = await db.Payments
            .Where(p => p.Status == PaymentStatus.Success && p.PaidUtc >= monthStartUtc)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;
        var activePasses = await db.CustomerPasses.CountAsync(p =>
            p.Status == CustomerPassStatus.Active &&
            p.ValidFrom <= todayIst && todayIst <= p.ValidTo, ct);
        var openTickets = await db.Tickets.CountAsync(t =>
            t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed, ct);
        var customers = await db.Users.CountAsync(u => u.UserType == UserType.Customer, ct);
        var housekeepingToday = await db.HousekeepingSchedules.CountAsync(h =>
            h.ScheduledDate == todayIst &&
            (h.Status == HousekeepingStatus.Scheduled || h.Status == HousekeepingStatus.InProgress), ct);

        // Today's occupancy: booked hours ÷ (bookable workspaces × the default
        // location's working window). An indicator, not an invoice.
        var bookedMinutesToday = await db.Bookings
            .Where(b => Booking.OccupancyStatuses.Contains(b.Status) &&
                        b.StartUtc < todayEndUtc && todayStartUtc < b.EndUtc)
            .SumAsync(b => (int?)b.DurationMinutes, ct) ?? 0;
        var bookableWorkspaces = await db.Workspaces.CountAsync(w => w.IsBookable, ct);
        var defaultLocation = await db.Locations.AsNoTracking().FirstAsync(l => l.IsDefault, ct);
        var dailyHours = (decimal)(defaultLocation.WorkingHoursEnd - defaultLocation.WorkingHoursStart)
            .TotalHours;
        var occupancyPercent = bookableWorkspaces == 0 || dailyHours == 0
            ? 0m
            : Math.Round(bookedMinutesToday / 60m / (bookableWorkspaces * dailyHours) * 100m, 1);

        var cards = new List<DashboardCard>
        {
            new("bookings-today", "Bookings today", bookingsToday.ToString(),
                pendingHolds > 0 ? $"{pendingHolds} pending hold{(pendingHolds > 1 ? "s" : "")}" : null,
                "/admin/bookings"),
            new("occupancy", "Occupancy today", $"{occupancyPercent:0.#}%",
                $"{bookableWorkspaces} bookable workspaces", "/admin/calendar"),
            new("checked-in", "Checked in now", checkedInNow.ToString(), null, "/admin/attendance"),
            new("revenue-today", "Revenue today", $"₹ {revenueToday:N0}", null, "/admin/payments"),
            new("revenue-month", "Revenue this month", $"₹ {revenueMonth:N0}",
                $"since {monthStartIst:dd MMM}", "/admin/payments"),
            new("active-passes", "Active passes", activePasses.ToString(), null, "/admin/passes/day"),
            new("open-tickets", "Open tickets", openTickets.ToString(), null, "/admin/tickets"),
            new("customers", "Customers", customers.ToString(), null, "/admin/customers"),
            new("housekeeping-today", "Housekeeping today", housekeepingToday.ToString(),
                null, "/admin/housekeeping")
        };
        return new AdminDashboardDto(cards, nowUtc);
    }

    public async Task<CustomerDashboardDto> GetCustomerAsync(CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        var todayIst = IstClock.IstDateOf(nowUtc);
        var userId = currentUser.UserId;

        var walletBalance = await db.Wallets
            .Where(w => w.CustomerId == userId)
            .Select(w => (decimal?)w.Balance)
            .FirstOrDefaultAsync(ct) ?? 0m;
        var activePasses = await db.CustomerPasses.CountAsync(p =>
            p.CustomerId == userId && p.Status == CustomerPassStatus.Active &&
            p.ValidFrom <= todayIst && todayIst <= p.ValidTo, ct);
        var unreadNotifications = await db.Notifications.CountAsync(n =>
            n.UserId == userId && !n.IsRead, ct);
        var openTickets = await db.Tickets.CountAsync(t =>
            t.CustomerId == userId &&
            t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed, ct);

        var todaysBooking = await ShapeBookings(db.Bookings.AsNoTracking()
                .Where(b => b.CustomerId == userId && b.BookingDate == todayIst &&
                            Booking.OccupancyStatuses.Contains(b.Status))
                .OrderBy(b => b.StartUtc))
            .FirstOrDefaultAsync(ct);
        var nextBooking = await ShapeBookings(db.Bookings.AsNoTracking()
                .Where(b => b.CustomerId == userId && b.StartUtc > nowUtc &&
                            b.BookingDate != todayIst &&
                            Booking.OccupancyStatuses.Contains(b.Status))
                .OrderBy(b => b.StartUtc))
            .FirstOrDefaultAsync(ct);

        return new CustomerDashboardDto(walletBalance, activePasses, unreadNotifications,
            openTickets, todaysBooking, nextBooking);
    }

    private static IQueryable<DashboardBookingDto> ShapeBookings(IQueryable<Booking> query) =>
        query.Select(b => new DashboardBookingDto(
            b.BookingId, b.BookingNumber, b.Workspace.WorkspaceName,
            b.Workspace.WorkspaceType.TypeName, b.StartUtc, b.EndUtc, (byte)b.Status));

    /// <summary>
    /// The Billing &amp; Finance dashboard (document §16). Kept separate from the
    /// operational dashboard so the two do not compete for space, and gated on
    /// PAYMENT.VIEW rather than DASHBOARD.VIEW — customers hold the latter for their
    /// own home screen and must never see company money.
    /// </summary>
    public async Task<AdminDashboardDto> GetFinanceAsync(CancellationToken ct = default)
    {
        if (currentUser.UserType == UserType.Customer)
        {
            throw new ForbiddenException();
        }

        var nowUtc = clock.UtcNow;
        var todayIst = IstClock.IstDateOf(nowUtc);
        var monthStartIst = new DateOnly(todayIst.Year, todayIst.Month, 1);
        var monthStartUtc = IstClock.ToUtc(monthStartIst.ToDateTime(TimeOnly.MinValue));

        // Quotations
        var quotations = await db.Quotations
            .GroupBy(q => q.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int QuotationCount(QuotationStatus status) =>
            quotations.FirstOrDefault(q => q.Status == status)?.Count ?? 0;
        var quotationTotal = quotations.Sum(q => q.Count);
        var awaitingDecision = QuotationCount(QuotationStatus.Sent) + QuotationCount(QuotationStatus.Viewed);

        // Invoices. Overdue is derived here exactly as it is on the invoice itself —
        // due date passed, money still owed, still awaiting payment.
        var invoices = await db.Invoices
            .Select(i => new { i.Status, i.DueDate, i.OutstandingAmount, i.TotalAmount })
            .ToListAsync(ct);
        var invoicesPending = invoices.Count(i =>
            i.Status is InvoiceStatus.Pending or InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid);
        var invoicesPaid = invoices.Count(i => i.Status == InvoiceStatus.Paid);
        var invoicesOverdue = invoices.Count(i =>
            i.DueDate < todayIst && i.OutstandingAmount > 0
            && i.Status is InvoiceStatus.Pending or InvoiceStatus.Sent or InvoiceStatus.PartiallyPaid);
        var outstanding = invoices.Sum(i => i.OutstandingAmount);

        var receivedMonth = await db.Payments
            .Where(p => p.Status == PaymentStatus.Success && p.PaidUtc >= monthStartUtc)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var expensesMonth = await db.Expenses
            .Where(e => e.ExpenseDate >= monthStartIst)
            .SumAsync(e => (decimal?)(e.Amount + e.AdjustmentTotal), ct) ?? 0m;

        var openPoValue = await db.PurchaseOrders
            .Where(p => p.Status != PurchaseOrderStatus.Cancelled
                        && p.Status != PurchaseOrderStatus.Received)
            .SumAsync(p => (decimal?)p.TotalAmount, ct) ?? 0m;

        // Stock is derived from the ledger (Invariant 11), never a stored column.
        // Consumables only: an asset is one unit with no reorder level (Assets module), and
        // a service holds no stock.
        var items = await db.InventoryItems
            .Where(i => i.Kind == ItemKind.Consumable && i.ItemType == InventoryItemType.Goods)
            .Select(i => new { i.InventoryItemId, i.OpeningQuantity, i.ReorderLevel })
            .ToListAsync(ct);
        var movements = await db.InventoryTransactions
            .GroupBy(t => new { t.InventoryItemId, t.TransactionType })
            .Select(g => new { g.Key.InventoryItemId, g.Key.TransactionType, Total = g.Sum(t => t.Quantity) })
            .ToListAsync(ct);
        var lowStock = 0;
        foreach (var item in items)
        {
            decimal SumOf(InventoryTransactionType type) => movements
                .Where(m => m.InventoryItemId == item.InventoryItemId && m.TransactionType == type)
                .Sum(m => m.Total);
            var current = item.OpeningQuantity
                          + SumOf(InventoryTransactionType.In)
                          + SumOf(InventoryTransactionType.OpeningBalance)
                          + SumOf(InventoryTransactionType.Adjustment)
                          - SumOf(InventoryTransactionType.Out)
                          - SumOf(InventoryTransactionType.Damage);
            if (current <= item.ReorderLevel)
            {
                lowStock++;
            }
        }

        var bank = await db.BankTransactions
            .Where(t => t.TransactionDate >= monthStartIst)
            .Select(t => new { t.Direction, t.Amount, t.ReconciliationStatus })
            .ToListAsync(ct);
        var bankCredits = bank.Where(b => b.Direction == BankTransactionDirection.Credit).Sum(b => b.Amount);
        var bankDebits = bank.Where(b => b.Direction == BankTransactionDirection.Debit).Sum(b => b.Amount);
        var unreconciled = bank.Count(b => b.ReconciliationStatus != ReconciliationStatus.Reconciled);

        var cards = new List<DashboardCard>
        {
            new("quotations", "Quotations", quotationTotal.ToString(),
                $"{awaitingDecision} awaiting a decision · {QuotationCount(QuotationStatus.Approved)} approved · "
                + $"{QuotationCount(QuotationStatus.Rejected)} rejected",
                "/admin/quotations"),
            new("invoices", "Invoices", invoices.Count.ToString(),
                $"{invoicesPending} pending · {invoicesPaid} paid · {invoicesOverdue} overdue",
                "/admin/invoices"),
            new("received-month", "Received this month", $"₹ {receivedMonth:N2}",
                "Successful payments since the 1st", "/admin/payments"),
            new("outstanding", "Outstanding", $"₹ {outstanding:N2}",
                $"Across {invoices.Count(i => i.OutstandingAmount > 0)} invoice(s)", "/admin/invoices"),
            new("expenses-month", "Expenses this month", $"₹ {expensesMonth:N2}",
                "Recorded company spend", "/admin/expenses"),
            new("purchase-orders", "Open purchase orders", $"₹ {openPoValue:N2}",
                "Committed but not fully received", "/admin/purchase-orders"),
            new("inventory", "Low stock items", lowStock.ToString(),
                $"Of {items.Count} stocked item(s)", "/admin/inventory/stock"),
            new("bank", "Bank this month", $"₹ {bankCredits - bankDebits:N2}",
                $"₹ {bankCredits:N2} in · ₹ {bankDebits:N2} out · {unreconciled} unreconciled",
                "/admin/bank/reconciliation")
        };

        return new AdminDashboardDto(cards, nowUtc);
    }
}
