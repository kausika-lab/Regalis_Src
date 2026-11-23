using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Offers;

namespace Regalis.API.Controllers;

/// <summary>Offers Management, admin portal (offers kit, brief §1, §8). OFFER.VIEW opens the module;
/// creating, editing, (de)activating, deleting and the picture need OFFER.MANAGE.</summary>
[Route("api/admin/offers")]
public class OffersAdminController(IOfferAdminService offers) : ApiControllerBase
{
	[HttpGet]
	[HasPermission("OFFER.VIEW")]
	public async Task<IActionResult> List([FromQuery] OfferListRequest request, CancellationToken ct) => OkEnvelope(await offers.ListAsync(request, ct));

	[HttpGet("options")]
	[HasPermission("OFFER.VIEW")]
	public async Task<IActionResult> Options(CancellationToken ct) => OkEnvelope(await offers.OptionsAsync(ct));

	[HttpGet("{id:int}")]
	[HasPermission("OFFER.VIEW")]
	public async Task<IActionResult> Get(int id, CancellationToken ct) => OkEnvelope(await offers.GetAsync(id, ct));

	[HttpGet("{id:int}/redemptions")]
	[HasPermission("OFFER.VIEW")]
	public async Task<IActionResult> Redemptions(int id, CancellationToken ct) => OkEnvelope(await offers.RedemptionsAsync(id, ct));

	[HttpPost]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> Create(SaveOfferRequest request, CancellationToken ct)
	{
		OfferDetailDto o = await offers.CreateAsync(request, ct);
		return CreatedEnvelope($"/api/admin/offers/{o.OfferId}", o, o.IsActive ? "Offer created and live." : "Offer created.");
	}

	[HttpPut("{id:int}")]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> Update(int id, SaveOfferRequest request, CancellationToken ct) => OkEnvelope(await offers.UpdateAsync(id, request, ct), "Offer saved.");

	[HttpPost("{id:int}/activate")]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> Activate(int id, CancellationToken ct) => OkEnvelope(await offers.SetActiveAsync(id, true, ct), "Offer activated.");

	[HttpPost("{id:int}/deactivate")]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> Deactivate(int id, CancellationToken ct) => OkEnvelope(await offers.SetActiveAsync(id, false, ct), "Offer deactivated.");

	[HttpDelete("{id:int}")]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> Delete(int id, CancellationToken ct)
	{
		await offers.DeleteAsync(id, ct);
		return OkEnvelope("Offer deleted.");
	}

	[HttpPost("{id:int}/image")]
	[HasPermission("OFFER.MANAGE")]
	[RequestSizeLimit(3145728L)]
	public async Task<IActionResult> SetImage(int id, IFormFile file, CancellationToken ct)
	{
		if (file == null)
		{
			return BadRequest();
		}
		using var ms = new MemoryStream();
		await using (Stream s = file.OpenReadStream())
		{
			await s.CopyToAsync(ms, ct);
		}
		return OkEnvelope(await offers.SetImageAsync(id, file.FileName, file.ContentType, ms.ToArray(), ct), "Picture saved.");
	}

	[HttpDelete("{id:int}/image")]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> RemoveImage(int id, CancellationToken ct) => OkEnvelope(await offers.RemoveImageAsync(id, ct), "Picture removed.");
}

/// <summary>Offers in the customer portal (brief §3-§7). Signed-in users; the services serve only
/// the caller's own data, and preview / apply are for customers only. Passes and memberships take
/// the chosen offer ids on the existing /api/passes/order and /api/passes/purchase calls.</summary>
[Route("api/offers")]
public class OffersPortalController(IOfferPortalService offers) : ApiControllerBase
{
	/// <summary>Offers to show in a module: booking | pass | membership.</summary>
	[HttpGet]
	[Authorize]
	public async Task<IActionResult> List([FromQuery] string? module, CancellationToken ct) => OkEnvelope(await offers.ListAsync(module, ct));

	[HttpPost("preview")]
	[Authorize]
	public async Task<IActionResult> Preview(OfferPreviewRequest request, CancellationToken ct) => OkEnvelope(await offers.PreviewAsync(request, ct));

	/// <summary>Apply (or with no offer ids, remove) offers on the caller's held bookings, before paying.</summary>
	[HttpPost("bookings/apply")]
	[Authorize]
	public async Task<IActionResult> ApplyToBookings(ApplyBookingOffersRequest request, CancellationToken ct) => OkEnvelope(await offers.ApplyToBookingsAsync(request, ct), "Offer applied.");

	/// <summary>The offer's banner picture. Anonymous, so it can be an &lt;img src&gt;; offers are public promotions.</summary>
	[HttpGet("{id:int}/image")]
	[AllowAnonymous]
	[ResponseCache(Duration = 86400, Location = ResponseCacheLocation.Any)]
	public async Task<IActionResult> Image(int id, CancellationToken ct)
	{
		var img = await offers.ImageAsync(id, ct);
		return img == null ? NotFound() : File(img.Value.Data, img.Value.ContentType);
	}
}
