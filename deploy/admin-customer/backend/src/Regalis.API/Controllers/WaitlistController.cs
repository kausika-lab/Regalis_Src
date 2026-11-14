using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Waitlist;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>
/// Seat waiting list for day passes. Uses the existing pass permissions (no new codes):
/// customers join and remove their own entries with PASS.CREATE (what they buy passes with);
/// staff read the list with PASS.VIEW and remove entries with PASS.EDIT. The service also
/// checks the user type, so a customer can never reach the staff list and vice versa.
/// </summary>
[Route("api/waitlist")]
public class WaitlistController(IWaitlistService waitlist) : ApiControllerBase
{
	[HttpPost]
	[HasPermission("PASS.CREATE")]
	[ProducesResponseType(typeof(ApiResponse<SeatWaitlistDto>), 200)]
	public async Task<IActionResult> Join(JoinWaitlistRequest request, CancellationToken ct)
	{
		return OkEnvelope(await waitlist.JoinAsync(request, ct), "You are on the waiting list.");
	}

	[HttpGet("my")]
	[HasPermission("PASS.CREATE")]
	[ProducesResponseType(typeof(ApiResponse<List<SeatWaitlistDto>>), 200)]
	public async Task<IActionResult> GetMine(CancellationToken ct)
	{
		return OkEnvelope(await waitlist.GetMineAsync(ct));
	}

	[HttpPost("my/{id:long}/remove")]
	[HasPermission("PASS.CREATE")]
	public async Task<IActionResult> RemoveMine(long id, CancellationToken ct)
	{
		await waitlist.RemoveMineAsync(id, ct);
		return OkEnvelope("Removed from the waiting list.");
	}

	[HttpGet]
	[HasPermission("PASS.VIEW")]
	[ProducesResponseType(typeof(ApiResponse<PagedResult<SeatWaitlistDto>>), 200)]
	public async Task<IActionResult> List([FromQuery] WaitlistListRequest request, CancellationToken ct)
	{
		return OkEnvelope(await waitlist.ListAsync(request, ct));
	}

	[HttpPost("{id:long}/remove")]
	[HasPermission("PASS.EDIT")]
	public async Task<IActionResult> Remove(long id, RemoveWaitlistRequest request, CancellationToken ct)
	{
		await waitlist.RemoveAsync(id, request, ct);
		return OkEnvelope("Entry removed from the waiting list.");
	}
}
