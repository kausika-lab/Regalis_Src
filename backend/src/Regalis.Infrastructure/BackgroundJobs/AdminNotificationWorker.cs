using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services;
using Regalis.Shared;

namespace Regalis.Infrastructure.BackgroundJobs;

/// <summary>
/// The admin-notification jobs: sends the email / WhatsApp copies admins asked for in
/// their Notification settings, and raises "Invoice overdue" once per invoice. Kept apart
/// from the timer so tests can run a pass directly.
/// </summary>
public class AdminNotificationJobs(
    RegalisDbContext db,
    IQuotationDispatcher dispatcher,
    IDateTimeProvider clock,
    ILogger<AdminNotificationJobs> logger)
{
    private static readonly CultureInfo Indian = CultureInfo.GetCultureInfo("en-IN");

    /// <summary>Delivers the extra channels for rows not handled yet. In-app needs no
    /// delivery: the row is the notification. Nothing is sent for a type whose toggle is
    /// off; staff (non-admin) accounts get nothing unless they switched a channel on.</summary>
    public async Task<int> DeliverPendingAsync(CancellationToken ct)
    {
        var nowUtc = clock.UtcNow;
        var pending = await db.AdminNotifications
            .Where(n => n.DeliveredUtc == null)
            .OrderBy(n => n.AdminNotificationId).Take(50).ToListAsync(ct);
        if (pending.Count == 0) return 0;

        var admins = await db.Users.AsNoTracking()
            .Where(u => u.UserType != UserType.Customer && u.Status == UserStatus.Active)
            .Select(u => new { u.UserId, u.UserType, u.Email, u.MobileNumber })
            .ToListAsync(ct);
        var ids = admins.Select(a => a.UserId).ToList();
        var settings = await db.AdminNotificationSettings.AsNoTracking()
            .Where(s => ids.Contains(s.UserId)).ToListAsync(ct);

        foreach (var row in pending)
        {
            // A backlog (the API was down for a day) is not worth a flood of old mail.
            if (row.CreatedUtc < nowUtc.AddDays(-1))
            {
                row.DeliveredUtc = nowUtc;
                row.DeliveryNote = "Not sent: older than a day when picked up.";
                continue;
            }
            int emails = 0, emailFailures = 0, whatsApp = 0, whatsAppManual = 0, whatsAppFailures = 0;
            foreach (var admin in admins)
            {
                var own = settings.FirstOrDefault(s => s.UserId == admin.UserId && s.Type == row.Type);
                var wantsEmail = own?.Email ?? (admin.UserType == UserType.Admin
                                                && AdminNotificationCatalogue.DefaultEmail(row.Type));
                var wantsWhatsApp = own?.WhatsApp ?? false;

                if (wantsEmail && !string.IsNullOrWhiteSpace(admin.Email))
                {
                    var result = await dispatcher.SendEmailAsync(new QuotationEmailMessage(
                        admin.Email, null, null, $"Regalis WorkSpace — {row.Title}", EmailHtml(row), []), ct);
                    if (result.Succeeded) emails++; else emailFailures++;
                }
                if (wantsWhatsApp && !string.IsNullOrWhiteSpace(admin.MobileNumber))
                {
                    var result = await dispatcher.SendWhatsAppAsync(new QuotationWhatsAppMessage(
                        admin.MobileNumber, $"{row.Title}\n{row.Body}", "", null), ct);
                    if (!result.Succeeded) whatsAppFailures++;
                    // No WhatsApp Business provider: the dispatcher hands back a wa.me link
                    // instead of sending. Record that it did NOT go out.
                    else if (result.Note?.Contains("manual", StringComparison.OrdinalIgnoreCase) == true) whatsAppManual++;
                    else whatsApp++;
                }
            }
            row.DeliveredUtc = nowUtc;
            row.DeliveryNote = Note(emails, emailFailures, whatsApp, whatsAppManual, whatsAppFailures);
        }
        await db.SaveChangesAsync(ct);
        return pending.Count;
    }

    /// <summary>Raises "Invoice overdue" for invoices past their due date with money
    /// outstanding — once per invoice. It stops needing action when the invoice is paid.</summary>
    public async Task<int> RaiseOverdueInvoicesAsync(CancellationToken ct)
    {
        var nowUtc = clock.UtcNow;
        var today = IstClock.IstDateOf(nowUtc);
        var overdue = await db.Invoices.AsNoTracking()
            .Where(i => i.DueDate < today && i.OutstandingAmount > 0
                        && (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Sent
                            || i.Status == InvoiceStatus.PartiallyPaid)
                        && !db.AdminNotifications.Any(n => n.EventCode == "invoice.overdue"
                                                           && n.ReferenceType == "Invoice"
                                                           && n.ReferenceId == i.InvoiceId))
            .OrderBy(i => i.DueDate).Take(200)
            .Select(i => new
            {
                i.InvoiceId, i.InvoiceNumber, i.CustomerId, i.DueDate, i.OutstandingAmount,
                Customer = db.Users.Where(u => u.UserId == i.CustomerId).Select(u => u.FullName).FirstOrDefault()
            })
            .ToListAsync(ct);
        foreach (var i in overdue)
        {
            var days = today.DayNumber - i.DueDate.DayNumber;
            AdminAlerts.Raise(db, new AdminEvent(AdminNotificationType.Payment, "invoice.overdue",
                $"Invoice overdue — {i.InvoiceNumber}",
                $"{i.Customer ?? "A customer"} owes ₹{i.OutstandingAmount.ToString("#,##0.00", Indian)}, " +
                $"due {i.DueDate:dd MMM yyyy} ({days} day{(days == 1 ? "" : "s")} ago).",
                AdminNotificationSeverity.ActionNeeded, i.CustomerId, "Invoice", i.InvoiceId,
                $"Invoice {i.InvoiceNumber}", "Open invoice", "/admin/invoices"), nowUtc);
        }
        if (overdue.Count > 0) await db.SaveChangesAsync(ct);
        logger.LogInformation("Overdue invoice scan raised {Count} notification(s)", overdue.Count);
        return overdue.Count;
    }

    private static string? Note(int emails, int emailFailures, int whatsApp, int manual, int whatsAppFailures)
    {
        var parts = new List<string>();
        if (emails > 0 || emailFailures > 0)
            parts.Add($"Email to {emails} admin{(emails == 1 ? "" : "s")}" + (emailFailures > 0 ? $", {emailFailures} failed" : ""));
        if (whatsApp > 0) parts.Add($"WhatsApp to {whatsApp}");
        if (manual > 0) parts.Add($"WhatsApp NOT sent to {manual}: no provider configured (manual share)");
        if (whatsAppFailures > 0) parts.Add($"WhatsApp failed for {whatsAppFailures}");
        return parts.Count == 0 ? null : string.Join("; ", parts);
    }

    private static string EmailHtml(AdminNotification row) =>
        $"""
         <div style="font-family:Arial,sans-serif;max-width:560px;margin:0 auto;padding:20px;color:#241F19;">
           <p style="font-weight:bold;font-size:16px;margin:0 0 8px;">{WebUtility.HtmlEncode(row.Title)}</p>
           <p style="line-height:1.5;margin:0 0 8px;">{WebUtility.HtmlEncode(row.Body)}</p>
           <p style="color:#8C8272;font-size:13px;margin:0;">{WebUtility.HtmlEncode(row.ReferenceLabel ?? "")}</p>
           <p style="color:#8C8272;font-size:12px;margin-top:20px;">Regalis WorkSpace admin notification.
             Change what you receive in Notifications → Notification settings.</p>
         </div>
         """;
}

/// <summary>Runs <see cref="AdminNotificationJobs"/>: delivery every minute, the
/// overdue-invoice scan every hour.</summary>
public class AdminNotificationWorker(IServiceScopeFactory scopeFactory, ILogger<AdminNotificationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        var lastScan = DateTime.MinValue;
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var jobs = scope.ServiceProvider.GetRequiredService<AdminNotificationJobs>();
                if (DateTime.UtcNow - lastScan >= TimeSpan.FromHours(1))
                {
                    await jobs.RaiseOverdueInvoicesAsync(stoppingToken);
                    lastScan = DateTime.UtcNow;
                }
                await jobs.DeliverPendingAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Survive transient failures; the next tick retries.
                logger.LogError(exception, "Admin notification job failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
