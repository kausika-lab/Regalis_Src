using System.Threading;
using System.Threading.Tasks;
using Regalis.Application.Coupons;
using Regalis.Application.Finance;
using Regalis.Application.Offers;

namespace Regalis.Application.Bookings.OnBehalf;

/// <summary>
/// admin-booking kit (27 Nov 2026): the admin "New booking" screen books for a customer through
/// the same engine as the customer "Book a Workspace" screen (same validation, availability,
/// closed days, 409 on a lost slot, GST, monthly and Virtual Office rules, offers and coupons).
/// Differences from the customer screen, and only these:
///  - the customer is chosen by the staff user (<paramref name="customerId"/> on every call);
///  - nothing is paid: every booking is confirmed the pay-later way (no payment, receipt or
///    wallet row), whatever the customer's own pay-later eligibility;
///  - BookingSource is Admin ("Front desk") and CreatedByUserId is the staff user.
/// Staff only; customers can never reach it.
/// </summary>
public interface IOnBehalfBookingService
{
	Task<BookingDto> CreateHoldAsync(long customerId, CreateBookingRequest request, CancellationToken ct = default);

	Task<BookingDto> CreateMonthlyHoldAsync(long customerId, MonthlyBookingRequest request, CancellationToken ct = default);

	/// <summary>Confirms a hold made on this screen for this customer, without payment.</summary>
	Task<ConfirmBookingResponse> ConfirmAsync(long customerId, long bookingId, CancellationToken ct = default);

	Task<OfferPreviewDto> PreviewOffersAsync(long customerId, OfferPreviewRequest request, CancellationToken ct = default);

	Task<ApplyBookingOffersResultDto> ApplyOffersAsync(long customerId, ApplyBookingOffersRequest request, CancellationToken ct = default);

	Task<CouponPreviewDto> PreviewCouponAsync(long customerId, CouponPreviewRequest request, CancellationToken ct = default);

	Task<ApplyBookingOffersResultDto> ApplyCouponAsync(long customerId, ApplyBookingCouponRequest request, CancellationToken ct = default);

	/// <summary>A Virtual Office (MEM-VIRTUAL-* plan) for the customer, settled pay-later.</summary>
	Task<PurchasePassResponse> PurchaseVirtualOfficeAsync(long customerId, OnBehalfPassRequest request, CancellationToken ct = default);
}
