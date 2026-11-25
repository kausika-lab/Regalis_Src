using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Regalis.Application.Offers;

namespace Regalis.Application.Coupons;

/// <summary>Admin portal: the Coupon codes tab of Sales > Offers (OFFER.VIEW / OFFER.MANAGE).</summary>
public interface ICouponAdminService
{
	Task<CouponListDto> ListAsync(CouponListRequest request, CancellationToken ct = default);

	Task<CouponDetailDto> GetAsync(int couponId, CancellationToken ct = default);

	Task<CouponDetailDto> CreateAsync(SaveCouponRequest request, CancellationToken ct = default);

	Task<CouponDetailDto> UpdateAsync(int couponId, SaveCouponRequest request, CancellationToken ct = default);

	Task<CouponDetailDto> SetActiveAsync(int couponId, bool active, CancellationToken ct = default);

	Task DeleteAsync(int couponId, CancellationToken ct = default);

	Task<List<CouponRedemptionRowDto>> RedemptionsAsync(int couponId, CancellationToken ct = default);
}

/// <summary>Customer portal: checking a typed code against a purchase, and putting a coupon on the
/// customer's own held bookings. Coupons are never listed to customers (product-owner decision,
/// 25 Nov 2026): a code works only when the customer types it. Passes and memberships take the code on
/// the existing /passes/order and /passes/purchase calls instead.</summary>
public interface ICouponPortalService
{
	Task<CouponPreviewDto> PreviewAsync(CouponPreviewRequest request, CancellationToken ct = default);

	Task<ApplyBookingOffersResultDto> ApplyToBookingsAsync(ApplyBookingCouponRequest request, CancellationToken ct = default);
}
