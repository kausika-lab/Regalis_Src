using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Coupons;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Offers;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services.Offers;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services.Coupons;

/// <summary>
/// The one place coupon codes are checked and priced (coupons kit, 25 Nov 2026). Bookings, passes
/// and memberships, the preview and the charge all come through here.
///
/// A coupon replaces any offer on the purchase (product-owner decision): it is never combined with
/// one. The discount is worked out by OfferEngine.Price, the same arithmetic offers use: off the price
/// before GST, a percentage rounded to the paisa and capped at the maximum, a fixed amount never more
/// than the price, then CGST + SGST on what is left. So a purchase without a coupon costs exactly
/// what it did before this kit.
///
/// Inside OfferEngine's price objects a coupon is one line whose Offer is not saved anywhere and has
/// OfferId = -CouponId, so the offers code in PassService can carry it unchanged; everything that
/// writes or labels the line comes here first.
/// </summary>
public sealed class CouponEngine(RegalisDbContext db, OfferEngine offers, IDateTimeProvider clock)
{
	public const string Invalid = "COUPON_INVALID";
	public const string Expired = "COUPON_EXPIRED";
	public const string Inactive = "COUPON_INACTIVE";
	public const string NotStarted = "COUPON_NOT_STARTED";
	public const string NotApplicable = "COUPON_NOT_APPLICABLE";
	public const string MinAmount = "COUPON_MIN_AMOUNT";
	public const string AlreadyUsed = "COUPON_ALREADY_USED";
	public const string LimitReached = "COUPON_LIMIT_REACHED";
	public const string NotAvailable = "COUPON_NOT_AVAILABLE";

	public DateOnly Today => IstClock.IstDateOf(clock.UtcNow);

	public static string Normalize(string? code) => (code ?? "").Trim().ToUpperInvariant();

	/// <summary>Expired (past its last day, switched on or not) | Inactive | Scheduled | Active.</summary>
	public static string StatusOf(Coupon c, DateOnly today) =>
		c.ValidUntil < today ? "Expired" : !c.IsActive ? "Inactive" : c.ValidFrom > today ? "Scheduled" : "Active";

	public async Task<Dictionary<int, HashSet<OfferModule>>> ModulesAsync(IReadOnlyCollection<int> couponIds, CancellationToken ct)
	{
		int[] ids = couponIds.ToArray();
		var rows = await db.Set<CouponModuleLink>().AsNoTracking().Where(l => ids.Contains(l.CouponId)).ToListAsync(ct);
		return rows.GroupBy(r => r.CouponId).ToDictionary(g => g.Key, g => g.Select(x => x.Module).ToHashSet());
	}

	// ---- uses ------------------------------------------------------------------------------------

	/// <summary>Redemptions whose purchase went through or is still in progress, exactly as for offers.
	/// A booking row also stops counting once an offer was put on that booking instead.</summary>
	public IQueryable<CouponRedemption> Counting(IQueryable<CouponRedemption> q, IReadOnlyCollection<long>? excludeBookingIds = null)
	{
		long[] excl = (excludeBookingIds ?? Array.Empty<long>()).ToArray();
		DateTime orderCutoff = clock.UtcNow.AddMinutes(-OfferEngine.OrderHoldMinutes);
		return q.Where(r =>
			(r.BookingId != null && !excl.Contains(r.BookingId.Value)
				&& !db.Set<OfferRedemption>().Any(o => o.BookingId == r.BookingId)
				&& db.Bookings.Any(b => b.BookingId == r.BookingId
					&& (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn || b.Status == BookingStatus.Completed || b.Status == BookingStatus.NoShow)))
			|| (r.CustomerPassId != null && db.CustomerPasses.Any(p => p.CustomerPassId == r.CustomerPassId && p.Status != CustomerPassStatus.Cancelled))
			|| (r.BookingId == null && r.CustomerPassId == null && r.PaymentId != null && db.Payments.Any(p => p.PaymentId == r.PaymentId
				&& ((p.Status == PaymentStatus.Pending && p.CreatedDate > orderCutoff) || p.Status == PaymentStatus.Success))));
	}

	/// <summary>Purchases per coupon (a multi-day booking is one purchase).</summary>
	public async Task<Dictionary<int, int>> UsesAsync(IReadOnlyCollection<int> couponIds, long? customerId, IReadOnlyCollection<long>? excludeBookingIds, CancellationToken ct)
	{
		if (couponIds.Count == 0)
		{
			return new Dictionary<int, int>();
		}
		int[] ids = couponIds.ToArray();
		IQueryable<CouponRedemption> q = db.Set<CouponRedemption>().AsNoTracking().Where(r => ids.Contains(r.CouponId));
		if (customerId.HasValue)
		{
			long c = customerId.Value;
			q = q.Where(r => r.CustomerId == c);
		}
		var keys = await Counting(q, excludeBookingIds).Select(r => new { r.CouponId, r.PurchaseKey }).Distinct().ToListAsync(ct);
		return keys.GroupBy(k => k.CouponId).ToDictionary(g => g.Key, g => g.Count());
	}

	// ---- checking ----------------------------------------------------------------------------------

	public sealed record Failure(string Message, string Code);

	/// <summary>Why this coupon cannot be used on this purchase, or null when it can. The order is the
	/// one a customer finds most useful: does the code exist, is it in date, is it switched on, does it
	/// fit this purchase, has it been used up.</summary>
	public Failure? Check(Coupon? c, string code, OfferModule module, decimal baseTotal, HashSet<OfferModule>? modules, int usesByCustomer, int usesTotal)
	{
		DateOnly today = Today;
		if (c == null || c.IsDeleted)
		{
			return new Failure($"The coupon code {code} is not valid. Check the code and try again.", Invalid);
		}
		if (c.ValidUntil < today)
		{
			return new Failure($"The coupon {c.Code} expired on {c.ValidUntil:dd MMM yyyy}.", Expired);
		}
		if (!c.IsActive)
		{
			return new Failure($"The coupon {c.Code} is not active.", Inactive);
		}
		if (c.ValidFrom > today)
		{
			return new Failure($"The coupon {c.Code} can be used from {c.ValidFrom:dd MMM yyyy}.", NotStarted);
		}
		if (modules == null || !modules.Contains(module))
		{
			return new Failure($"The coupon {c.Code} cannot be used on {OfferEngine.ModuleName(module)}.", NotApplicable);
		}
		if (baseTotal <= 0m)
		{
			return new Failure("There is nothing to discount on this purchase.", NotApplicable);
		}
		if (c.MinAmount is decimal min && min > 0m && baseTotal < min)
		{
			return new Failure($"Spend at least ₹{min:N2} (before GST) to use the coupon {c.Code}.", MinAmount);
		}
		if (c.UsesPerCustomer is int per && per > 0 && usesByCustomer >= per)
		{
			return new Failure(per == 1 ? $"You have already used the coupon {c.Code}." : $"You have already used the coupon {c.Code} {per} times, the most allowed.", AlreadyUsed);
		}
		if (c.TotalUses is int all && all > 0 && usesTotal >= all)
		{
			return new Failure($"The coupon {c.Code} has reached its usage limit.", LimitReached);
		}
		return null;
	}

	/// <summary>The coupon with this code (not deleted), or null.</summary>
	public Task<Coupon?> FindAsync(string code, CancellationToken ct) =>
		db.Set<Coupon>().AsNoTracking().FirstOrDefaultAsync(c => c.Code == code, ct);

	/// <summary>Checks the code for this purchase; null + the failure when it cannot be used.</summary>
	public async Task<(Coupon? Coupon, Failure? Failure)> EvaluateAsync(string code, OfferModule module, decimal baseTotal, long customerId, IReadOnlyCollection<long>? excludeBookingIds, CancellationToken ct)
	{
		Coupon? c = code.Length == 0 ? null : await FindAsync(code, ct);
		if (c == null)
		{
			return (null, Check(null, code, module, baseTotal, null, 0, 0));
		}
		Dictionary<int, HashSet<OfferModule>> mods = await ModulesAsync(new[] { c.CouponId }, ct);
		Dictionary<int, int> mine = await UsesAsync(new[] { c.CouponId }, customerId, excludeBookingIds, ct);
		Dictionary<int, int> all = c.TotalUses > 0 ? await UsesAsync(new[] { c.CouponId }, null, excludeBookingIds, ct) : new Dictionary<int, int>();
		Failure? f = Check(c, code, module, baseTotal, mods.GetValueOrDefault(c.CouponId), mine.GetValueOrDefault(c.CouponId), all.GetValueOrDefault(c.CouponId));
		return f == null ? (c, null) : (null, f);
	}

	/// <summary>Checks and prices the coupon; throws 422 with the reason (and its COUPON_* code).</summary>
	public async Task<(Coupon Coupon, OfferCalculation Calc)> CalculateAsync(string code, OfferModule module, IReadOnlyList<decimal> bases, long customerId, IReadOnlyCollection<long>? excludeBookingIds, CancellationToken ct)
	{
		(Coupon? c, Failure? f) = await EvaluateAsync(code, module, bases.Sum(), customerId, excludeBookingIds, ct);
		if (c == null)
		{
			throw new BusinessRuleException(f!.Message, f.Code);
		}
		(decimal cg, decimal sg) = await offers.TaxPercentsAsync(ct);
		return (c, Price(c, bases, cg, sg));
	}

	/// <summary>The coupon as an (unsaved) offer, so OfferEngine.Price does the arithmetic.</summary>
	public static Offer AsOffer(Coupon c) => new()
	{
		OfferId = -c.CouponId,
		OfferName = c.Code,
		Title = "Coupon " + c.Code,
		Code = c.Code,
		DiscountType = c.DiscountType,
		DiscountValue = c.DiscountValue,
		MaxDiscountAmount = c.MaxDiscountAmount,
		CanCombine = false
	};

	public static OfferCalculation Price(Coupon c, IReadOnlyList<decimal> bases, decimal cgstPercent, decimal sgstPercent) =>
		OfferEngine.Price(new[] { AsOffer(c) }, bases, cgstPercent, sgstPercent);

	public static bool IsCoupon(OfferPassPrice? price) => price != null && price.Calc.Lines.Count == 1 && price.Calc.Lines[0].Offer.OfferId < 0;

	private static int CouponIdOf(OfferPassPrice price) => -price.Calc.Lines[0].Offer.OfferId;

	/// <summary>Serialises everything that counts or takes a use of one coupon, so two customers cannot
	/// both take its last use. Held until the surrounding transaction ends.</summary>
	private async Task LockAsync(int couponId, CancellationToken ct)
	{
		await db.Database.ExecuteSqlRawAsync(
			"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {0}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000; IF @r < 0 THROW 51000, 'The coupon is busy. Please try again.', 1;",
			new object[] { "RG_COUPON_" + couponId }, ct);
	}

	/// <summary>Inside the caller's transaction: takes the coupon's lock and checks its limits again,
	/// counting what other customers committed meanwhile.</summary>
	public async Task RecheckLimitsLockedAsync(int couponId, long customerId, IReadOnlyCollection<long>? excludeBookingIds, CancellationToken ct)
	{
		await LockAsync(couponId, ct);
		Coupon c = await db.Set<Coupon>().AsNoTracking().FirstOrDefaultAsync(x => x.CouponId == couponId, ct)
			?? throw new BusinessRuleException("This coupon is no longer available. Please start again.", NotAvailable);
		if (c.UsesPerCustomer is int per && per > 0 && (await UsesAsync(new[] { couponId }, customerId, excludeBookingIds, ct)).GetValueOrDefault(couponId) >= per)
		{
			throw new BusinessRuleException(per == 1 ? $"You have already used the coupon {c.Code}." : $"You have already used the coupon {c.Code} {per} times, the most allowed.", AlreadyUsed);
		}
		if (c.TotalUses is int all && all > 0 && (await UsesAsync(new[] { couponId }, null, excludeBookingIds, ct)).GetValueOrDefault(couponId) >= all)
		{
			throw new BusinessRuleException($"The coupon {c.Code} has reached its usage limit.", LimitReached);
		}
	}

	// ---- passes and memberships (called from PassService) ------------------------------------------

	/// <summary>The price of a pass / membership with the customer's coupon or, when no coupon is given,
	/// with their chosen offers (OfferEngine, unchanged). A coupon replaces offers. Null = neither.</summary>
	public async Task<OfferPassPrice?> PriceForPassAsync(long customerId, PassPlan plan, decimal taxable, string? couponCode, IReadOnlyList<int>? offerIds, CancellationToken ct)
	{
		string code = Normalize(couponCode);
		if (code.Length == 0)
		{
			return await offers.PriceForPassAsync(customerId, plan, taxable, offerIds, ct);
		}
		OfferModule module = OfferEngine.ModuleForPass(plan.PassType);
		(Coupon _, OfferCalculation calc) = await CalculateAsync(code, module, new[] { taxable }, customerId, null, ct);
		return new OfferPassPrice(module, plan.PassPlanId, calc, Array.Empty<long>());
	}

	/// <summary>The coupon or offers priced into this online order when it was created. The money was
	/// taken at that price, so it is honoured even if the coupon ended in between.</summary>
	public async Task<OfferPassPrice?> ReservedForOrderAsync(long customerId, PassPlan plan, decimal taxable, string? gatewayOrderId, CancellationToken ct)
	{
		if (!string.IsNullOrEmpty(gatewayOrderId))
		{
			long? paymentId = await db.Payments.AsNoTracking().Where(p => p.GatewayOrderId == gatewayOrderId && p.CustomerId == customerId).Select(p => (long?)p.PaymentId).FirstOrDefaultAsync(ct);
			if (paymentId.HasValue)
			{
				CouponRedemption? row = await db.Set<CouponRedemption>().AsNoTracking().Where(r => r.PaymentId == paymentId && r.CustomerPassId == null && r.BookingId == null).OrderBy(r => r.RedemptionId).FirstOrDefaultAsync(ct);
				if (row != null)
				{
					if (row.PassPlanId != plan.PassPlanId)
					{
						throw new BusinessRuleException("This payment was made for a different plan.", "PAYMENT_VERIFICATION_FAILED");
					}
					// The amount fixed at order time, as a fixed-amount coupon.
					var fixedCoupon = new Coupon { CouponId = row.CouponId, Code = row.CouponCode, DiscountType = OfferDiscountType.Fixed, DiscountValue = row.DiscountAmount };
					(decimal cg, decimal sg) = await offers.TaxPercentsAsync(ct);
					return new OfferPassPrice(OfferEngine.ModuleForPass(plan.PassType), plan.PassPlanId, Price(fixedCoupon, new[] { taxable }, cg, sg), new[] { row.RedemptionId });
				}
			}
		}
		return await offers.ReservedForOrderAsync(customerId, plan, taxable, gatewayOrderId, ct);
	}

	/// <summary>Order-time rows (online payment created, pass not yet bought). A coupon is written and
	/// committed here under its lock; offers are added for the caller's SaveChanges, as before.</summary>
	public async Task ReserveForOrderAsync(OfferPassPrice price, long customerId, long paymentId, CancellationToken ct)
	{
		if (!IsCoupon(price))
		{
			offers.ReserveForOrder(price, customerId, paymentId);
			return;
		}
		int couponId = CouponIdOf(price);
		await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
		{
			await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(ct);
			await RecheckLimitsLockedAsync(couponId, customerId, null, ct);
			db.Set<CouponRedemption>().Add(Row(price, customerId, null, paymentId));
			await db.SaveChangesAsync(ct);
			await tx.CommitAsync(ct);
		});
	}

	/// <summary>Ties the coupon or offers to the pass just bought (inside PassService's transaction).</summary>
	public async Task RecordPassAsync(OfferPassPrice price, long customerId, long customerPassId, long? paymentId, CancellationToken ct)
	{
		if (!IsCoupon(price))
		{
			await offers.RecordPassAsync(price, customerId, customerPassId, paymentId, ct);
			return;
		}
		if (price.ReservedRowIds.Count > 0)
		{
			long[] ids = price.ReservedRowIds.ToArray();
			foreach (CouponRedemption r in await db.Set<CouponRedemption>().Where(r => ids.Contains(r.RedemptionId)).ToListAsync(ct))
			{
				r.CustomerPassId = customerPassId;
			}
			return;
		}
		await RecheckLimitsLockedAsync(CouponIdOf(price), customerId, null, ct);
		db.Set<CouponRedemption>().Add(Row(price, customerId, customerPassId, paymentId));
	}

	private CouponRedemption Row(OfferPassPrice price, long customerId, long? customerPassId, long? paymentId) => new()
	{
		CouponId = CouponIdOf(price),
		CustomerId = customerId,
		Module = price.Module,
		CustomerPassId = customerPassId,
		PaymentId = paymentId,
		PassPlanId = price.PassPlanId,
		PurchaseKey = Guid.NewGuid(),
		BaseAmount = price.Calc.Base,
		DiscountAmount = price.Calc.Discount,
		CouponCode = price.Calc.Lines[0].Offer.Code ?? "",
		CreatedUtc = clock.UtcNow
	};

	/// <summary>"; coupon CODE" or "; offer CODE - Title, ..." for a pass invoice line, "" when neither.</summary>
	public static string InvoiceNote(OfferPassPrice? price) =>
		price == null || price.Calc.Lines.Count == 0 ? ""
		: IsCoupon(price) ? "; coupon " + price.Calc.Lines[0].Offer.Code
		: "; offer " + string.Join(", ", price.Calc.Lines.Select(l => OfferEngine.Label(l.Offer)));

	// ---- bookings (called from BookingService / PaymentService) ------------------------------------

	/// <summary>"; coupon CODE" for the booking's invoice line, or "" when no coupon is on it.</summary>
	public static async Task<string> InvoiceNoteAsync(RegalisDbContext db, long bookingId, CancellationToken ct)
	{
		if (await db.Set<OfferRedemption>().AnyAsync(r => r.BookingId == bookingId, ct))
		{
			return "";
		}
		string? code = await db.Set<CouponRedemption>().AsNoTracking().Where(r => r.BookingId == bookingId).OrderBy(r => r.RedemptionId).Select(r => r.CouponCode).FirstOrDefaultAsync(ct);
		return code == null ? "" : "; coupon " + code;
	}

	/// <summary>Server-side re-check just before money moves for a held booking: a coupon on it must
	/// still exist, be switched on and be inside its dates. Throws 422 COUPON_NOT_AVAILABLE otherwise.
	/// The limits were checked under the coupon's lock when it was put on the hold, which counts as a use.</summary>
	public static async Task EnsureBookingCouponValidAsync(RegalisDbContext db, DateTime utcNow, long bookingId, CancellationToken ct)
	{
		var row = await db.Set<CouponRedemption>().AsNoTracking().Where(r => r.BookingId == bookingId).Select(r => new { r.CouponId, r.CouponCode }).FirstOrDefaultAsync(ct);
		if (row == null || await db.Set<OfferRedemption>().AnyAsync(r => r.BookingId == bookingId, ct))
		{
			return;
		}
		DateOnly today = IstClock.IstDateOf(utcNow);
		Coupon? c = await db.Set<Coupon>().IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(x => x.CouponId == row.CouponId, ct);
		if (c == null || c.IsDeleted || !c.IsActive || c.ValidFrom > today || c.ValidUntil < today)
		{
			throw new BusinessRuleException($"The coupon {row.CouponCode} is no longer available, so this discounted price cannot be charged. Please start the booking again.", NotAvailable);
		}
	}
}
