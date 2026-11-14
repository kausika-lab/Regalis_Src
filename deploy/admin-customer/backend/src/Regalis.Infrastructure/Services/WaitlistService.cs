using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Bookings;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Waitlist;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Waitlist;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Seat waiting list for day passes (13 Nov 2026). A customer may join only for a date the
/// day-pass screen shows as full: the location is open that day and no desk slot is free
/// (same IAvailabilityService.GetSlotsAsync the screen calls). Customers see and remove their
/// own open entries; staff list every entry and remove one with a reason. Nothing is deleted:
/// entries are closed with a status.
/// </summary>
public class WaitlistService(RegalisDbContext db, IAvailabilityService availability, ICurrentUser currentUser, IDateTimeProvider clock, ILogger<WaitlistService> logger) : IWaitlistService
{
	/// <summary>How far ahead a customer can wait; matches the day-pass date picker's practical range.</summary>
	public const int MaxDaysAhead = 90;

	public async Task<SeatWaitlistDto> JoinAsync(JoinWaitlistRequest request, CancellationToken ct = default)
	{
		EnsureCustomer();
		long customerId = currentUser.UserId;
		DateOnly todayIst = IstClock.IstDateOf(clock.UtcNow);
		if (request.WaitDate < todayIst)
		{
			throw new BusinessRuleException("Pick today or a later date.");
		}
		if (request.WaitDate > todayIst.AddDays(MaxDaysAhead))
		{
			throw new BusinessRuleException($"You can join the waiting list up to {MaxDaysAhead} days ahead.");
		}
		Location location = await ResolveLocationAsync(request.LocationId, ct);
		int deskTypeId = await DeskTypeIdAsync(ct);
		if (request.PassPlanId.HasValue && !await db.PassPlans.AnyAsync(p => p.PassPlanId == request.PassPlanId && p.IsActive, ct))
		{
			throw new NotFoundException("PassPlan", request.PassPlanId.Value);
		}
		SeatWaitlistEntry existing = await db.Set<SeatWaitlistEntry>().FirstOrDefaultAsync(e => e.CustomerId == customerId && e.LocationId == location.LocationId && e.WorkspaceTypeId == deskTypeId && e.WaitDate == request.WaitDate && e.Status == SeatWaitlistStatus.Waiting, ct);
		if (existing != null)
		{
			return (await ShapeAsync(db.Set<SeatWaitlistEntry>().AsNoTracking().Where(e => e.SeatWaitlistId == existing.SeatWaitlistId), ct)).Single();
		}
		List<SlotDto> slots = await availability.GetSlotsAsync(location.LocationId, deskTypeId, request.WaitDate, ct);
		if (slots.Count == 0)
		{
			throw new BusinessRuleException($"{location.LocationName} is closed on {request.WaitDate:ddd d MMM yyyy}. Pick another date.");
		}
		if (slots.Any(s => s.IsAvailable))
		{
			throw new BusinessRuleException($"A desk is free on {request.WaitDate:ddd d MMM yyyy} - you can buy your day pass now.", "SEAT_AVAILABLE");
		}
		SeatWaitlistEntry entry = new SeatWaitlistEntry
		{
			CustomerId = customerId,
			LocationId = location.LocationId,
			WorkspaceTypeId = deskTypeId,
			PassPlanId = request.PassPlanId,
			WaitDate = request.WaitDate,
			Status = SeatWaitlistStatus.Waiting,
			IsSeatFree = false
		};
		db.Set<SeatWaitlistEntry>().Add(entry);
		await db.SaveChangesAsync(ct);
		logger.LogInformation("Customer {CustomerId} joined the seat waiting list for {Date} at location {LocationId} (entry {Id})", customerId, request.WaitDate, location.LocationId, entry.SeatWaitlistId);
		return (await ShapeAsync(db.Set<SeatWaitlistEntry>().AsNoTracking().Where(e => e.SeatWaitlistId == entry.SeatWaitlistId), ct)).Single();
	}

	public async Task<List<SeatWaitlistDto>> GetMineAsync(CancellationToken ct = default)
	{
		EnsureCustomer();
		long customerId = currentUser.UserId;
		DateOnly todayIst = IstClock.IstDateOf(clock.UtcNow);
		return await ShapeAsync(from e in db.Set<SeatWaitlistEntry>().AsNoTracking()
			where e.CustomerId == customerId && e.Status == SeatWaitlistStatus.Waiting && e.WaitDate >= todayIst
			orderby e.WaitDate, e.SeatWaitlistId
			select e, ct);
	}

	public async Task RemoveMineAsync(long seatWaitlistId, CancellationToken ct = default)
	{
		EnsureCustomer();
		SeatWaitlistEntry entry = await db.Set<SeatWaitlistEntry>().FirstOrDefaultAsync(e => e.SeatWaitlistId == seatWaitlistId && e.CustomerId == currentUser.UserId, ct) ?? throw new NotFoundException("Waiting list entry", seatWaitlistId);
		if (entry.Status != SeatWaitlistStatus.Waiting)
		{
			throw new BusinessRuleException("This waiting-list entry is already closed.");
		}
		Close(entry, SeatWaitlistStatus.RemovedByCustomer, "Removed by the customer.");
		await db.SaveChangesAsync(ct);
	}

	public async Task<PagedResult<SeatWaitlistDto>> ListAsync(WaitlistListRequest request, CancellationToken ct = default)
	{
		EnsureStaff();
		IQueryable<SeatWaitlistEntry> query = db.Set<SeatWaitlistEntry>().AsNoTracking();
		byte status = request.Status ?? (byte)SeatWaitlistStatus.Waiting;
		if (status != 0)
		{
			query = query.Where(e => (byte)e.Status == status);
		}
		if (request.LocationId.HasValue)
		{
			query = query.Where(e => e.LocationId == request.LocationId);
		}
		if (request.From.HasValue)
		{
			query = query.Where(e => e.WaitDate >= request.From);
		}
		if (request.To.HasValue)
		{
			query = query.Where(e => e.WaitDate <= request.To);
		}
		if (!string.IsNullOrWhiteSpace(request.Search))
		{
			string term = request.Search.Trim();
			List<long> ids = await db.Users.AsNoTracking().Where(u => u.FullName.Contains(term) || u.Email.Contains(term) || u.MobileNumber.Contains(term)).Select(u => u.UserId).ToListAsync(ct);
			query = query.Where(e => ids.Contains(e.CustomerId));
		}
		int total = await query.CountAsync(ct);
		List<SeatWaitlistDto> items = await ShapeAsync(query.OrderBy(e => e.WaitDate).ThenBy(e => e.SeatWaitlistId).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize), ct);
		return PagedResult<SeatWaitlistDto>.Create(items, request.Page, request.PageSize, total);
	}

	public async Task RemoveAsync(long seatWaitlistId, RemoveWaitlistRequest request, CancellationToken ct = default)
	{
		EnsureStaff();
		string reason = request.Reason?.Trim();
		if (string.IsNullOrEmpty(reason))
		{
			throw new BusinessRuleException("Enter a reason for removing this entry.");
		}
		if (reason.Length > 300)
		{
			throw new BusinessRuleException("Keep the reason under 300 characters.");
		}
		SeatWaitlistEntry entry = await db.Set<SeatWaitlistEntry>().FirstOrDefaultAsync(e => e.SeatWaitlistId == seatWaitlistId, ct) ?? throw new NotFoundException("Waiting list entry", seatWaitlistId);
		if (entry.Status != SeatWaitlistStatus.Waiting)
		{
			throw new BusinessRuleException("This waiting-list entry is already closed.");
		}
		Close(entry, SeatWaitlistStatus.RemovedByStaff, reason);
		db.Notifications.Add(new Regalis.Domain.Entities.Operations.Notification
		{
			UserId = entry.CustomerId,
			Audience = NotificationAudience.SingleUser,
			Title = "Removed from the waiting list",
			Message = $"Your waiting-list entry for a desk on {entry.WaitDate:ddd d MMM yyyy} was removed by the front desk. Reason: {reason}",
			Category = NotificationCategory.Pass,
			Priority = NotificationPriority.Normal,
			ReferenceType = "SeatWaitlist",
			ReferenceId = entry.SeatWaitlistId,
			ActionLabel = "View passes",
			ActionRoute = "/passes",
			SentUtc = clock.UtcNow
		});
		await db.SaveChangesAsync(ct);
		logger.LogInformation("Waiting-list entry {Id} removed by staff {UserId}: {Reason}", seatWaitlistId, currentUser.UserId, reason);
	}

	private void Close(SeatWaitlistEntry entry, SeatWaitlistStatus status, string reason)
	{
		entry.Status = status;
		entry.ClosedUtc = clock.UtcNow;
		entry.ClosedByUserId = currentUser.UserId;
		entry.CloseReason = reason;
	}

	private void EnsureCustomer()
	{
		if (currentUser.UserType != UserType.Customer)
		{
			throw new ForbiddenException("The waiting list is for members buying their own day pass.");
		}
	}

	private void EnsureStaff()
	{
		if (currentUser.UserType == UserType.Customer)
		{
			throw new ForbiddenException();
		}
	}

	private async Task<Location> ResolveLocationAsync(int? locationId, CancellationToken ct)
	{
		IQueryable<Location> active = db.Locations.AsNoTracking().Where(l => l.IsActive);
		Location location = locationId.HasValue
			? await active.FirstOrDefaultAsync(l => l.LocationId == locationId, ct)
			: (await active.FirstOrDefaultAsync(l => l.IsDefault, ct) ?? await active.OrderBy(l => l.LocationId).FirstOrDefaultAsync(ct));
		return location ?? throw new NotFoundException("Location", locationId ?? 0);
	}

	internal async Task<int> DeskTypeIdAsync(CancellationToken ct)
	{
		int? id = await db.WorkspaceTypes.AsNoTracking().Where(t => t.TypeCode == "DESK" && t.IsActive).Select(t => (int?)t.WorkspaceTypeId).FirstOrDefaultAsync(ct);
		return id ?? throw new BusinessRuleException("No desk workspace type (code DESK) is set up.");
	}

	/// <summary>Entries joined to names, plus the queue position of each open entry.</summary>
	private async Task<List<SeatWaitlistDto>> ShapeAsync(IQueryable<SeatWaitlistEntry> entries, CancellationToken ct)
	{
		var rows = await (from e in entries
			join u in db.Users on e.CustomerId equals u.UserId
			join l in db.Locations on e.LocationId equals l.LocationId
			join t in db.WorkspaceTypes on e.WorkspaceTypeId equals t.WorkspaceTypeId
			select new { Entry = e, u.FullName, u.Email, u.MobileNumber, l.LocationName, t.TypeName }).ToListAsync(ct);
		List<int> planIds = rows.Where(r => r.Entry.PassPlanId.HasValue).Select(r => r.Entry.PassPlanId.Value).Distinct().ToList();
		Dictionary<int, string> plans = planIds.Count == 0 ? new Dictionary<int, string>() : await db.PassPlans.AsNoTracking().Where(p => planIds.Contains(p.PassPlanId)).ToDictionaryAsync(p => p.PassPlanId, p => p.PlanName, ct);
		// Queue position among open entries for the same location / type / date.
		var keys = rows.Where(r => r.Entry.Status == SeatWaitlistStatus.Waiting).Select(r => new { r.Entry.LocationId, r.Entry.WorkspaceTypeId, r.Entry.WaitDate }).Distinct().ToList();
		Dictionary<long, int> positions = new Dictionary<long, int>();
		foreach (var key in keys)
		{
			List<long> queue = await db.Set<SeatWaitlistEntry>().AsNoTracking()
				.Where(e => e.Status == SeatWaitlistStatus.Waiting && e.LocationId == key.LocationId && e.WorkspaceTypeId == key.WorkspaceTypeId && e.WaitDate == key.WaitDate)
				.OrderBy(e => e.SeatWaitlistId).Select(e => e.SeatWaitlistId).ToListAsync(ct);
			for (int i = 0; i < queue.Count; i++)
			{
				positions[queue[i]] = i + 1;
			}
		}
		return rows.Select(r => new SeatWaitlistDto(r.Entry.SeatWaitlistId, r.Entry.CustomerId, r.FullName, r.Email, r.MobileNumber,
			r.Entry.LocationId, r.LocationName, r.Entry.WorkspaceTypeId, r.TypeName, r.Entry.PassPlanId,
			r.Entry.PassPlanId.HasValue && plans.TryGetValue(r.Entry.PassPlanId.Value, out var planName) ? planName : null,
			r.Entry.WaitDate, (byte)r.Entry.Status, StatusName(r.Entry.Status), positions.TryGetValue(r.Entry.SeatWaitlistId, out var pos) ? pos : 0,
			r.Entry.IsSeatFree, r.Entry.CreatedDate, r.Entry.LastNotifiedUtc, r.Entry.NotifyCount, r.Entry.ClosedUtc, r.Entry.CloseReason)).ToList();
	}

	public static string StatusName(SeatWaitlistStatus status) => status switch
	{
		SeatWaitlistStatus.Waiting => "Waiting",
		SeatWaitlistStatus.Booked => "Booked",
		SeatWaitlistStatus.RemovedByCustomer => "Removed by customer",
		SeatWaitlistStatus.RemovedByStaff => "Removed by staff",
		SeatWaitlistStatus.Expired => "Expired",
		_ => status.ToString()
	};
}
