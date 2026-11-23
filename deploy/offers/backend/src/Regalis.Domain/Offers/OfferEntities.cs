using System;
using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Offers;

// Offers (offers kit, 23 Nov 2026). Tables are created by db/migrations/20261123120000_Offers.sql.
// No navigation properties: the services join by id, like the CRM and Wi-Fi kits.

/// <summary>Where an offer can be used in the customer portal.</summary>
public enum OfferModule : byte
{
	/// <summary>My Bookings: the customer's own hourly and monthly workspace bookings.</summary>
	Booking = 1,
	/// <summary>Passes: day-pass plans (PassType.DayPass).</summary>
	Pass = 2,
	/// <summary>Membership: monthly plans (PassType.MonthlyPass, Virtual Office included).</summary>
	Membership = 3
}

public enum OfferDiscountType : byte
{
	Percentage = 1,
	Fixed = 2
}

/// <summary>MST_Offer — a promotion the admin configures. IsActive (from AuditableEntity) is the
/// Active / Inactive switch; "Expired" and "Scheduled" are worked out from the dates when read.
/// Audited and soft-deleted like every master table.</summary>
public class Offer : AuditableEntity
{
	public int OfferId { get; set; }

	/// <summary>Internal name, shown to the admin only.</summary>
	public string OfferName { get; set; } = string.Empty;

	/// <summary>Headline the customer sees.</summary>
	public string Title { get; set; } = string.Empty;

	public string? Description { get; set; }

	public string? Terms { get; set; }

	/// <summary>Optional code, stored upper case. Unique among offers that are not deleted.</summary>
	public string? Code { get; set; }

	/// <summary>False = not listed in the portal; it works only when the customer types the code.</summary>
	public bool ShowInPortal { get; set; } = true;

	public OfferDiscountType DiscountType { get; set; }

	/// <summary>Percent (0-100] or rupees, off the price before GST.</summary>
	public decimal DiscountValue { get; set; }

	/// <summary>Cap on the discount in rupees (percentage offers).</summary>
	public decimal? MaxDiscountAmount { get; set; }

	/// <summary>Smallest purchase (price before GST) the offer applies to.</summary>
	public decimal? MinAmount { get; set; }

	/// <summary>First day the offer can be used (IST, inclusive).</summary>
	public DateOnly StartDate { get; set; }

	/// <summary>Last day the offer can be used (IST, inclusive).</summary>
	public DateOnly EndDate { get; set; }

	/// <summary>May be used together with other offers that also allow it.</summary>
	public bool CanCombine { get; set; }

	/// <summary>Display priority: lower first.</summary>
	public int SortOrder { get; set; }

	/// <summary>Optional: how many purchases one customer may use it on.</summary>
	public int? UsesPerCustomer { get; set; }

	/// <summary>Optional: how many purchases in total.</summary>
	public int? TotalUses { get; set; }

	/// <summary>Bumped on every image change, so the portal fetches the new picture.</summary>
	public int ImageVersion { get; set; }

	public bool HasImage { get; set; }
}

/// <summary>MST_OfferModule — the customer modules an offer appears in and applies to.</summary>
public class OfferModuleLink
{
	public int OfferModuleId { get; set; }

	public int OfferId { get; set; }

	public OfferModule Module { get; set; }
}

/// <summary>MST_OfferScope — optional narrowing inside a module: specific pass / membership
/// plans (PassPlanId) or workspace types for bookings (WorkspaceTypeId). No rows for a module
/// = every plan or workspace type in it.</summary>
public class OfferScope
{
	public int OfferScopeId { get; set; }

	public int OfferId { get; set; }

	public OfferModule Module { get; set; }

	/// <summary>PassPlanId for Pass / Membership, WorkspaceTypeId for Booking.</summary>
	public int RefId { get; set; }
}

/// <summary>TRN_OfferImage — the optional banner picture, kept in the database so no file
/// storage has to be configured.</summary>
public class OfferImage
{
	public int OfferId { get; set; }

	public string ContentType { get; set; } = string.Empty;

	public byte[] Data { get; set; } = Array.Empty<byte>();

	public DateTime UpdatedUtc { get; set; }
}

/// <summary>TRN_OfferRedemption — one offer applied to one booking or one pass purchase.
/// A booking row is written when the offer is applied to the customer's held booking; a pass row
/// when the online order is created (PaymentId) or the pass is bought (CustomerPassId).
/// Whether it counts as used is read from the booking / pass / payment it points to, so a
/// cancelled booking or an abandoned order gives the use back without any job.</summary>
public class OfferRedemption
{
	public long RedemptionId { get; set; }

	public int OfferId { get; set; }

	public long CustomerId { get; set; }

	public OfferModule Module { get; set; }

	public long? BookingId { get; set; }

	public long? CustomerPassId { get; set; }

	/// <summary>The pending online order a pass discount was priced into.</summary>
	public long? PaymentId { get; set; }

	public int? PassPlanId { get; set; }

	/// <summary>One purchase: every day of a multi-day booking shares it, so a use is counted once.</summary>
	public Guid PurchaseKey { get; set; }

	/// <summary>Price before GST and before any offer.</summary>
	public decimal BaseAmount { get; set; }

	/// <summary>This offer's share of the discount.</summary>
	public decimal DiscountAmount { get; set; }

	/// <summary>Offer code / title at the time, for the history.</summary>
	public string OfferLabel { get; set; } = string.Empty;

	public DateTime CreatedUtc { get; set; }
}
