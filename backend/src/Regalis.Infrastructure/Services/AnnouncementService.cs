using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Adapters;
using Regalis.Infrastructure.BackgroundJobs;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Announcements to customers. Every recipient gets an in-app copy (a TRN_Notification
/// with ReferenceType "Announcement"), which is also what the read count is counted
/// from. Email and WhatsApp copies go out from the worker, and only when a channel can
/// really deliver: with no mail server or no WhatsApp provider the announcement says so
/// (WhatsAppManualShare, DeliveryNote) instead of pretending it went out.
///
/// A sent announcement is never edited, deleted or unsent.
/// </summary>
public class AnnouncementService(
    RegalisDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IQuotationDispatcher dispatcher,
    IOptions<SmtpOptions> smtpOptions,
    IOptions<WhatsAppOptions> whatsAppOptions) : IAnnouncementService
{
    /// <summary>WhatsApp and SMS copies are trimmed to this length.</summary>
    public const int ShortMessageLength = 300;

    public AnnouncementChannelsDto Channels() => new(
        !string.IsNullOrWhiteSpace(smtpOptions.Value.Host),
        !string.IsNullOrWhiteSpace(whatsAppOptions.Value.ApiBaseUrl)
        && !string.IsNullOrWhiteSpace(whatsAppOptions.Value.AccessToken));

    public async Task<PagedResult<AnnouncementDto>> ListAsync(AnnouncementListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Announcements.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(a => a.Title.Contains(term) || a.Message.Contains(term));
        }
        if (request.Status is { } status) query = query.Where(a => a.Status == (AnnouncementStatus)status);
        if (request.Audience is { } audience) query = query.Where(a => a.Audience == (AnnouncementAudience)audience);
        if (request.From is { } from)
        {
            var fromUtc = IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue));
            query = query.Where(a => (a.SentUtc ?? a.ScheduledForUtc ?? a.CreatedDate) >= fromUtc);
        }
        if (request.To is { } to)
        {
            var toUtc = IstClock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue));
            query = query.Where(a => (a.SentUtc ?? a.ScheduledForUtc ?? a.CreatedDate) < toUtc);
        }
        var total = await query.CountAsync(ct);
        // Drafts and scheduled ones first (they still need someone), then newest.
        var rows = await query
            .OrderBy(a => a.Status == AnnouncementStatus.Sent)
            .ThenByDescending(a => a.SentUtc ?? a.ScheduledForUtc ?? a.CreatedDate)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .ToListAsync(ct);
        var items = new List<AnnouncementDto>();
        foreach (var row in rows) items.Add(await ToDtoAsync(row, ct));
        return PagedResult<AnnouncementDto>.Create(items, request.Page, request.PageSize, total);
    }

    public async Task<Dictionary<byte, int>> StatusCountsAsync(CancellationToken ct = default)
    {
        var counts = await db.Announcements.AsNoTracking().GroupBy(a => a.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => (byte)x.Key, x => x.Count, ct);
        foreach (var s in Enum.GetValues<AnnouncementStatus>()) counts.TryAdd((byte)s, 0);
        return counts;
    }

    public async Task<AnnouncementDto> GetAsync(long announcementId, CancellationToken ct = default) =>
        await ToDtoAsync(await LoadAsync(announcementId, ct, track: false), ct);

    public async Task<AnnouncementDto> CreateAsync(SaveAnnouncementRequest request, CancellationToken ct = default)
    {
        var announcement = new Announcement();
        await ApplyAsync(announcement, request, ct);
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync(ct);
        return await ActAsync(announcement, request.Action, ct);
    }

    public async Task<AnnouncementDto> UpdateAsync(long announcementId, SaveAnnouncementRequest request,
        CancellationToken ct = default)
    {
        var announcement = await LoadAsync(announcementId, ct, track: true);
        if (announcement.Status == AnnouncementStatus.Sent)
        {
            throw new BusinessRuleException("This announcement has been sent. A sent announcement cannot be edited.");
        }
        await ApplyAsync(announcement, request, ct);
        await db.SaveChangesAsync(ct);
        return await ActAsync(announcement, request.Action, ct);
    }

    public async Task DeleteAsync(long announcementId, CancellationToken ct = default)
    {
        var announcement = await LoadAsync(announcementId, ct, track: true);
        if (announcement.Status == AnnouncementStatus.Sent)
        {
            throw new BusinessRuleException("A sent announcement cannot be deleted or unsent.");
        }
        db.Announcements.Remove(announcement); // soft delete
        await db.SaveChangesAsync(ct);
    }

    public Task<int> AudienceCountAsync(byte audience, long? customerId, CancellationToken ct = default) =>
        Recipients((AnnouncementAudience)audience, customerId).CountAsync(ct);

    // ── Sending ─────────────────────────────────────────────────────────────

    /// <summary>Scheduled announcements whose time has come (run by the worker).</summary>
    public async Task<int> PublishDueAsync(CancellationToken ct)
    {
        var nowUtc = clock.UtcNow;
        var due = await db.Announcements
            .Where(a => a.Status == AnnouncementStatus.Scheduled && a.ScheduledForUtc <= nowUtc)
            .OrderBy(a => a.ScheduledForUtc).Take(20).ToListAsync(ct);
        foreach (var announcement in due)
        {
            await SendAsync(announcement, announcement.ModifiedBy ?? announcement.CreatedBy, ct);
        }
        return due.Count;
    }

    /// <summary>Email / WhatsApp copies for sent announcements (run by the worker).</summary>
    public async Task<int> DeliverChannelsAsync(CancellationToken ct)
    {
        var pending = await db.Announcements
            .Where(a => a.Status == AnnouncementStatus.Sent && a.ChannelsDeliveredUtc == null)
            .OrderBy(a => a.SentUtc).Take(5).ToListAsync(ct);
        var channels = Channels();
        foreach (var announcement in pending)
        {
            var recipients = await db.Notifications.AsNoTracking()
                .Where(n => n.ReferenceType == "Announcement" && n.ReferenceId == announcement.AnnouncementId)
                .Join(db.Users, n => n.UserId, u => u.UserId, (n, u) => new { u.Email, u.MobileNumber })
                .ToListAsync(ct);
            var notes = new List<string>();
            if (announcement.SendEmail)
            {
                if (!channels.EmailConfigured)
                {
                    notes.Add("Email NOT sent: no mail server is configured");
                }
                else
                {
                    int sent = 0, failed = 0;
                    foreach (var r in recipients.Where(r => !string.IsNullOrWhiteSpace(r.Email)))
                    {
                        var result = await dispatcher.SendEmailAsync(new QuotationEmailMessage(
                            r.Email, null, null, announcement.Title, EmailHtml(announcement), []), ct);
                        if (result.Succeeded && !AdminNotificationJobs.NotSent(result.Note)) sent++; else failed++;
                    }
                    announcement.EmailSentCount = sent;
                    notes.Add($"Email sent to {sent}" + (failed > 0 ? $", {failed} failed" : ""));
                }
            }
            if (announcement.SendWhatsApp)
            {
                if (!channels.WhatsAppConfigured)
                {
                    announcement.WhatsAppManualShare = true;
                    notes.Add("WhatsApp NOT sent: no provider is configured (manual share)");
                }
                else
                {
                    int sent = 0, failed = 0;
                    var text = Short(announcement);
                    foreach (var r in recipients.Where(r => !string.IsNullOrWhiteSpace(r.MobileNumber)))
                    {
                        var result = await dispatcher.SendWhatsAppAsync(
                            new QuotationWhatsAppMessage(r.MobileNumber, text, "", null), ct);
                        if (result.Succeeded && result.Note?.Contains("manual", StringComparison.OrdinalIgnoreCase) != true) sent++;
                        else failed++;
                    }
                    announcement.WhatsAppSentCount = sent;
                    notes.Add($"WhatsApp sent to {sent}" + (failed > 0 ? $", {failed} not sent" : ""));
                }
            }
            announcement.DeliveryNote = notes.Count == 0 ? null : string.Join("; ", notes);
            announcement.ChannelsDeliveredUtc = clock.UtcNow;
        }
        if (pending.Count > 0) await db.SaveChangesAsync(ct);
        return pending.Count;
    }

    private async Task<AnnouncementDto> ActAsync(Announcement announcement, string action, CancellationToken ct)
    {
        switch (action)
        {
            case "send":
                await SendAsync(announcement, currentUser.UserId, ct);
                break;
            case "schedule":
                if (announcement.ScheduledForUtc is not { } when || when <= clock.UtcNow.AddMinutes(1))
                {
                    throw new BusinessRuleException("Pick a time in the future to schedule it.");
                }
                announcement.Status = AnnouncementStatus.Scheduled;
                await db.SaveChangesAsync(ct);
                break;
            default:
                announcement.Status = AnnouncementStatus.Draft;
                await db.SaveChangesAsync(ct);
                break;
        }
        return await ToDtoAsync(announcement, ct);
    }

    /// <summary>In-app copies to every recipient now; email / WhatsApp follow from the
    /// worker. A WhatsApp choice with no provider is marked manual share at once, so the
    /// list says so straight away.</summary>
    private async Task SendAsync(Announcement announcement, long? sentBy, CancellationToken ct)
    {
        var nowUtc = clock.UtcNow;
        var recipientIds = await Recipients(announcement.Audience, announcement.TargetCustomerId)
            .Select(u => u.UserId).ToListAsync(ct);
        foreach (var userId in recipientIds)
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId,
                Audience = NotificationAudience.SingleUser,
                Title = announcement.Title,
                Message = announcement.Message,
                Category = NotificationCategory.Announcement,
                Priority = NotificationPriority.Normal,
                ReferenceType = "Announcement",
                ReferenceId = announcement.AnnouncementId,
                SentUtc = nowUtc,
                CreatedByUserId = sentBy
            });
        }
        announcement.Status = AnnouncementStatus.Sent;
        announcement.SentUtc = nowUtc;
        announcement.SentByUserId = sentBy;
        announcement.RecipientCount = recipientIds.Count;
        var channels = Channels();
        if (announcement.SendWhatsApp && !channels.WhatsAppConfigured) announcement.WhatsAppManualShare = true;
        if (!announcement.SendEmail && !announcement.SendWhatsApp) announcement.ChannelsDeliveredUtc = nowUtc;
        await db.SaveChangesAsync(ct);
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    /// <summary>Active customers in the audience. Companies and individuals are told
    /// apart by whether the profile has a company name.</summary>
    private IQueryable<User> Recipients(AnnouncementAudience audience, long? customerId)
    {
        var customers = db.Users.AsNoTracking()
            .Where(u => u.UserType == UserType.Customer && u.Status == UserStatus.Active);
        return audience switch
        {
            AnnouncementAudience.CompaniesOnly => customers.Where(u => u.CompanyName != null && u.CompanyName.Trim() != ""),
            AnnouncementAudience.IndividualMembers => customers.Where(u => u.CompanyName == null || u.CompanyName.Trim() == ""),
            AnnouncementAudience.OneCustomer => customers.Where(u => u.UserId == customerId),
            _ => customers
        };
    }

    private async Task ApplyAsync(Announcement announcement, SaveAnnouncementRequest request, CancellationToken ct)
    {
        var audience = (AnnouncementAudience)request.Audience;
        if (audience == AnnouncementAudience.OneCustomer
            && !await db.Users.AnyAsync(u => u.UserId == request.TargetCustomerId && u.UserType == UserType.Customer, ct))
        {
            throw new NotFoundException("Customer", request.TargetCustomerId ?? 0);
        }
        announcement.Title = request.Title.Trim();
        announcement.Message = request.Message.Trim();
        announcement.Audience = audience;
        announcement.TargetCustomerId = audience == AnnouncementAudience.OneCustomer ? request.TargetCustomerId : null;
        announcement.SendEmail = request.SendEmail;
        announcement.SendWhatsApp = request.SendWhatsApp;
        announcement.ScheduledForUtc = request.Action == "schedule" ? request.ScheduledForUtc : null;
    }

    private async Task<Announcement> LoadAsync(long id, CancellationToken ct, bool track)
    {
        var query = track ? db.Announcements : db.Announcements.AsNoTracking();
        return await query.FirstOrDefaultAsync(a => a.AnnouncementId == id, ct)
               ?? throw new NotFoundException("Announcement", id);
    }

    private async Task<AnnouncementDto> ToDtoAsync(Announcement a, CancellationToken ct)
    {
        var readCount = a.Status == AnnouncementStatus.Sent
            ? await db.Notifications.AsNoTracking().CountAsync(
                n => n.ReferenceType == "Announcement" && n.ReferenceId == a.AnnouncementId && n.IsRead, ct)
            : 0;
        var names = await db.Users.AsNoTracking()
            .Where(u => u.UserId == a.TargetCustomerId || u.UserId == a.SentByUserId)
            .Select(u => new { u.UserId, u.FullName }).ToListAsync(ct);
        return new AnnouncementDto(a.AnnouncementId, a.Title, a.Message, (byte)a.Audience, a.TargetCustomerId,
            names.FirstOrDefault(n => n.UserId == a.TargetCustomerId)?.FullName,
            a.SendEmail, a.SendWhatsApp, (byte)a.Status, a.ScheduledForUtc, a.SentUtc,
            names.FirstOrDefault(n => n.UserId == a.SentByUserId)?.FullName,
            a.RecipientCount, readCount, a.EmailSentCount, a.WhatsAppSentCount, a.WhatsAppManualShare,
            a.Status == AnnouncementStatus.Sent && a.ChannelsDeliveredUtc is null, a.DeliveryNote, a.CreatedDate);
    }

    private static string Short(Announcement a)
    {
        var text = $"{a.Title}\n{a.Message}";
        return text.Length <= ShortMessageLength ? text : text[..(ShortMessageLength - 1)] + "…";
    }

    private static string EmailHtml(Announcement a) =>
        $"""
         <div style="font-family:Arial,sans-serif;max-width:560px;margin:0 auto;padding:20px;color:#241F19;">
           <p style="font-weight:bold;font-size:17px;margin:0 0 10px;">{WebUtility.HtmlEncode(a.Title)}</p>
           <p style="line-height:1.6;margin:0 0 16px;white-space:pre-wrap;">{WebUtility.HtmlEncode(a.Message)}</p>
           <p style="color:#8C8272;font-size:12px;margin:0;">Regalis WorkSpace</p>
         </div>
         """;
}
