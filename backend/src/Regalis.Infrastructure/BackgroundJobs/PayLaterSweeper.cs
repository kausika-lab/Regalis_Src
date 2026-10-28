using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Access;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services;
using Regalis.Shared;

namespace Regalis.Infrastructure.BackgroundJobs;

/// <summary>
/// Pay later housekeeping, every 5 minutes:
/// 1. Reminds the customer once, PayLater.ReminderHoursBefore hours before the start.
/// 2. Releases a pay-later booking with nothing paid PayLater.AutoCancelAfterStartMinutes
///    after its start and not checked in: cancelled, slot and QR released, invoice
///    cancelled. A part-paid booking is never released; it stays on the outstanding list.
/// 3. After PayLater.AutoBlockAfterUnpaidNoShows such releases, switches pay later off
///    for that customer (MST_User.PayLaterAllowed); staff can switch it back on.
/// Runs whether or not PayLater.Enabled is on, so bookings made while it was on are still
/// looked after. ChangedByUserId 0 = system, as in BookingHoldSweeper.
/// </summary>
public class PayLaterSweeper(IServiceScopeFactory scopeFactory, ILogger<PayLaterSweeper> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Pay later sweep failed");
            }
            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using IServiceScope scope = scopeFactory.CreateScope();
        RegalisDbContext db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        ISettingService settings = scope.ServiceProvider.GetRequiredService<ISettingService>();
        DateTime nowUtc = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>().UtcNow;

        await RemindAsync(db, settings, nowUtc, ct);
        await ReleaseUnpaidAsync(db, settings, nowUtc, ct);
        await ReleaseUnpaidPassesAsync(db, settings, nowUtc, ct);
    }

    /// <summary>A pay-later day pass or membership with nothing paid by the end of its first
    /// day is cancelled: its QR revoked, its unpaid invoice cancelled, the member told.
    /// Part-paid passes are left for the desk. Uses the same on/off switch as bookings
    /// (PayLater.AutoCancelAfterStartMinutes = 0 turns both off).</summary>
    private async Task ReleaseUnpaidPassesAsync(RegalisDbContext db, ISettingService settings, DateTime nowUtc, CancellationToken ct)
    {
        if (await settings.GetIntAsync(PayLaterPolicy.AutoCancelAfterStartMinutes, 30, ct) <= 0)
        {
            return;
        }
        DateTime firstDayOverBefore = nowUtc.AddDays(-1);
        List<CustomerPass> unpaid = await db.CustomerPasses.Include(p => p.PassPlan)
            .Where(p => p.IsPayLater && p.Status == CustomerPassStatus.Active && p.AmountPaid == 0m
                        && p.PaymentDueUtc != null && p.PaymentDueUtc <= firstDayOverBefore)
            .ToListAsync(ct);
        if (unpaid.Count == 0)
        {
            return;
        }
        List<long> ids = unpaid.Select(p => p.CustomerPassId).ToList();
        List<AccessToken> tokens = await db.AccessTokens
            .Where(t => t.CustomerPassId != null && ids.Contains(t.CustomerPassId.Value) && t.Status == AccessTokenStatus.Active)
            .ToListAsync(ct);
        List<Invoice> invoices = await db.Invoices
            .Where(i => i.CustomerPassId != null && ids.Contains(i.CustomerPassId.Value) && i.Status == InvoiceStatus.Pending && i.PaidAmount == 0m)
            .ToListAsync(ct);
        const string reason = "Pay later: not paid by the end of the first day; pass released.";
        foreach (CustomerPass p in unpaid)
        {
            p.Status = CustomerPassStatus.Cancelled;
            db.Notifications.Add(new Notification
            {
                UserId = p.CustomerId,
                Audience = NotificationAudience.SingleUser,
                Title = "Pass released",
                Message = $"{p.PassPlan.PlanName} ({p.PassNumber}) was not paid by the end of its first day, so it has been cancelled. Nothing was charged.",
                Category = NotificationCategory.Pass,
                Priority = NotificationPriority.High,
                ReferenceType = "CustomerPass",
                ReferenceId = p.CustomerPassId,
                ActionLabel = "View passes",
                ActionRoute = "/passes/my",
                SentUtc = nowUtc
            });
            AdminAlerts.Raise(db, new AdminEvent(AdminNotificationType.Payment, "paylater.pass_released",
                $"Pass released — {p.PassNumber}", $"{p.PassPlan.PlanName}: pay later, not paid by the end of the first day.",
                AdminNotificationSeverity.Info, p.CustomerId, "CustomerPass", p.CustomerPassId, p.PassNumber), nowUtc);
        }
        foreach (AccessToken t in tokens)
        {
            t.Status = AccessTokenStatus.Revoked;
            t.RevokedReason = "PayLaterUnpaid";
        }
        foreach (Invoice i in invoices)
        {
            i.Status = InvoiceStatus.Cancelled;
            i.CancellationReason = reason;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Released {Count} unpaid pay later pass(es)", unpaid.Count);
    }

    private async Task RemindAsync(RegalisDbContext db, ISettingService settings, DateTime nowUtc, CancellationToken ct)
    {
        int hours = await settings.GetIntAsync(PayLaterPolicy.ReminderHoursBefore, 24, ct);
        if (hours <= 0)
        {
            return;
        }
        DateTime horizon = nowUtc.AddHours(hours);
        List<Booking> due = await db.Bookings.Include(b => b.Workspace)
            .Where(b => b.IsPayLater && b.Status == BookingStatus.Confirmed && b.AmountPaid < b.TotalAmount
                        && b.PayLaterReminderSentUtc == null && b.StartUtc > nowUtc && b.StartUtc <= horizon)
            .ToListAsync(ct);
        if (due.Count == 0)
        {
            return;
        }
        foreach (Booking b in due)
        {
            DateTime startIst = IstClock.ToIst(b.StartUtc);
            decimal balance = b.TotalAmount - b.AmountPaid;
            db.Notifications.Add(new Notification
            {
                UserId = b.CustomerId,
                Audience = NotificationAudience.SingleUser,
                Title = "Payment due at the front desk",
                Message = $"{b.BookingNumber}: {b.Workspace.WorkspaceName} on {startIst:dd MMM yyyy, HH:mm} IST. "
                          + $"Please pay ₹{balance:N2} at the front desk (cash, UPI or card) before you check in.",
                Category = NotificationCategory.Payment,
                Priority = NotificationPriority.High,
                ReferenceType = "Booking",
                ReferenceId = b.BookingId,
                ActionLabel = "View booking",
                ActionRoute = $"/bookings/{b.BookingId}",
                SentUtc = nowUtc
            });
            b.PayLaterReminderSentUtc = nowUtc;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Sent {Count} pay later reminder(s)", due.Count);
    }

    private async Task ReleaseUnpaidAsync(RegalisDbContext db, ISettingService settings, DateTime nowUtc, CancellationToken ct)
    {
        int minutes = await settings.GetIntAsync(PayLaterPolicy.AutoCancelAfterStartMinutes, 30, ct);
        if (minutes <= 0)
        {
            return;
        }
        DateTime startedBefore = nowUtc.AddMinutes(-minutes);
        List<Booking> unpaid = await db.Bookings
            .Where(b => b.IsPayLater && b.Status == BookingStatus.Confirmed && b.AmountPaid == 0m && b.StartUtc <= startedBefore)
            .ToListAsync(ct);
        if (unpaid.Count == 0)
        {
            return;
        }

        List<long> ids = unpaid.Select(b => b.BookingId).ToList();
        List<AccessToken> tokens = await db.AccessTokens
            .Where(t => t.BookingId != null && ids.Contains(t.BookingId.Value) && t.Status == AccessTokenStatus.Active)
            .ToListAsync(ct);
        List<Invoice> invoices = await db.Invoices
            .Where(i => i.BookingId != null && ids.Contains(i.BookingId.Value) && i.Status == InvoiceStatus.Pending && i.PaidAmount == 0m)
            .ToListAsync(ct);

        foreach (Booking b in unpaid)
        {
            b.Status = BookingStatus.Cancelled;
            b.CancelledUtc = nowUtc;
            b.CancelledByUserId = 0L;
            b.CancellationReason = PayLaterPolicy.UnpaidReleaseReason;
            b.StatusHistory.Add(new BookingStatusHistory
            {
                BookingId = b.BookingId,
                FromStatus = BookingStatus.Confirmed,
                ToStatus = BookingStatus.Cancelled,
                ChangedByUserId = 0L,
                ChangedUtc = nowUtc,
                Reason = PayLaterPolicy.UnpaidReleaseReason
            });
            db.Notifications.Add(new Notification
            {
                UserId = b.CustomerId,
                Audience = NotificationAudience.SingleUser,
                Title = "Booking released",
                Message = $"{b.BookingNumber} was not paid by {minutes} minutes after the start, so the slot has been released.",
                Category = NotificationCategory.Booking,
                Priority = NotificationPriority.High,
                ReferenceType = "Booking",
                ReferenceId = b.BookingId,
                ActionLabel = "View booking",
                ActionRoute = $"/bookings/{b.BookingId}",
                SentUtc = nowUtc
            });
            await AdminAlerts.BookingAsync(db, b, "Booking cancelled", nowUtc, ct, "Pay later booking not paid; slot released.");
        }
        foreach (AccessToken t in tokens)
        {
            t.Status = AccessTokenStatus.Revoked;
            t.RevokedReason = "PayLaterUnpaid";
        }
        foreach (Invoice i in invoices)
        {
            i.Status = InvoiceStatus.Cancelled;
            i.CancellationReason = PayLaterPolicy.UnpaidReleaseReason;
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Released {Count} unpaid pay later booking(s)", unpaid.Count);

        int blockAfter = await settings.GetIntAsync(PayLaterPolicy.AutoBlockAfterUnpaidNoShows, 3, ct);
        if (blockAfter <= 0)
        {
            return;
        }
        List<long> customers = unpaid.Select(b => b.CustomerId).Distinct().ToList();
        List<long> toBlock = await db.Bookings.AsNoTracking()
            .Where(b => customers.Contains(b.CustomerId) && b.IsPayLater && b.Status == BookingStatus.Cancelled
                        && b.CancellationReason == PayLaterPolicy.UnpaidReleaseReason)
            .GroupBy(b => b.CustomerId)
            .Where(g => g.Count() >= blockAfter)
            .Select(g => g.Key)
            .ToListAsync(ct);
        if (toBlock.Count == 0)
        {
            return;
        }
        foreach (var user in await db.Users.Where(u => toBlock.Contains(u.UserId) && u.PayLaterAllowed).ToListAsync(ct))
        {
            user.PayLaterAllowed = false;
            logger.LogWarning("Pay later switched off for customer {CustomerId} after {Count} unpaid no-shows", user.UserId, blockAfter);
        }
        await db.SaveChangesAsync(ct);
    }
}
