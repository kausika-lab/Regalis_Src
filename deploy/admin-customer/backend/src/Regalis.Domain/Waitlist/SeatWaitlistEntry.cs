using System;
using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Waitlist;

/// <summary>Where a seat waiting-list entry stands. Stored as tinyint.</summary>
public enum SeatWaitlistStatus : byte
{
	/// <summary>Open: the customer is told when a desk frees up on <see cref="SeatWaitlistEntry.WaitDate" />.</summary>
	Waiting = 1,
	/// <summary>Closed: the customer now holds an active day pass that covers the date.</summary>
	Booked = 2,
	/// <summary>Closed by the customer (Passes -> My waiting list -> Remove).</summary>
	RemovedByCustomer = 3,
	/// <summary>Closed by staff (Admin -> Waiting List -> Remove), with a reason.</summary>
	RemovedByStaff = 4,
	/// <summary>Closed automatically once the date has passed.</summary>
	Expired = 5
}

/// <summary>
/// TRN_SeatWaitlist: a customer waiting for a desk on a day the day-pass screen showed as
/// full (no free desk at the location on that date). The waiting-list worker checks the
/// date every minute; when a desk is free it notifies every open entry for that location +
/// type + date (in-app, and by email through the customer notification email worker). No
/// seat is held: the first customer to buy gets it. Entries are never deleted, only closed.
/// </summary>
public class SeatWaitlistEntry : AuditableEntity
{
	public long SeatWaitlistId { get; set; }

	public long CustomerId { get; set; }

	public int LocationId { get; set; }

	/// <summary>The desk type the day pass needs (WorkspaceType.TypeCode = 'DESK').</summary>
	public int WorkspaceTypeId { get; set; }

	/// <summary>The day pass plan the customer was looking at, for the admin list only.</summary>
	public int? PassPlanId { get; set; }

	/// <summary>The first visit date (IST) the customer wants a desk for.</summary>
	public DateOnly WaitDate { get; set; }

	public SeatWaitlistStatus Status { get; set; } = SeatWaitlistStatus.Waiting;

	/// <summary>What the worker saw last time: true while a desk is free. A notice is sent on
	/// each change from false to true, so a desk that frees up, is taken, and frees up again
	/// is announced twice.</summary>
	public bool IsSeatFree { get; set; }

	public DateTime? LastNotifiedUtc { get; set; }

	public int NotifyCount { get; set; }

	public DateTime? ClosedUtc { get; set; }

	public long? ClosedByUserId { get; set; }

	public string? CloseReason { get; set; }
}
