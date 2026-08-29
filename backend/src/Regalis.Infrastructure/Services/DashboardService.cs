using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
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
}
