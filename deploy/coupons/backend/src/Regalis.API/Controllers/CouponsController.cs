using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Coupons;

namespace Regalis.API.Controllers;

/// <summary>Coupon codes, admin portal (coupons kit): the Coupon codes tab of Sales > Offers. Same
/// permissions as offers: OFFER.VIEW to see, OFFER.MANAGE to create, edit, (de)activate and delete.</summary>
[Route("api/admin/coupons")]
public class CouponsAdminController(ICouponAdminService coupons) : ApiControllerBase
{
	[HttpGet]
	[HasPermission("OFFER.VIEW")]
	public async Task<IActionResult> List([FromQuery] CouponListRequest request, CancellationToken ct) => OkEnvelope(await coupons.ListAsync(request, ct));

	[HttpGet("{id:int}")]
	[HasPermission("OFFER.VIEW")]
	public async Task<IActionResult> Get(int id, CancellationToken ct) => OkEnvelope(await coupons.GetAsync(id, ct));

	[HttpGet("{id:int}/redemptions")]
	[HasPermission("OFFER.VIEW")]
	public async Task<IActionResult> Redemptions(int id, CancellationToken ct) => OkEnvelope(await coupons.RedemptionsAsync(id, ct));

	[HttpPost]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> Create(SaveCouponRequest request, CancellationToken ct)
	{
		CouponDetailDto c = await coupons.CreateAsync(request, ct);
		return CreatedEnvelope($"/api/admin/coupons/{c.CouponId}", c, c.IsActive ? "Coupon created and live." : "Coupon created.");
	}

	[HttpPut("{id:int}")]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> Update(int id, SaveCouponRequest request, CancellationToken ct) => OkEnvelope(await coupons.UpdateAsync(id, request, ct), "Coupon saved.");

	[HttpPost("{id:int}/activate")]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> Activate(int id, CancellationToken ct) => OkEnvelope(await coupons.SetActiveAsync(id, true, ct), "Coupon activated.");

	[HttpPost("{id:int}/deactivate")]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> Deactivate(int id, CancellationToken ct) => OkEnvelope(await coupons.SetActiveAsync(id, false, ct), "Coupon deactivated.");

	[HttpDelete("{id:int}")]
	[HasPermission("OFFER.MANAGE")]
	public async Task<IActionResult> Delete(int id, CancellationToken ct)
	{
		await coupons.DeleteAsync(id, ct);
		return OkEnvelope("Coupon deleted.");
	}
}

/// <summary>Coupons in the customer portal. There is no list: a code works only when typed. Signed-in users; preview and apply are for customers only
/// and act on the caller's own bookings. Passes and memberships take the code on the existing
/// /api/passes/order and /api/passes/purchase calls (couponCode).</summary>
[Route("api/coupons")]
public class CouponsPortalController(ICouponPortalService coupons) : ApiControllerBase
{
	/// <summary>Checks a typed code against a purchase and prices it. Always 200: Valid / Error say
	/// whether it can be used.</summary>
	[HttpPost("preview")]
	[Authorize]
	public async Task<IActionResult> Preview(CouponPreviewRequest request, CancellationToken ct) => OkEnvelope(await coupons.PreviewAsync(request, ct));

	/// <summary>Put a coupon (or, with no code, none) on the caller's held bookings before paying. It
	/// replaces any offer on them.</summary>
	[HttpPost("bookings/apply")]
	[Authorize]
	public async Task<IActionResult> ApplyToBookings(ApplyBookingCouponRequest request, CancellationToken ct) => OkEnvelope(await coupons.ApplyToBookingsAsync(request, ct), "Coupon applied.");
}
