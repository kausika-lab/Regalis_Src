using System;
using Regalis.Domain.Common;
using Regalis.Domain.Entities.Offers;

namespace Regalis.Domain.Entities.Coupons;

// Coupon codes (coupons kit, 25 Nov 2026). Tables are created by db/migrations/20261125120000_Coupons.sql.
// Kept apart from offers (product-owner decision, 25 Nov 2026): own tables, own admin tab, own
// redemptions. The modules (OfferModule) and discount types (OfferDiscountType) are the offers kit's,
// so a coupon is priced by exactly the same arithmetic. No navigation properties: the services join by id.

/// <summary>MST_Coupon — a code the customer types at checkout. Coupons are never listed to customers:
/// only a customer who types a matching code gets the discount. IsActive (from AuditableEntity) is the
/// Active / Inactive switch; "Expired" and "Scheduled" are worked out from the dates when read.
/// Audited and soft-deleted like every master table.</summary>
public class Coupon : AuditableEntity
{
	public int CouponId { get; set; }

	/// <summary>Stored upper case. Unique among coupons that are not deleted.</summary>
	public string Code { get; set; } = string.Empty;

	/// <summary>Optional short text the customer sees next to the code.</summary>
	public string? Description { get; set; }

	public OfferDiscountType DiscountType { get; set; }

	/// <summary>Percent (0-100] or rupees, off the price before GST.</summary>
	public decimal DiscountValue { get; set; }

	/// <summary>Cap on the discount in rupees (percentage coupons).</summary>
	public decimal? MaxDiscountAmount { get; set; }

	/// <summary>Smallest purchase (price before GST) the coupon applies to.</summary>
	public decimal? MinAmount { get; set; }

	/// <summary>First day the coupon can be used (IST, inclusive).</summary>
	public DateOnly ValidFrom { get; set; }

	/// <summary>Last day the coupon can be used (IST, inclusive).</summary>
	public DateOnly ValidUntil { get; set; }

	/// <summary>Optional: how many purchases in total, by everyone.</summary>
	public int? TotalUses { get; set; }

	/// <summary>Optional: how many purchases one customer may use it on.</summary>
	public int? UsesPerCustomer { get; set; }
}

/// <summary>MST_CouponModule — where a coupon can be used: My Bookings, Passes, Membership.</summary>
public class CouponModuleLink
{
	public int CouponModuleId { get; set; }

	public int CouponId { get; set; }

	public OfferModule Module { get; set; }
}

/// <summary>TRN_CouponRedemption — a coupon on one booking or one pass purchase. Same life cycle as
/// TRN_OfferRedemption: whether it counts as used is read from the booking / pass / payment it
/// points to, so a cancelled booking or an abandoned order gives the use back without any job.</summary>
public class CouponRedemption
{
	public long RedemptionId { get; set; }

	public int CouponId { get; set; }

	public long CustomerId { get; set; }

	public OfferModule Module { get; set; }

	public long? BookingId { get; set; }

	public long? CustomerPassId { get; set; }

	/// <summary>The pending online order a pass discount was priced into.</summary>
	public long? PaymentId { get; set; }

	public int? PassPlanId { get; set; }

	/// <summary>One purchase: every day of a multi-day booking shares it, so a use is counted once.</summary>
	public Guid PurchaseKey { get; set; }

	/// <summary>Price before GST and before the coupon.</summary>
	public decimal BaseAmount { get; set; }

	public decimal DiscountAmount { get; set; }

	/// <summary>The code at the time, for the history and the invoice.</summary>
	public string CouponCode { get; set; } = string.Empty;

	public DateTime CreatedUtc { get; set; }
}
