using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.BackgroundJobs;

/// <summary>
/// Sweeps Pending bookings whose payment window (Booking.PendingHoldMinutes,
/// default 10) has lapsed to Expired, releasing the slot — the code path of
/// usp_ExpireStaleBookingHolds (DATABASE_DESIGN §10). ChangedByUserId 0 = system.
/// </summary>
public class BookingHoldSweeper(IServiceScopeFactory scopeFactory, ILogger<BookingHoldSweeper> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(60);

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
                return;
            }
            catch (Exception exception)
            {
                // The sweeper must survive transient failures; the next tick retries.
                logger.LogError(exception, "Booking hold sweep failed");
            }

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var settings = scope.ServiceProvider.GetRequiredService<ISettingService>();
        var clock = scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();

        var holdMinutes = await settings.GetIntAsync(AppSettingKeys.Booking.PendingHoldMinutes, 10, ct);
        var nowUtc = clock.UtcNow;
        var cutoffUtc = nowUtc.AddMinutes(-holdMinutes);

        var staleHolds = await db.Bookings
            .Where(b => b.Status == BookingStatus.Pending && b.CreatedDate < cutoffUtc)
            .ToListAsync(ct);
        if (staleHolds.Count == 0)
        {
            return;
        }

        foreach (var booking in staleHolds)
        {
            booking.Status = BookingStatus.Expired;
            booking.StatusHistory.Add(new BookingStatusHistory
            {
                BookingId = booking.BookingId,
                FromStatus = BookingStatus.Pending,
                ToStatus = BookingStatus.Expired,
                ChangedByUserId = 0,
                ChangedUtc = nowUtc,
                Reason = $"Hold expired after {holdMinutes} minutes without payment."
            });
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Expired {Count} stale booking hold(s)", staleHolds.Count);

        // Passes past their last valid day → Expired (usp_ExpirePasses equivalent).
        var todayIst = Shared.IstClock.IstDateOf(nowUtc);
        var stalePasses = await db.CustomerPasses
            .Where(p => p.Status == Domain.Enums.CustomerPassStatus.Active && p.ValidTo < todayIst)
            .ToListAsync(ct);
        if (stalePasses.Count > 0)
        {
            foreach (var pass in stalePasses)
            {
                pass.Status = Domain.Enums.CustomerPassStatus.Expired;
            }
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Expired {Count} pass(es)", stalePasses.Count);
        }

        // Attendance left open past Attendance.AutoCheckoutHour (IST) → AutoClosed
        // (usp_AutoCloseAttendance equivalent). Corrections stay auditable.
        var autoCloseHour = await settings.GetIntAsync(
            Shared.Constants.AppSettingKeys.Attendance.AutoCheckoutHour, 23, ct);
        var openRows = await db.Attendances
            .Where(a => a.Status == Domain.Enums.AttendanceStatus.CheckedIn)
            .ToListAsync(ct);
        var closed = 0;
        foreach (var attendance in openRows)
        {
            var closeAtUtc = Shared.IstClock.ToUtc(
                attendance.AttendanceDate.ToDateTime(new TimeOnly(Math.Min(autoCloseHour, 23), 0)));
            if (nowUtc >= closeAtUtc)
            {
                attendance.CheckOutUtc = closeAtUtc;
                attendance.TotalMinutes = (int)Math.Max(0,
                    Math.Round((closeAtUtc - attendance.CheckInUtc).TotalMinutes));
                attendance.Status = Domain.Enums.AttendanceStatus.AutoClosed;
                attendance.IsException = true;
                attendance.ExceptionNotes = $"Auto-closed at {autoCloseHour}:00 IST — no check-out recorded.";
                closed++;
            }
        }
        if (closed > 0)
        {
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Auto-closed {Count} attendance record(s)", closed);
        }
    }
}
