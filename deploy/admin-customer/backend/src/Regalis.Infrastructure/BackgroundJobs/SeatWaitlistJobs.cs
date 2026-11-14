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
using Regalis.Application.Bookings;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Entities.Waitlist;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.BackgroundJobs;

/// <summary>
/// One pass over the open seat waiting-list entries:
/// 1. a date that has passed closes as Expired;
/// 2. a customer who now holds an active day pass covering the date closes as Booked;
/// 3. for each remaining location + type + date, the day's desk slots are read with the same
///    IAvailabilityService.GetSlotsAsync the day-pass screen uses. When a desk is free, every
///    open entry that last saw "full" gets an in-app notification (Category Pass), which the
///    customer notification email worker then emails to the address on the account. No seat
///    is held; the first to buy gets it. When the date is full again the entries re-arm, so
///    the next freed desk is announced too.
/// </summary>
public class SeatWaitlistJobs(RegalisDbContext db, IAvailabilityService availability, IDateTimeProvider clock, ILogger<SeatWaitlistJobs> logger)
{
	public async Task<int> RunAsync(CancellationToken ct)
	{
		DateTime nowUtc = clock.UtcNow;
		DateOnly todayIst = IstClock.IstDateOf(nowUtc);
		List<SeatWaitlistEntry> open = await db.Set<SeatWaitlistEntry>().Where(e => e.Status == SeatWaitlistStatus.Waiting).ToListAsync(ct);
		if (open.Count == 0)
		{
			return 0;
		}
		int notified = 0;
		foreach (SeatWaitlistEntry entry in open.Where(e => e.WaitDate < todayIst))
		{
			entry.Status = SeatWaitlistStatus.Expired;
			entry.ClosedUtc = nowUtc;
			entry.CloseReason = "The date has passed.";
		}
		List<SeatWaitlistEntry> live = open.Where(e => e.Status == SeatWaitlistStatus.Waiting).ToList();
		List<long> customerIds = live.Select(e => e.CustomerId).Distinct().ToList();
		var passes = await db.CustomerPasses.AsNoTracking()
			.Where(p => customerIds.Contains(p.CustomerId) && p.PassType == PassType.DayPass && p.Status == CustomerPassStatus.Active && p.ValidTo >= todayIst)
			.Select(p => new { p.CustomerId, p.ValidFrom, p.ValidTo }).ToListAsync(ct);
		foreach (SeatWaitlistEntry entry in live)
		{
			if (passes.Any(p => p.CustomerId == entry.CustomerId && p.ValidFrom <= entry.WaitDate && entry.WaitDate <= p.ValidTo))
			{
				entry.Status = SeatWaitlistStatus.Booked;
				entry.ClosedUtc = nowUtc;
				entry.CloseReason = "A day pass covering this date was bought.";
			}
		}
		Dictionary<int, string> locationNames = await db.Locations.AsNoTracking().ToDictionaryAsync(l => l.LocationId, l => l.LocationName, ct);
		foreach (var group in live.Where(e => e.Status == SeatWaitlistStatus.Waiting).GroupBy(e => new { e.LocationId, e.WorkspaceTypeId, e.WaitDate }))
		{
			List<SlotDto> slots;
			try
			{
				slots = await availability.GetSlotsAsync(group.Key.LocationId, group.Key.WorkspaceTypeId, group.Key.WaitDate, ct);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				logger.LogWarning(ex, "Seat waiting list: could not read availability for location {LocationId} on {Date}", group.Key.LocationId, group.Key.WaitDate);
				continue;
			}
			bool free = slots.Any(s => s.IsAvailable);
			foreach (SeatWaitlistEntry entry in group)
			{
				if (free && !entry.IsSeatFree)
				{
					string where = locationNames.TryGetValue(entry.LocationId, out var name) ? " at " + name : "";
					db.Notifications.Add(new Notification
					{
						UserId = entry.CustomerId,
						Audience = NotificationAudience.SingleUser,
						Title = $"A desk is free on {entry.WaitDate:d MMM}",
						Message = $"A desk has opened up{where} on {entry.WaitDate:dddd, d MMMM yyyy}, the date on your waiting list. Buy your day pass now - the first member to book gets it. Your waiting-list entry stays open until you buy, remove it, or the date passes.",
						Category = NotificationCategory.Pass,
						Priority = NotificationPriority.High,
						ReferenceType = "SeatWaitlist",
						ReferenceId = entry.SeatWaitlistId,
						ActionLabel = "Buy day pass",
						ActionRoute = $"/passes?date={entry.WaitDate:yyyy-MM-dd}",
						SentUtc = nowUtc
					});
					entry.LastNotifiedUtc = nowUtc;
					entry.NotifyCount++;
					notified++;
				}
				entry.IsSeatFree = free;
			}
		}
		await db.SaveChangesAsync(ct);
		if (notified > 0)
		{
			logger.LogInformation("Seat waiting list: {Count} customer(s) told a desk is free", notified);
		}
		return notified;
	}
}

/// <summary>Runs <see cref="SeatWaitlistJobs" /> every minute.</summary>
public class SeatWaitlistWorker(IServiceScopeFactory scopeFactory, ILogger<SeatWaitlistWorker> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		using PeriodicTimer timer = new PeriodicTimer(TimeSpan.FromMinutes(1.0));
		do
		{
			try
			{
				using IServiceScope scope = scopeFactory.CreateScope();
				await scope.ServiceProvider.GetRequiredService<SeatWaitlistJobs>().RunAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception exception)
			{
				logger.LogError(exception, "Seat waiting list job failed");
			}
		}
		while (await timer.WaitForNextTickAsync(stoppingToken));
	}
}
