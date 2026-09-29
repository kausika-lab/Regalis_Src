using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>The event types, their settings-screen wording and default extra channels:
/// in-app is always on, email is on by default for new customers, payments and support
/// only, WhatsApp is off unless an admin turns it on.</summary>
internal static class AdminNotificationCatalogue
{
    public static readonly (AdminNotificationType Type, string Label, string Description, bool DefaultEmail)[] Types =
    [
        (AdminNotificationType.Customer, "Customer registered", "A new customer creates an account", true),
        (AdminNotificationType.Booking, "New booking / cancellation", "Any booking made, changed or cancelled", false),
        (AdminNotificationType.Payment, "Payment received", "Invoice paid, payment failed or invoice overdue", true),
        (AdminNotificationType.Wallet, "Wallet activity", "Recharges, refunds, adjustments and low balance", false),
        (AdminNotificationType.AccountChange, "Account changes", "Customer edits name, mobile, email, GSTIN, address or company", false),
        (AdminNotificationType.Support, "Support ticket", "New ticket, customer reply or reopened ticket", true),
        (AdminNotificationType.Maintenance, "Maintenance", "Task overdue, or work waiting for verification", false),
        (AdminNotificationType.Purchase, "Purchase and supplier", "PO sent, goods received, bill recorded", false),
    ];

    public static bool DefaultEmail(AdminNotificationType type) =>
        Types.FirstOrDefault(t => t.Type == type).DefaultEmail;
}

/// <summary>
/// The admin Notifications feed. Read state is per admin (TRN_AdminNotificationRead).
/// Nothing is ever deleted: without an explicit From date the feed shows the last 90
/// days, and older rows stay reachable through the date filter.
///
/// "Needs action" is worked out from the record itself, not from read state: a new
/// ticket stops needing action when the ticket is resolved or closed, an overdue invoice
/// when it is paid, a maintenance item when the work is verified (or done, for an
/// overdue task), a failed payment when the customer's next payment succeeds.
/// </summary>
public class AdminNotificationService(
    RegalisDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IAdminNotificationService
{
    internal const int ArchiveDays = 90;

    public async Task<AdminNotificationFeedDto> GetFeedAsync(AdminNotificationListRequest request,
        CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var nowUtc = clock.UtcNow;

        var scoped = Filter(db.AdminNotifications.AsNoTracking(), request, userId, nowUtc, applyType: false);
        var typeCounts = await scoped.GroupBy(n => n.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => (byte)x.Type, x => x.Count, ct);
        foreach (var type in Enum.GetValues<AdminNotificationType>()) typeCounts.TryAdd((byte)type, 0);

        var query = request.Type is { } t ? scoped.Where(n => n.Type == (AdminNotificationType)t) : scoped;
        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(n => n.CreatedUtc).ThenByDescending(n => n.AdminNotificationId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(n => new
            {
                Row = n,
                IsRead = n.Reads.Any(r => r.UserId == userId),
                CustomerName = db.Users.Where(u => u.UserId == n.CustomerId).Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync(ct);

        var ids = rows.Select(r => r.Row.AdminNotificationId).ToList();
        var open = await Unresolved(db.AdminNotifications.AsNoTracking().Where(n => ids.Contains(n.AdminNotificationId)))
            .Select(n => n.AdminNotificationId).ToListAsync(ct);

        var items = rows.Select(r => new AdminNotificationDto(
            r.Row.AdminNotificationId, (byte)r.Row.Type, r.Row.EventCode, (byte)r.Row.Severity,
            r.Row.Title, r.Row.Body, r.Row.CustomerId, r.CustomerName, r.Row.ReferenceType,
            r.Row.ReferenceId, r.Row.ReferenceLabel, r.Row.ActionLabel, r.Row.ActionRoute,
            r.Row.CreatedUtc, r.IsRead, open.Contains(r.Row.AdminNotificationId))).ToList();

        return new AdminNotificationFeedDto(
            PagedResult<AdminNotificationDto>.Create(items, request.Page, request.PageSize, total),
            typeCounts, typeCounts.Values.Sum(), await GetUnreadCountAsync(ct),
            await SummaryAsync(userId, nowUtc, ct));
    }

    public Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var since = clock.UtcNow.AddDays(-ArchiveDays);
        return db.AdminNotifications.AsNoTracking()
            .CountAsync(n => n.CreatedUtc >= since && !n.Reads.Any(r => r.UserId == userId), ct);
    }

    public async Task MarkReadAsync(long adminNotificationId, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        if (!await db.AdminNotifications.AnyAsync(n => n.AdminNotificationId == adminNotificationId, ct))
        {
            throw new NotFoundException("Notification", adminNotificationId);
        }
        if (await db.AdminNotificationReads.AnyAsync(
                r => r.AdminNotificationId == adminNotificationId && r.UserId == userId, ct))
        {
            return;
        }
        db.AdminNotificationReads.Add(new AdminNotificationRead
        {
            AdminNotificationId = adminNotificationId, UserId = userId, ReadUtc = clock.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Marks everything this admin has not read, in one statement.</summary>
    public Task<int> MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var nowUtc = clock.UtcNow;
        return db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO TRN_AdminNotificationRead (AdminNotificationId, UserId, ReadUtc)
            SELECT n.AdminNotificationId, {userId}, {nowUtc}
            FROM TRN_AdminNotification n
            WHERE NOT EXISTS (SELECT 1 FROM TRN_AdminNotificationRead r
                              WHERE r.AdminNotificationId = n.AdminNotificationId AND r.UserId = {userId})
            """, ct);
    }

    public async Task<List<AdminNotificationSettingDto>> GetSettingsAsync(CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var saved = await db.AdminNotificationSettings.AsNoTracking()
            .Where(s => s.UserId == userId).ToDictionaryAsync(s => s.Type, ct);
        return AdminNotificationCatalogue.Types.Select(t => saved.TryGetValue(t.Type, out var s)
                ? new AdminNotificationSettingDto((byte)t.Type, t.Label, t.Description, true, s.Email, s.WhatsApp)
                : new AdminNotificationSettingDto((byte)t.Type, t.Label, t.Description, true, t.DefaultEmail, false))
            .ToList();
    }

    public async Task<List<AdminNotificationSettingDto>> SaveSettingsAsync(
        SaveAdminNotificationSettingsRequest request, CancellationToken ct = default)
    {
        var userId = currentUser.UserId;
        var saved = await db.AdminNotificationSettings.Where(s => s.UserId == userId).ToListAsync(ct);
        foreach (var item in request.Items)
        {
            var type = (AdminNotificationType)item.Type;
            var row = saved.FirstOrDefault(s => s.Type == type);
            if (row is null)
            {
                row = new AdminNotificationSetting { UserId = userId, Type = type };
                db.AdminNotificationSettings.Add(row);
            }
            row.Email = item.Email;
            row.WhatsApp = item.WhatsApp;
            row.ModifiedUtc = clock.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        return await GetSettingsAsync(ct);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>Midnight IST of a date, in UTC.</summary>
    private static DateTime DayStartUtc(DateOnly date) => IstClock.ToUtc(date.ToDateTime(TimeOnly.MinValue));

    private IQueryable<AdminNotification> Filter(IQueryable<AdminNotification> query,
        AdminNotificationListRequest request, long userId, DateTime nowUtc, bool applyType)
    {
        // Auto-archive: older than 90 days is out of the default view, never deleted.
        var fromUtc = request.From is { } from ? DayStartUtc(from) : nowUtc.AddDays(-ArchiveDays);
        query = query.Where(n => n.CreatedUtc >= fromUtc);
        if (request.To is { } to)
        {
            var toUtc = DayStartUtc(to.AddDays(1));
            query = query.Where(n => n.CreatedUtc < toUtc);
        }
        if (applyType && request.Type is { } type)
        {
            query = query.Where(n => n.Type == (AdminNotificationType)type);
        }
        query = request.State?.Trim().ToLowerInvariant() switch
        {
            "unread" => query.Where(n => !n.Reads.Any(r => r.UserId == userId)),
            "read" => query.Where(n => n.Reads.Any(r => r.UserId == userId)),
            _ => query
        };
        if (request.CustomerId is { } customerId)
        {
            query = query.Where(n => n.CustomerId == customerId);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(n => n.Title.Contains(term) || n.Body.Contains(term)
                                     || (n.ReferenceLabel != null && n.ReferenceLabel.Contains(term))
                                     || db.Users.Any(u => u.UserId == n.CustomerId && u.FullName.Contains(term)));
        }
        if (request.NeedsAction == true)
        {
            query = Unresolved(query);
        }
        return query;
    }

    /// <summary>ActionNeeded rows whose record is not resolved yet.</summary>
    private IQueryable<AdminNotification> Unresolved(IQueryable<AdminNotification> query) =>
        query.Where(n => n.Severity == AdminNotificationSeverity.ActionNeeded
            && !((n.ReferenceType == "Ticket" && db.Tickets.Any(t => t.TicketId == n.ReferenceId
                     && (t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed)))
                 || (n.ReferenceType == "Invoice" && db.Invoices.Any(i => i.InvoiceId == n.ReferenceId
                     && (i.Status == InvoiceStatus.Paid || i.Status == InvoiceStatus.Cancelled
                         || i.Status == InvoiceStatus.Refunded)))
                 || (n.EventCode == "maintenance.verification" && db.MaintenanceWorkItems.Any(w =>
                     w.MaintenanceWorkItemId == n.ReferenceId && (w.Status == MaintenanceWorkStatus.Verified
                         || w.Status == MaintenanceWorkStatus.Cancelled || w.Status == MaintenanceWorkStatus.Rework)))
                 || (n.EventCode == "maintenance.overdue" && db.MaintenanceWorkItems.Any(w =>
                     w.MaintenanceWorkItemId == n.ReferenceId && (w.Status == MaintenanceWorkStatus.Done
                         || w.Status == MaintenanceWorkStatus.Verified || w.Status == MaintenanceWorkStatus.Cancelled)))
                 || (n.EventCode == "payment.failed" && db.Payments.Any(p => p.CustomerId == n.CustomerId
                     && p.Status == PaymentStatus.Success && p.CreatedDate > n.CreatedUtc))));

    private async Task<AdminNotificationSummaryDto> SummaryAsync(long userId, DateTime nowUtc, CancellationToken ct)
    {
        var since = nowUtc.AddDays(-ArchiveDays);
        var todayUtc = DayStartUtc(IstClock.IstDateOf(nowUtc));
        var all = db.AdminNotifications.AsNoTracking();
        var unread = await all.CountAsync(n => n.CreatedUtc >= since && !n.Reads.Any(r => r.UserId == userId), ct);
        var today = await all.Where(n => n.CreatedUtc >= todayUtc)
            .GroupBy(n => n.Type).Select(g => g.Count()).ToListAsync(ct);
        var needsAction = await Unresolved(all).CountAsync(ct);
        var sent = db.Announcements.AsNoTracking().Where(a => a.Status == AnnouncementStatus.Sent);
        return new AdminNotificationSummaryDto(unread, today.Sum(), today.Count, needsAction,
            await sent.CountAsync(ct), await sent.MaxAsync(a => a.SentUtc, ct));
    }
}
