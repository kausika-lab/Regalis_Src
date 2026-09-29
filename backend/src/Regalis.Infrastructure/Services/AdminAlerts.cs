using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services;

/// <summary>One event for the admin Notifications page.</summary>
internal sealed record AdminEvent(
    AdminNotificationType Type,
    string EventCode,
    string Title,
    string Body,
    AdminNotificationSeverity Severity = AdminNotificationSeverity.Info,
    long? CustomerId = null,
    string? ReferenceType = null,
    long? ReferenceId = null,
    string? ReferenceLabel = null,
    string? ActionLabel = null,
    string? ActionRoute = null);

/// <summary>
/// Raises events onto the admin Notifications page (TRN_AdminNotification). There is
/// one row per event; each admin's read state is a TRN_AdminNotificationRead row, so
/// one admin reading an item never clears it for the others. Rows are only added to
/// the context: the caller's SaveChanges commits them with the change they describe.
///
/// Payments, wallet entries, new customers, customer profile edits, purchase orders
/// and maintenance status changes are raised by <c>AdminEventInterceptor</c> from the
/// change tracker, so every code path that saves one produces its notification. The
/// helpers here cover bookings, tickets and staff accounts, which carry richer text.
/// </summary>
internal static class AdminAlerts
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    public static AdminNotification Raise(RegalisDbContext db, AdminEvent ev, DateTime nowUtc,
        long? readByUserId = null)
    {
        var row = new AdminNotification
        {
            Type = ev.Type,
            EventCode = ev.EventCode,
            Severity = ev.Severity,
            Title = Clip(ev.Title, 200),
            Body = Clip(ev.Body, 1000),
            CustomerId = ev.CustomerId,
            ReferenceType = ev.ReferenceType,
            ReferenceId = ev.ReferenceId,
            ReferenceLabel = ev.ReferenceLabel is null ? null : Clip(ev.ReferenceLabel, 120),
            ActionLabel = ev.ActionLabel,
            ActionRoute = ev.ActionRoute,
            CreatedUtc = nowUtc
        };
        // The admin who caused the event has, by definition, seen it.
        if (readByUserId is { } userId)
        {
            row.Reads.Add(new AdminNotificationRead { UserId = userId, ReadUtc = nowUtc });
        }
        db.AdminNotifications.Add(row);
        return row;
    }

    /// <summary>"New booking — Board Room": "Kausika RS · 25 Sep 2026, 04:00 PM–06:00 PM IST."</summary>
    public static async Task BookingAsync(RegalisDbContext db, Booking booking, string title,
        DateTime nowUtc, CancellationToken ct, string? suffix = null)
    {
        var customer = await NameOfAsync(db, booking.CustomerId, ct);
        var workspace = await db.Workspaces.AsNoTracking()
            .Where(w => w.WorkspaceId == booking.WorkspaceId)
            .Select(w => w.WorkspaceName)
            .FirstOrDefaultAsync(ct) ?? "a workspace";
        var start = IstClock.ToIst(booking.StartUtc);
        var end = IstClock.ToIst(booking.EndUtc);
        var body = $"{customer} · {start.ToString("dd MMM yyyy, hh:mm tt", Invariant)}–" +
                   $"{end.ToString("hh:mm tt", Invariant)} IST." + (suffix is null ? "" : " " + suffix);
        var code = title switch
        {
            "New booking" => "booking.created",
            "Booking confirmed" => "booking.confirmed",
            "Booking cancelled" => "booking.cancelled",
            "Booking rescheduled" => "booking.rescheduled",
            "Booking completed" => "booking.completed",
            _ => "booking.updated"
        };
        Raise(db, new AdminEvent(AdminNotificationType.Booking, code, $"{title} — {workspace}", body,
            CustomerId: booking.CustomerId, ReferenceType: "Booking", ReferenceId: booking.BookingId,
            ReferenceLabel: $"Booking {booking.BookingNumber}", ActionLabel: "Open booking",
            ActionRoute: $"/admin/bookings?booking={booking.BookingId}"), nowUtc);
    }

    /// <summary>A new ticket, a customer reply or a reopened ticket needs someone; the
    /// rest is information. "Needs action" clears when the ticket is resolved or closed.</summary>
    public static async Task TicketAsync(RegalisDbContext db, Ticket ticket, string title,
        DateTime nowUtc, CancellationToken ct, long? exceptUserId = null, string? suffix = null)
    {
        var customer = await NameOfAsync(db, ticket.CustomerId, ct);
        var (code, severity) = title switch
        {
            "New support ticket" => ("support.ticket_created", AdminNotificationSeverity.ActionNeeded),
            "Customer replied" => ("support.customer_replied", AdminNotificationSeverity.ActionNeeded),
            "Ticket reopened" => ("support.reopened", AdminNotificationSeverity.ActionNeeded),
            "Ticket assigned" => ("support.assigned", AdminNotificationSeverity.Info),
            "Ticket closed" => ("support.closed", AdminNotificationSeverity.Info),
            _ => ("support.updated", AdminNotificationSeverity.Info)
        };
        var unassigned = ticket.AssignedToUserId is null;
        var body = $"Raised by {customer}, {ticket.Priority} priority." +
                   (code == "support.ticket_created" && unassigned ? " No one assigned yet." : "") +
                   (suffix is null ? "" : " " + suffix);
        Raise(db, new AdminEvent(AdminNotificationType.Support, code, $"{title} — {ticket.Subject}", body,
            severity, ticket.CustomerId, "Ticket", ticket.TicketId, $"Ticket {ticket.TicketNumber}",
            unassigned && severity == AdminNotificationSeverity.ActionNeeded ? "Assign ticket" : "Open ticket",
            $"/admin/tickets/{ticket.TicketId}"), nowUtc, exceptUserId);
    }

    /// <summary>Staff and admin account activity. A customer's own edits and new
    /// customers are raised by the interceptor, with the actual old → new values.</summary>
    public static Task AccountAsync(RegalisDbContext db, User user, string title, string action,
        DateTime nowUtc, CancellationToken ct, NotificationPriority priority = NotificationPriority.Normal)
    {
        if (user.UserType == UserType.Customer && title != "Account unlocked")
        {
            return Task.CompletedTask;
        }
        var kind = user.UserType switch
        {
            UserType.Customer => "Customer",
            UserType.Staff => "Staff",
            _ => "Admin"
        };
        var route = user.UserType == UserType.Customer
            ? $"/admin/customers/{user.UserId}"
            : $"/admin/users?search={Uri.EscapeDataString(user.Email)}";
        Raise(db, new AdminEvent(AdminNotificationType.AccountChange,
            "account." + title.ToLowerInvariant().Replace(' ', '_'), title,
            $"{kind} profile {user.FullName} {action}.",
            CustomerId: user.UserType == UserType.Customer ? user.UserId : null,
            ReferenceType: "User", ReferenceId: user.UserId, ReferenceLabel: $"{kind} · {user.FullName}",
            ActionLabel: user.UserType == UserType.Customer ? "Open customer" : "View profile",
            ActionRoute: route), nowUtc);
        return Task.CompletedTask;
    }

    private static async Task<string> NameOfAsync(RegalisDbContext db, long userId,
        CancellationToken ct) =>
        await db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => u.FullName)
            .FirstOrDefaultAsync(ct) ?? "a customer";

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";
}
