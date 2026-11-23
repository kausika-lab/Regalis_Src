using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Offers;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services.Offers;

/// <summary>One offer's part of a purchase.</summary>
public sealed record OfferLine(Offer Offer, decimal Discount);

/// <summary>One priced item (a booking, or a pass). PerOffer[k] is Lines[k]'s share of Discount.</summary>
public sealed record OfferItemPrice(decimal Base, decimal Discount, decimal Taxable, decimal Cgst, decimal Sgst, decimal Total, decimal[] PerOffer);

/// <summary>The priced purchase: what the customer is charged, item by item.</summary>
public sealed class OfferCalculation
{
	public required IReadOnlyList<OfferLine> Lines { get; init; }

	public required IReadOnlyList<OfferItemPrice> Items { get; init; }

	public required decimal CgstPercent { get; init; }

	public required decimal SgstPercent { get; init; }

	public decimal Base => Items.Sum(i => i.Base);

	public decimal Discount => Items.Sum(i => i.Discount);

	public decimal Taxable => Items.Sum(i => i.Taxable);

	public decimal Cgst => Items.Sum(i => i.Cgst);

	public decimal Sgst => Items.Sum(i => i.Sgst);

	public decimal Total => Items.Sum(i => i.Total);
}

/// <summary>Offers priced into one pass / membership purchase. ReservedRowIds: rows written when the
/// online order was created (they get the pass id once it exists).</summary>
public sealed record OfferPassPrice(OfferModule Module, int PassPlanId, OfferCalculation Calc, IReadOnlyList<long> ReservedRowIds)
{
	public decimal Discount => Calc.Discount;

	public decimal Taxable => Calc.Taxable;

	public decimal Cgst => Calc.Cgst;

	public decimal Sgst => Calc.Sgst;

	public decimal Total => Calc.Total;
}

/// <summary>
/// The one place offers are validated and discounts worked out (brief §7, §9): My Bookings, Passes
/// and Membership all come through here, both for the preview and for the money.
///
/// Rules: the discount comes off the price before GST; a percentage is rounded to the paisa and
/// capped at the offer's maximum; a fixed amount never exceeds the price; several offers only when
/// every one of them may be combined, and together they never exceed the price; GST (CGST + SGST)
/// is then charged on what is left, each rounded half away from zero exactly as the booking and
/// pass engines do, so a purchase without an offer costs what it always did.
/// </summary>
public sealed class OfferEngine(RegalisDbContext db, ISettingService settings, IDateTimeProvider clock)
{
	public const string NotEligible = "OFFER_NOT_ELIGIBLE";

	public const string NotAvailable = "OFFER_NOT_AVAILABLE";

	/// <summary>An online pass order counts against the limits for this long while unpaid.</summary>
	public const int OrderHoldMinutes = 30;

	public DateOnly Today => IstClock.IstDateOf(clock.UtcNow);

	public static OfferModule ModuleForPass(PassType type) => type == PassType.DayPass ? OfferModule.Pass : OfferModule.Membership;

	public static OfferModule? ParseModule(string? value) => (value ?? "").Trim().ToLowerInvariant() switch
	{
		"booking" or "bookings" or "my-bookings" or "1" => OfferModule.Booking,
		"pass" or "passes" or "2" => OfferModule.Pass,
		"membership" or "memberships" or "3" => OfferModule.Membership,
		_ => null
	};

	public static string ModuleName(OfferModule m) => m switch
	{
		OfferModule.Booking => "bookings",
		OfferModule.Pass => "passes",
		_ => "memberships"
	};

	/// <summary>Expired (past its end date, switched on or not) | Inactive (switched off) |
	/// Scheduled (on, not started yet) | Active (on and running). Each offer is exactly one.</summary>
	public static string StatusOf(Offer o, DateOnly today) =>
		o.EndDate < today ? "Expired" : !o.IsActive ? "Inactive" : o.StartDate > today ? "Scheduled" : "Active";

	public static string Label(Offer o) => string.IsNullOrEmpty(o.Code) ? o.Title : $"{o.Code} - {o.Title}";

	/// <summary>Active, inside its dates today, and switched on for the module.</summary>
	public IQueryable<Offer> Live(OfferModule module)
	{
		DateOnly today = Today;
		return db.Set<Offer>().Where(o => o.IsActive && o.StartDate <= today && o.EndDate >= today
			&& db.Set<OfferModuleLink>().Any(l => l.OfferId == o.OfferId && l.Module == module));
	}

	// ---- uses ------------------------------------------------------------------------------------

	/// <summary>Redemptions whose purchase went through or is still in progress. Cancelled or expired
	/// bookings, cancelled passes and abandoned online orders give the use back.</summary>
	public IQueryable<OfferRedemption> Counting(IQueryable<OfferRedemption> q, IReadOnlyCollection<long>? excludeBookingIds = null)
	{
		long[] excl = (excludeBookingIds ?? Array.Empty<long>()).ToArray();
		DateTime orderCutoff = clock.UtcNow.AddMinutes(-OrderHoldMinutes);
		return q.Where(r =>
			(r.BookingId != null && !excl.Contains(r.BookingId.Value) && db.Bookings.Any(b => b.BookingId == r.BookingId
				&& (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed || b.Status == BookingStatus.NoShow)))
			|| (r.CustomerPassId != null && db.CustomerPasses.Any(p => p.CustomerPassId == r.CustomerPassId && p.Status != CustomerPassStatus.Cancelled))
			|| (r.BookingId == null && r.CustomerPassId == null && r.PaymentId != null && db.Payments.Any(p => p.PaymentId == r.PaymentId
				&& ((p.Status == PaymentStatus.Pending && p.CreatedDate > orderCutoff) || p.Status == PaymentStatus.Success))));
	}

	/// <summary>Purchases per offer (a multi-day booking is one purchase).</summary>
	public async Task<Dictionary<int, int>> UsesAsync(IReadOnlyCollection<int> offerIds, long? customerId, IReadOnlyCollection<long>? excludeBookingIds, CancellationToken ct)
	{
		if (offerIds.Count == 0)
		{
			return new Dictionary<int, int>();
		}
		int[] ids = offerIds.ToArray();
		IQueryable<OfferRedemption> q = db.Set<OfferRedemption>().AsNoTracking().Where(r => ids.Contains(r.OfferId));
		if (customerId.HasValue)
		{
			long c = customerId.Value;
			q = q.Where(r => r.CustomerId == c);
		}
		var keys = await Counting(q, excludeBookingIds).Select(r => new { r.OfferId, r.PurchaseKey }).Distinct().ToListAsync(ct);
		return keys.GroupBy(k => k.OfferId).ToDictionary(g => g.Key, g => g.Count());
	}

	// ---- eligibility -----------------------------------------------------------------------------

	public sealed record Scope(Dictionary<int, HashSet<OfferModule>> Modules, Dictionary<(int, OfferModule), HashSet<int>> Refs);

	public async Task<Scope> ScopeAsync(IReadOnlyCollection<int> offerIds, CancellationToken ct)
	{
		int[] ids = offerIds.ToArray();
		var mods = await db.Set<OfferModuleLink>().AsNoTracking().Where(l => ids.Contains(l.OfferId)).ToListAsync(ct);
		var refs = await db.Set<OfferScope>().AsNoTracking().Where(s => ids.Contains(s.OfferId)).ToListAsync(ct);
		return new Scope(
			mods.GroupBy(m => m.OfferId).ToDictionary(g => g.Key, g => g.Select(x => x.Module).ToHashSet()),
			refs.GroupBy(s => (s.OfferId, s.Module)).ToDictionary(g => g.Key, g => g.Select(x => x.RefId).ToHashSet()));
	}

	/// <summary>Why this offer cannot be used on this purchase, or null when it can.</summary>
	public string? Reason(Offer o, OfferModule module, int? refId, decimal baseTotal, Scope scope, int usesByCustomer, int usesTotal)
	{
		DateOnly today = Today;
		if (o.IsDeleted || !o.IsActive)
		{
			return "This offer is not available any more.";
		}
		if (o.StartDate > today)
		{
			return $"This offer starts on {o.StartDate:dd MMM yyyy}.";
		}
		if (o.EndDate < today)
		{
			return $"This offer ended on {o.EndDate:dd MMM yyyy}.";
		}
		if (!scope.Modules.TryGetValue(o.OfferId, out HashSet<OfferModule>? mods) || !mods.Contains(module))
		{
			return $"This offer cannot be used on {ModuleName(module)}.";
		}
		if (scope.Refs.TryGetValue((o.OfferId, module), out HashSet<int>? refs) && refs.Count > 0 && (!refId.HasValue || !refs.Contains(refId.Value)))
		{
			return module == OfferModule.Booking ? "This offer does not apply to this type of workspace." : "This offer does not apply to this plan.";
		}
		if (baseTotal <= 0m)
		{
			return "There is nothing to discount on this purchase.";
		}
		if (o.MinAmount is decimal min && min > 0m && baseTotal < min)
		{
			return $"Spend at least ₹{min:N2} (before GST) to use this offer.";
		}
		if (o.UsesPerCustomer is int per && per > 0 && usesByCustomer >= per)
		{
			return per == 1 ? "You have already used this offer." : $"You have already used this offer {per} times, the most allowed.";
		}
		if (o.TotalUses is int all && all > 0 && usesTotal >= all)
		{
			return "This offer has been fully claimed.";
		}
		return null;
	}

	/// <summary>Reasons for a set of offers on one purchase (for the portal list).</summary>
	public async Task<Dictionary<int, string?>> ReasonsAsync(IReadOnlyList<Offer> offers, OfferModule module, int? refId, decimal baseTotal, long customerId, IReadOnlyCollection<long>? excludeBookingIds, CancellationToken ct)
	{
		int[] ids = offers.Select(o => o.OfferId).ToArray();
		Scope scope = await ScopeAsync(ids, ct);
		Dictionary<int, int> mine = await UsesAsync(ids, customerId, excludeBookingIds, ct);
		int[] capped = offers.Where(o => o.TotalUses > 0).Select(o => o.OfferId).ToArray();
		Dictionary<int, int> all = await UsesAsync(capped, null, excludeBookingIds, ct);
		return offers.ToDictionary(o => o.OfferId, o => Reason(o, module, refId, baseTotal, scope, mine.GetValueOrDefault(o.OfferId), all.GetValueOrDefault(o.OfferId)));
	}

	// ---- pricing ---------------------------------------------------------------------------------

	/// <summary>Validates the offers for this purchase and prices it. Throws a 422 with the reason when
	/// any offer cannot be used. <paramref name="bases" /> is the price before GST of each item.</summary>
	public async Task<OfferCalculation> CalculateAsync(OfferModule module, int? refId, IReadOnlyList<decimal> bases, IReadOnlyList<int> offerIds, long customerId, IReadOnlyCollection<long>? excludeBookingIds, CancellationToken ct)
	{
		if (offerIds.Count != offerIds.Distinct().Count())
		{
			throw new BusinessRuleException("The same offer cannot be applied twice.", NotEligible);
		}
		if (offerIds.Count > 10)
		{
			throw new BusinessRuleException("Too many offers on one purchase.", NotEligible);
		}
		int[] ids = offerIds.ToArray();
		List<Offer> offers = await db.Set<Offer>().AsNoTracking().Where(o => ids.Contains(o.OfferId)).ToListAsync(ct);
		if (offers.Count != ids.Length)
		{
			throw new BusinessRuleException("This offer is not available any more.", NotAvailable);
		}
		// Keep the customer's order so the result lines match what they picked.
		offers = ids.Select(id => offers.First(o => o.OfferId == id)).ToList();
		if (offers.Count > 1)
		{
			Offer? solo = offers.FirstOrDefault(o => !o.CanCombine);
			if (solo != null)
			{
				throw new BusinessRuleException($"\"{solo.Title}\" cannot be combined with other offers. Remove one of them.", NotEligible);
			}
		}
		decimal baseTotal = bases.Sum();
		Dictionary<int, string?> reasons = await ReasonsAsync(offers, module, refId, baseTotal, customerId, excludeBookingIds, ct);
		foreach (Offer o in offers)
		{
			if (reasons[o.OfferId] is string why)
			{
				throw new BusinessRuleException($"{o.Title}: {why}", NotEligible);
			}
		}
		(decimal cgstPercent, decimal sgstPercent) = await TaxPercentsAsync(ct);
		return Price(offers, bases, cgstPercent, sgstPercent);
	}

	public async Task<(decimal Cgst, decimal Sgst)> TaxPercentsAsync(CancellationToken ct) =>
		(await settings.GetDecimalAsync("Tax.CgstPercent", 9m, ct), await settings.GetDecimalAsync("Tax.SgstPercent", 9m, ct));

	/// <summary>The arithmetic only (no checks). Public for the unit tests.</summary>
	public static OfferCalculation Price(IReadOnlyList<Offer> offers, IReadOnlyList<decimal> bases, decimal cgstPercent, decimal sgstPercent)
	{
		decimal[] b = bases.Select(R).ToArray();
		decimal baseTotal = b.Sum();
		// Each offer on the whole purchase; together never more than the price.
		decimal[] perOffer = new decimal[offers.Count];
		decimal left = baseTotal;
		for (int k = 0; k < offers.Count; k++)
		{
			Offer o = offers[k];
			decimal d = o.DiscountType == OfferDiscountType.Percentage ? R(baseTotal * o.DiscountValue / 100m) : R(o.DiscountValue);
			if (o.MaxDiscountAmount is decimal cap && cap > 0m && d > cap)
			{
				d = R(cap);
			}
			d = Math.Max(0m, Math.Min(d, left));
			perOffer[k] = d;
			left -= d;
		}
		decimal total = perOffer.Sum();
		// Spread the discount over the items in proportion to their price; the last takes the rounding.
		decimal[] itemDisc = new decimal[b.Length];
		decimal given = 0m;
		for (int i = 0; i < b.Length; i++)
		{
			decimal share = i == b.Length - 1 ? total - given : (baseTotal == 0m ? 0m : R(total * b[i] / baseTotal));
			itemDisc[i] = Math.Max(0m, Math.Min(share, b[i]));
			given += itemDisc[i];
		}
		// Anything a clamp left over goes to items with room (only possible at 100% off).
		decimal rest = total - given;
		for (int i = 0; i < b.Length && rest > 0m; i++)
		{
			decimal room = b[i] - itemDisc[i];
			decimal add = Math.Min(room, rest);
			itemDisc[i] += add;
			rest -= add;
		}
		var items = new List<OfferItemPrice>(b.Length);
		for (int i = 0; i < b.Length; i++)
		{
			decimal[] split = new decimal[offers.Count];
			decimal done = 0m;
			for (int k = 0; k < offers.Count; k++)
			{
				split[k] = k == offers.Count - 1 ? itemDisc[i] - done : (total == 0m ? 0m : R(itemDisc[i] * perOffer[k] / total));
				done += split[k];
			}
			decimal taxable = b[i] - itemDisc[i];
			decimal cgst = R(taxable * cgstPercent / 100m);
			decimal sgst = R(taxable * sgstPercent / 100m);
			items.Add(new OfferItemPrice(b[i], itemDisc[i], taxable, cgst, sgst, taxable + cgst + sgst, split));
		}
		return new OfferCalculation
		{
			Lines = offers.Select((o, k) => new OfferLine(o, perOffer[k])).ToList(),
			Items = items,
			CgstPercent = cgstPercent,
			SgstPercent = sgstPercent
		};
	}

	/// <summary>The same purchase with no offer (what it cost before this kit).</summary>
	public static OfferCalculation NoOffer(IReadOnlyList<decimal> bases, decimal cgstPercent, decimal sgstPercent) =>
		Price(Array.Empty<Offer>(), bases, cgstPercent, sgstPercent);

	private static decimal R(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);

	// ---- passes and memberships (called from PassService) ------------------------------------------

	/// <summary>The customer's chosen offers priced into a pass / membership; null when none.</summary>
	public async Task<OfferPassPrice?> PriceForPassAsync(long customerId, PassPlan plan, decimal taxable, IReadOnlyList<int>? offerIds, CancellationToken ct)
	{
		if (offerIds == null || offerIds.Count == 0)
		{
			return null;
		}
		OfferModule module = ModuleForPass(plan.PassType);
		OfferCalculation calc = await CalculateAsync(module, plan.PassPlanId, new[] { taxable }, offerIds, customerId, null, ct);
		return new OfferPassPrice(module, plan.PassPlanId, calc, Array.Empty<long>());
	}

	/// <summary>The offers that were priced into this online order when it was created. The money was
	/// taken at that price, so it is honoured even if an offer ended in between.</summary>
	public async Task<OfferPassPrice?> ReservedForOrderAsync(long customerId, PassPlan plan, decimal taxable, string? gatewayOrderId, CancellationToken ct)
	{
		if (string.IsNullOrEmpty(gatewayOrderId))
		{
			return null;
		}
		long? paymentId = await db.Payments.AsNoTracking().Where(p => p.GatewayOrderId == gatewayOrderId && p.CustomerId == customerId).Select(p => (long?)p.PaymentId).FirstOrDefaultAsync(ct);
		if (!paymentId.HasValue)
		{
			return null;
		}
		List<OfferRedemption> rows = await db.Set<OfferRedemption>().AsNoTracking().Where(r => r.PaymentId == paymentId && r.CustomerPassId == null && r.BookingId == null).OrderBy(r => r.RedemptionId).ToListAsync(ct);
		if (rows.Count == 0)
		{
			return null;
		}
		if (rows.Any(r => r.PassPlanId != plan.PassPlanId))
		{
			throw new BusinessRuleException("This payment was made for a different plan.", "PAYMENT_VERIFICATION_FAILED");
		}
		int[] ids = rows.Select(r => r.OfferId).ToArray();
		List<Offer> offers = await db.Set<Offer>().IgnoreQueryFilters().AsNoTracking().Where(o => ids.Contains(o.OfferId)).ToListAsync(ct);
		// Price with the amounts fixed at order time, as fixed-amount offers.
		List<Offer> fixedOffers = rows.Select(r => new Offer
		{
			OfferId = r.OfferId,
			Title = offers.FirstOrDefault(o => o.OfferId == r.OfferId)?.Title ?? r.OfferLabel,
			Code = offers.FirstOrDefault(o => o.OfferId == r.OfferId)?.Code,
			DiscountType = OfferDiscountType.Fixed,
			DiscountValue = r.DiscountAmount,
			CanCombine = true
		}).ToList();
		(decimal cg, decimal sg) = await TaxPercentsAsync(ct);
		OfferCalculation calc = Price(fixedOffers, new[] { taxable }, cg, sg);
		return new OfferPassPrice(ModuleForPass(plan.PassType), plan.PassPlanId, calc, rows.Select(r => r.RedemptionId).ToList());
	}

	/// <summary>Writes the order-time rows (online payment created, pass not yet bought).</summary>
	public void ReserveForOrder(OfferPassPrice price, long customerId, long paymentId)
	{
		Guid key = Guid.NewGuid();
		DateTime now = clock.UtcNow;
		for (int k = 0; k < price.Calc.Lines.Count; k++)
		{
			OfferLine line = price.Calc.Lines[k];
			db.Set<OfferRedemption>().Add(new OfferRedemption
			{
				OfferId = line.Offer.OfferId,
				CustomerId = customerId,
				Module = price.Module,
				PaymentId = paymentId,
				PassPlanId = price.PassPlanId,
				PurchaseKey = key,
				BaseAmount = price.Calc.Base,
				DiscountAmount = price.Calc.Items[0].PerOffer[k],
				OfferLabel = Trim(Label(line.Offer)),
				CreatedUtc = now
			});
		}
	}

	/// <summary>Ties the offers to the pass just bought (inside PassService's transaction).</summary>
	public async Task RecordPassAsync(OfferPassPrice price, long customerId, long customerPassId, long? paymentId, CancellationToken ct)
	{
		if (price.ReservedRowIds.Count > 0)
		{
			long[] ids = price.ReservedRowIds.ToArray();
			foreach (OfferRedemption r in await db.Set<OfferRedemption>().Where(r => ids.Contains(r.RedemptionId)).ToListAsync(ct))
			{
				r.CustomerPassId = customerPassId;
			}
			return;
		}
		Guid key = Guid.NewGuid();
		DateTime now = clock.UtcNow;
		for (int k = 0; k < price.Calc.Lines.Count; k++)
		{
			OfferLine line = price.Calc.Lines[k];
			db.Set<OfferRedemption>().Add(new OfferRedemption
			{
				OfferId = line.Offer.OfferId,
				CustomerId = customerId,
				Module = price.Module,
				CustomerPassId = customerPassId,
				PaymentId = paymentId,
				PassPlanId = price.PassPlanId,
				PurchaseKey = key,
				BaseAmount = price.Calc.Base,
				DiscountAmount = price.Calc.Items[0].PerOffer[k],
				OfferLabel = Trim(Label(line.Offer)),
				CreatedUtc = now
			});
		}
	}

	public static string Trim(string s) => s.Length > 160 ? s.Substring(0, 160) : s;

	// ---- bookings (called from BookingService / PaymentService) ------------------------------------

	/// <summary>"; offer CODE - Title" for the booking's invoice line, or "" when no offer is on it.</summary>
	public static async Task<string> InvoiceNoteAsync(RegalisDbContext db, long bookingId, CancellationToken ct)
	{
		List<string> labels = await db.Set<OfferRedemption>().AsNoTracking().Where(r => r.BookingId == bookingId).OrderBy(r => r.RedemptionId).Select(r => r.OfferLabel).ToListAsync(ct);
		return labels.Count == 0 ? "" : "; offer " + string.Join(", ", labels.Distinct());
	}

	/// <summary>Server-side re-check just before money moves for a held booking: every offer applied to
	/// it must still be active and inside its dates. Throws 422 OFFER_NOT_AVAILABLE when one ended.</summary>
	public static async Task EnsureBookingOffersValidAsync(RegalisDbContext db, DateTime utcNow, long bookingId, CancellationToken ct)
	{
		var rows = await db.Set<OfferRedemption>().AsNoTracking().Where(r => r.BookingId == bookingId).Select(r => new { r.OfferId, r.OfferLabel }).ToListAsync(ct);
		if (rows.Count == 0)
		{
			return;
		}
		int[] ids = rows.Select(r => r.OfferId).ToArray();
		DateOnly today = IstClock.IstDateOf(utcNow);
		List<Offer> offers = await db.Set<Offer>().IgnoreQueryFilters().AsNoTracking().Where(o => ids.Contains(o.OfferId)).ToListAsync(ct);
		foreach (var r in rows)
		{
			Offer? o = offers.FirstOrDefault(x => x.OfferId == r.OfferId);
			if (o == null || o.IsDeleted || !o.IsActive || o.StartDate > today || o.EndDate < today)
			{
				throw new BusinessRuleException($"The offer \"{r.OfferLabel}\" is no longer available, so this discounted price cannot be charged. Please start the booking again.", NotAvailable);
			}
		}
	}
}
