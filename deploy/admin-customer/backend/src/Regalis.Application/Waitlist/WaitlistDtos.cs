using System;
using System.Threading;
using System.Threading.Tasks;
using Regalis.Shared.Api;

namespace Regalis.Application.Waitlist;

/// <summary>Customer: join the waiting list for a day-pass first visit date that is full.
/// LocationId null = the default location (what the day-pass screen uses).</summary>
public record JoinWaitlistRequest(DateOnly WaitDate, int? LocationId = null, int? PassPlanId = null);

/// <summary>Staff removal; the reason is required and shown to the customer.</summary>
public record RemoveWaitlistRequest(string? Reason);

public class WaitlistListRequest : PagedRequest
{
	public int? LocationId { get; set; }

	public DateOnly? From { get; set; }

	public DateOnly? To { get; set; }

	/// <summary>1 Waiting (default when empty), 2 Booked, 3 Removed by customer,
	/// 4 Removed by staff, 5 Expired; 0 = every status.</summary>
	public byte? Status { get; set; }
}

/// <param name="Position">1 = waiting longest for that location/type/date (open entries only; 0 when closed).</param>
public record SeatWaitlistDto(long SeatWaitlistId, long CustomerId, string CustomerName, string? CustomerEmail, string? CustomerMobile,
	int LocationId, string LocationName, int WorkspaceTypeId, string WorkspaceTypeName, int? PassPlanId, string? PassPlanName,
	DateOnly WaitDate, byte Status, string StatusName, int Position, bool IsSeatFree, DateTime JoinedUtc,
	DateTime? LastNotifiedUtc, int NotifyCount, DateTime? ClosedUtc, string? CloseReason);

public interface IWaitlistService
{
	Task<SeatWaitlistDto> JoinAsync(JoinWaitlistRequest request, CancellationToken ct = default);

	/// <summary>The signed-in customer's open entries, soonest date first.</summary>
	Task<System.Collections.Generic.List<SeatWaitlistDto>> GetMineAsync(CancellationToken ct = default);

	Task RemoveMineAsync(long seatWaitlistId, CancellationToken ct = default);

	Task<PagedResult<SeatWaitlistDto>> ListAsync(WaitlistListRequest request, CancellationToken ct = default);

	Task RemoveAsync(long seatWaitlistId, RemoveWaitlistRequest request, CancellationToken ct = default);
}
