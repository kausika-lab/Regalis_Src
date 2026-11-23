using System;
using System.Collections.Generic;
using Regalis.Shared.Api;

namespace Regalis.Application.Offers;

// ---- admin ---------------------------------------------------------------------------------------

/// <summary>Status: all | active | inactive | expired | scheduled. Module: 1 bookings, 2 passes,
/// 3 membership. Sort: priority (default) | newest | name | ending.</summary>
public class OfferListRequest : PagedRequest
{
	public string? Status { get; set; }

	public byte? Module { get; set; }

	public string? Sort { get; set; }
}

public record OfferSummaryDto(int Total, int Active, int Inactive, int Expired, int Scheduled);

public record OfferRowDto(int OfferId, string OfferName, string Title, string? Code, byte DiscountType, decimal DiscountValue, decimal? MaxDiscountAmount, decimal? MinAmount, IReadOnlyList<byte> Modules, DateOnly StartDate, DateOnly EndDate, bool IsActive, string Status, bool CanCombine, bool ShowInPortal, int SortOrder, DateTime CreatedDate, int Uses, decimal DiscountGiven, bool HasImage, int ImageVersion);

public record OfferListDto(OfferSummaryDto Summary, PagedResult<OfferRowDto> Page);

public record OfferDetailDto(int OfferId, string OfferName, string Title, string? Description, string? Terms, string? Code, bool ShowInPortal, byte DiscountType, decimal DiscountValue, decimal? MaxDiscountAmount, decimal? MinAmount, IReadOnlyList<byte> Modules, IReadOnlyList<int> WorkspaceTypeIds, IReadOnlyList<int> PassPlanIds, IReadOnlyList<int> MembershipPlanIds, DateOnly StartDate, DateOnly EndDate, bool IsActive, string Status, bool CanCombine, int SortOrder, int? UsesPerCustomer, int? TotalUses, bool HasImage, int ImageVersion, DateTime CreatedDate, DateTime? ModifiedDate, int Uses, decimal DiscountGiven, bool CanDelete);

/// <summary>Create / update. Empty WorkspaceTypeIds / PassPlanIds / MembershipPlanIds = every
/// workspace type or plan in that module.</summary>
public record SaveOfferRequest(string? OfferName, string? Title, string? Description, string? Terms, string? Code, bool ShowInPortal, byte DiscountType, decimal DiscountValue, decimal? MaxDiscountAmount, decimal? MinAmount, IReadOnlyList<byte>? Modules, IReadOnlyList<int>? WorkspaceTypeIds, IReadOnlyList<int>? PassPlanIds, IReadOnlyList<int>? MembershipPlanIds, DateOnly? StartDate, DateOnly? EndDate, bool IsActive, bool CanCombine, int SortOrder, int? UsesPerCustomer, int? TotalUses);

public record OfferOptionItemDto(int Id, string Name, decimal? Price);

public record OfferOptionsDto(IReadOnlyList<OfferOptionItemDto> WorkspaceTypes, IReadOnlyList<OfferOptionItemDto> PassPlans, IReadOnlyList<OfferOptionItemDto> MembershipPlans);

/// <summary>State: Used (the booking / pass went through), Held (booking awaiting payment or an
/// online order in progress), Released (cancelled, expired or abandoned: the use was given back).</summary>
public record OfferRedemptionRowDto(long RedemptionId, DateTime CreatedUtc, long CustomerId, string CustomerName, byte Module, string Reference, decimal BaseAmount, decimal DiscountAmount, string State);

// ---- customer portal -----------------------------------------------------------------------------

/// <summary>An offer as the customer sees it. AppliesTo names the plans / workspace types when the
/// admin narrowed it. UsesLeft is for this customer (null = no limit).</summary>
public record PortalOfferDto(int OfferId, string Title, string? Description, string? Terms, string? Code, byte DiscountType, decimal DiscountValue, decimal? MaxDiscountAmount, decimal? MinAmount, DateOnly StartDate, DateOnly EndDate, bool CanCombine, IReadOnlyList<byte> Modules, IReadOnlyList<string> AppliesTo, string? ImageUrl, int? UsesLeft);

/// <summary>Module: "booking" | "pass" | "membership". For a booking give WorkspaceId and the price
/// before GST of each booking (one per day of a multi-day range); for a pass or membership give
/// PassPlanId (the server prices the plan). Code: a typed code, added to OfferIds when valid.</summary>
public record OfferPreviewRequest(string? Module, IReadOnlyList<int>? OfferIds, string? Code, int? PassPlanId, int? WorkspaceId, IReadOnlyList<decimal>? BaseAmounts);

public record OfferAppliedDto(int OfferId, string Title, string? Code, decimal DiscountAmount);

public record OfferPriceItemDto(decimal BaseAmount, decimal DiscountAmount, decimal TaxableAmount, decimal CgstAmount, decimal SgstAmount, decimal TotalAmount);

/// <summary>Offers: the module's offers with whether each can be used on this purchase (Reason
/// when not). Applied / totals: the selected offers priced exactly as checkout will charge.
/// Error: why the selection (or the typed code) could not be applied; the totals are then the
/// price without offers and Applied is empty.</summary>
public record OfferPreviewDto(IReadOnlyList<PortalOfferEligibilityDto> Offers, IReadOnlyList<OfferAppliedDto> Applied, decimal BaseAmount, decimal DiscountAmount, decimal TaxableAmount, decimal CgstPercent, decimal CgstAmount, decimal SgstPercent, decimal SgstAmount, decimal TotalAmount, decimal OriginalTotal, IReadOnlyList<OfferPriceItemDto> Items, int? CodeOfferId, string? Error);

public record PortalOfferEligibilityDto(PortalOfferDto Offer, bool Eligible, string? Reason);

/// <summary>Apply offers to the customer's own held bookings (one, or every day of a multi-day
/// range). An empty OfferIds removes any offer already applied.</summary>
public record ApplyBookingOffersRequest(IReadOnlyList<long>? BookingIds, IReadOnlyList<int>? OfferIds);

public record BookingOfferResultItemDto(long BookingId, string BookingNumber, decimal BaseAmount, decimal DiscountAmount, decimal TaxAmount, decimal TotalAmount);

public record ApplyBookingOffersResultDto(IReadOnlyList<OfferAppliedDto> Applied, decimal DiscountAmount, decimal TotalAmount, IReadOnlyList<BookingOfferResultItemDto> Bookings);
