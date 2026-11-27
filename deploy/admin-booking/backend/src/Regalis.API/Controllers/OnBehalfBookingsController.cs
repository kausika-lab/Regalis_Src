using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Bookings;
using Regalis.Application.Bookings.OnBehalf;
using Regalis.Application.Coupons;
using Regalis.Application.Finance;
using Regalis.Application.Offers;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>
/// admin-booking kit (27 Nov 2026): the admin "New booking" screen, which is the customer
/// "Book a Workspace" screen without payment. Every call names the customer in the route and
/// runs the customer booking engine for that customer (see IOnBehalfBookingService). Staff
/// only: a customer login is refused before model binding, whatever its permissions.
/// Quotes, slots and availability use the existing endpoints (they do not depend on the caller).
/// </summary>
[Route("api/admin/on-behalf/{customerId:long}")]
[StaffOnly]
public class OnBehalfBookingsController(IOnBehalfBookingService onBehalf) : ApiControllerBase
{
	/// <summary>A Pending hold for the customer (same rules as POST /api/bookings).</summary>
	[HttpPost("bookings")]
	[HasPermission("BOOKING.CREATE")]
	[ProducesResponseType(typeof(ApiResponse<BookingDto>), 201)]
	[ProducesResponseType(typeof(ApiResponse), 409)]
	public async Task<IActionResult> Hold(long customerId, CreateBookingRequest request, CancellationToken ct)
	{
		BookingDto hold = await onBehalf.CreateHoldAsync(customerId, request, ct);
		return CreatedEnvelope($"/api/bookings/{hold.BookingId}", hold, "Slot held for the customer.");
	}

	/// <summary>A Pending hold on a desk or cabin for a monthly term (same rules as POST /api/bookings/monthly).</summary>
	[HttpPost("bookings/monthly")]
	[HasPermission("BOOKING.CREATE")]
	[ProducesResponseType(typeof(ApiResponse<BookingDto>), 201)]
	[ProducesResponseType(typeof(ApiResponse), 409)]
	public async Task<IActionResult> HoldMonthly(long customerId, MonthlyBookingRequest request, CancellationToken ct)
	{
		BookingDto hold = await onBehalf.CreateMonthlyHoldAsync(customerId, request, ct);
		return CreatedEnvelope($"/api/bookings/{hold.BookingId}", hold, "Workspace held for the customer for the term.");
	}

	/// <summary>Confirms a hold made on this screen without payment: the invoice is raised and
	/// stays due; no payment, receipt or wallet row is written.</summary>
	[HttpPost("bookings/{bookingId:long}/confirm")]
	[HasPermission("BOOKING.CREATE")]
	[ProducesResponseType(typeof(ApiResponse<ConfirmBookingResponse>), 200)]
	[ProducesResponseType(typeof(ApiResponse), 409)]
	public async Task<IActionResult> Confirm(long customerId, long bookingId, CancellationToken ct)
	{
		return OkEnvelope(await onBehalf.ConfirmAsync(customerId, bookingId, ct), "Booking confirmed. The amount is due from the customer.");
	}

	/// <summary>Offers for the customer's purchase, priced as the customer screen prices them.</summary>
	[HttpPost("offers/preview")]
	[HasPermission("BOOKING.CREATE")]
	public async Task<IActionResult> PreviewOffers(long customerId, OfferPreviewRequest request, CancellationToken ct)
	{
		return OkEnvelope(await onBehalf.PreviewOffersAsync(customerId, request, ct));
	}

	[HttpPost("offers/bookings/apply")]
	[HasPermission("BOOKING.CREATE")]
	public async Task<IActionResult> ApplyOffers(long customerId, ApplyBookingOffersRequest request, CancellationToken ct)
	{
		return OkEnvelope(await onBehalf.ApplyOffersAsync(customerId, request, ct));
	}

	/// <summary>A typed coupon code checked for the customer, as the customer screen checks it.</summary>
	[HttpPost("coupons/preview")]
	[HasPermission("BOOKING.CREATE")]
	public async Task<IActionResult> PreviewCoupon(long customerId, CouponPreviewRequest request, CancellationToken ct)
	{
		return OkEnvelope(await onBehalf.PreviewCouponAsync(customerId, request, ct));
	}

	[HttpPost("coupons/bookings/apply")]
	[HasPermission("BOOKING.CREATE")]
	public async Task<IActionResult> ApplyCoupon(long customerId, ApplyBookingCouponRequest request, CancellationToken ct)
	{
		return OkEnvelope(await onBehalf.ApplyCouponAsync(customerId, request, ct));
	}

	/// <summary>A Virtual Office for the customer, reserved without payment (AwaitingPayment until paid).</summary>
	[HttpPost("virtual-office")]
	[HasPermission("PASS.CREATE")]
	[ProducesResponseType(typeof(ApiResponse<PurchasePassResponse>), 200)]
	public async Task<IActionResult> VirtualOffice(long customerId, OnBehalfPassRequest request, CancellationToken ct)
	{
		return OkEnvelope(await onBehalf.PurchaseVirtualOfficeAsync(customerId, request, ct), "Virtual Office reserved. The amount is due from the customer.");
	}
}
