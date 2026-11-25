using System;
using System.Collections.Generic;
using Regalis.Application.Offers;
using Regalis.Shared.Api;

namespace Regalis.Application.Coupons;

// ---- admin ---------------------------------------------------------------------------------------

/// <summary>Status: all | active | inactive | expired | scheduled. Module: 1 bookings, 2 passes,
/// 3 membership. Sort: newest (default) | code | ending.</summary>
public class CouponListRequest : PagedRequest
{
	public string? Status { get; set; }

	public byte? Module { get; set; }

	public string? Sort { get; set; }
}

public record CouponSummaryDto(int Total, int Active, int Inactive, int Expired, int Scheduled);

/// <summary>OfferClash: an offer uses the same code (an offer saved after the coupon). The coupon is
/// the one customers get when they type it.</summary>
public record CouponRowDto(int CouponId, string Code, string? Description, byte DiscountType, decimal DiscountValue, decimal? MaxDiscountAmount, decimal? MinAmount, IReadOnlyList<byte> Modules, DateOnly ValidFrom, DateOnly ValidUntil, int? TotalUses, int? UsesPerCustomer, bool ShowInPortal, bool IsActive, string Status, DateTime CreatedDate, int Uses, decimal DiscountGiven, bool OfferClash);

public record CouponListDto(CouponSummaryDto Summary, PagedResult<CouponRowDto> Page);

public record CouponDetailDto(int CouponId, string Code, string? Description, byte DiscountType, decimal DiscountValue, decimal? MaxDiscountAmount, decimal? MinAmount, IReadOnlyList<byte> Modules, DateOnly ValidFrom, DateOnly ValidUntil, int? TotalUses, int? UsesPerCustomer, bool ShowInPortal, bool IsActive, string Status, DateTime CreatedDate, DateTime? ModifiedDate, int Uses, decimal DiscountGiven, bool CanDelete, bool OfferClash);

public record SaveCouponRequest(string? Code, string? Description, byte DiscountType, decimal DiscountValue, decimal? MaxDiscountAmount, decimal? MinAmount, IReadOnlyList<byte>? Modules, DateOnly? ValidFrom, DateOnly? ValidUntil, int? TotalUses, int? UsesPerCustomer, bool ShowInPortal, bool IsActive);

/// <summary>Same states as an offer's uses: Used, Held (awaiting payment), Released (given back).</summary>
public record CouponRedemptionRowDto(long RedemptionId, DateTime CreatedUtc, long CustomerId, string CustomerName, byte Module, string Reference, decimal BaseAmount, decimal DiscountAmount, string State);

// ---- customer portal -----------------------------------------------------------------------------

/// <summary>A listed coupon as the customer sees it. UsesLeft is for this customer (null = no limit).</summary>
public record PortalCouponDto(int CouponId, string Code, string? Description, byte DiscountType, decimal DiscountValue, decimal? MaxDiscountAmount, decimal? MinAmount, DateOnly ValidFrom, DateOnly ValidUntil, IReadOnlyList<byte> Modules, int? UsesLeft);

/// <summary>Module: "booking" | "pass" | "membership". For a booking give WorkspaceId and the price
/// before GST of each booking (one per day of a multi-day range); for a pass or membership give
/// PassPlanId (the server prices the plan).</summary>
public record CouponPreviewRequest(string? Module, string? Code, int? PassPlanId, int? WorkspaceId, IReadOnlyList<decimal>? BaseAmounts);

public record CouponAppliedDto(int CouponId, string Code, string? Description, byte DiscountType, decimal DiscountValue, decimal DiscountAmount);

/// <summary>Found: false when no coupon has this code (the portal then tries it as an offer code).
/// Valid: the coupon can be used on this purchase; otherwise Error / ErrorCode say why
/// (COUPON_INVALID, COUPON_EXPIRED, COUPON_INACTIVE, COUPON_NOT_STARTED, COUPON_NOT_APPLICABLE,
/// COUPON_MIN_AMOUNT, COUPON_ALREADY_USED, COUPON_LIMIT_REACHED) and the totals are the price
/// without a discount. The amounts are what checkout will charge, item by item. Applied has the
/// coupon as one line (OfferId = 0) so the portal shows it like an applied offer.</summary>
public record CouponPreviewDto(bool Found, bool Valid, string? Error, string? ErrorCode, CouponAppliedDto? Coupon, IReadOnlyList<OfferAppliedDto> Applied, decimal BaseAmount, decimal DiscountAmount, decimal TaxableAmount, decimal CgstPercent, decimal CgstAmount, decimal SgstPercent, decimal SgstAmount, decimal TotalAmount, decimal OriginalTotal, IReadOnlyList<OfferPriceItemDto> Items);

/// <summary>Put a coupon on the customer's own held bookings (one, or every day of a multi-day range).
/// It replaces any offer on them. An empty Code removes the coupon.</summary>
public record ApplyBookingCouponRequest(IReadOnlyList<long>? BookingIds, string? Code);
