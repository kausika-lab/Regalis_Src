using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Alerts for the admin portal's Notifications screen. The feed and its read state are
/// per user (<see cref="NotificationService.GetMyAsync"/> filters on UserId), so every
/// active admin-portal user gets a row of their own. A single row with a null UserId
/// reaches nobody. Rows are only added to the context; the caller's SaveChanges commits
/// them with the change they describe.
/// </summary>
internal static class AdminAlerts
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static async Task AddAsync(RegalisDbContext db, NotificationCategory category,
        string title, string message, string referenceType, long? referenceId,
        string actionRoute, string actionLabel, DateTime nowUtc, CancellationToken ct,
        NotificationPriority priority = NotificationPriority.Normal, long? exceptUserId = null)
    {
        var recipientIds = await db.Users.AsNoTracking()
            .Where(u => u.UserType != UserType.Customer && u.Status == UserStatus.Active)
            .Select(u => u.UserId)
            .ToListAsync(ct);
        foreach (var userId in recipientIds.Where(id => id != exceptUserId))
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Audience = NotificationAudience.AllAdmins,
                Title = title,
                Message = message,
                Category = category,
                Priority = priority,
                ReferenceType = referenceType,
                ReferenceId = referenceId,
                ActionLabel = actionLabel,
                ActionRoute = actionRoute,
                SentUtc = nowUtc
            });
        }
    }

    /// <summary>"BK-000123 for Rahul Kumar · Hot Desk 4 · 25 Sep 2026, 06:30 PM–07:30 PM IST."</summary>
    public static async Task BookingAsync(RegalisDbContext db, Booking booking, string title,
        DateTime nowUtc, CancellationToken ct, string? suffix = null)
    {
        var customer = await NameOfAsync(db, booking.CustomerId, ct);
        var workspace = await db.Workspaces.AsNoTracking()
            .Where(w => w.WorkspaceId == booking.WorkspaceId)
            .Select(w => w.WorkspaceName)
            .FirstOrDefaultAsync(ct);
        var start = IstClock.ToIst(booking.StartUtc);
        var end = IstClock.ToIst(booking.EndUtc);
        var message = $"{booking.BookingNumber} for {customer} · {workspace} · " +
                      start.ToString("dd MMM yyyy, hh:mm tt", Invariant) + "–" +
                      end.ToString("hh:mm tt", Invariant) + " IST." +
                      (suffix is null ? "" : " " + suffix);
        await AddAsync(db, NotificationCategory.Booking, title, message, "Booking",
            booking.BookingId, $"/admin/bookings?booking={booking.BookingId}", "View booking",
            nowUtc, ct);
    }

    /// <summary>"TKT-000045: Wi-Fi drops · Rahul Kumar · High priority."</summary>
    public static async Task TicketAsync(RegalisDbContext db, Ticket ticket, string title,
        DateTime nowUtc, CancellationToken ct, long? exceptUserId = null, string? suffix = null)
    {
        var customer = await NameOfAsync(db, ticket.CustomerId, ct);
        var message = $"{ticket.TicketNumber}: {ticket.Subject} · {customer} · " +
                      $"{ticket.Priority} priority." + (suffix is null ? "" : " " + suffix);
        await AddAsync(db, NotificationCategory.SupportTicket, title, message, "Ticket",
            ticket.TicketId, $"/admin/tickets/{ticket.TicketId}", "Open ticket", nowUtc, ct,
            ticket.Priority >= TicketPriority.High ? NotificationPriority.High : NotificationPriority.Normal,
            exceptUserId);
    }

    /// <summary>"Admin profile Regalis Admin was updated."</summary>
    public static Task AccountAsync(RegalisDbContext db, User user, string title, string action,
        DateTime nowUtc, CancellationToken ct, NotificationPriority priority = NotificationPriority.Normal)
    {
        var kind = user.UserType switch
        {
            UserType.Customer => "Customer",
            UserType.Staff => "Staff",
            _ => "Admin"
        };
        var route = user.UserType == UserType.Customer
            ? $"/admin/customers/{user.UserId}"
            : $"/admin/users?search={Uri.EscapeDataString(user.Email)}";
        return AddAsync(db, NotificationCategory.Account, title,
            $"{kind} profile {user.FullName} {action}.", "User", user.UserId, route,
            "View profile", nowUtc, ct, priority);
    }

    private static async Task<string> NameOfAsync(RegalisDbContext db, long userId,
        CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct) ?? "a customer";
}
