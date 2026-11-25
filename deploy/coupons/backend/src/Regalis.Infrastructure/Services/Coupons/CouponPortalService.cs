using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Coupons;
using Regalis.Application.Finance;
using Regalis.Application.Offers;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Coupons;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Offers;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services.Offers;

namespace Regalis.Infrastructure.Services.Coupons;

/// <summary>Customer portal side of coupons. Every amount comes from CouponEngine (OfferEngine's
/// arithmetic), so the preview and the charge cannot disagree.</summary>
public class CouponPortalService(RegalisDbContext db, CouponEngine engine, OfferEngine offers, ISettingService settings, IPassService passes, ICurrentUser currentUser, IDateTimeProvider clock, ILogger<CouponPortalService> logger) : ICouponPortalService
{
	private void RequireCustomer()
	{
		if (currentUser.UserType != UserType.Customer)
		{
			throw new ForbiddenException("Coupons are applied by customers in the customer portal.");
		}
	}

	private static OfferModule Module(string? value) =>
		OfferEngine.ParseModule(value) ?? throw new BusinessRuleException("Choose the module: booking, pass or membership.");

	public async Task<List<PortalCouponDto>> ListAsync(string? module, CancellationToken ct = default)
	{
		OfferModule m = Module(module);
		List<Coupon> coupons = await engine.Live(m).Where(c => c.ShowInPortal).AsNoTracking().OrderBy(c => c.ValidUntil).ThenBy(c => c.CouponId).ToListAsync(ct);
		int[] ids = coupons.Select(c => c.CouponId).ToArray();
		Dictionary<int, HashSet<OfferModule>> mods = await engine.ModulesAsync(ids, ct);
		Dictionary<int, int> mine = currentUser.UserType == UserType.Customer ? await engine.UsesAsync(ids, currentUser.UserId, null, ct) : new Dictionary<int, int>();
		Dictionary<int, int> all = await engine.UsesAsync(coupons.Where(c => c.TotalUses > 0).Select(c => c.CouponId).ToArray(), null, null, ct);
		return coupons
			// A coupon nobody can use any more is not advertised.
			.Where(c => !(c.TotalUses is int t && t > 0 && all.GetValueOrDefault(c.CouponId) >= t))
			.Select(c => new PortalCouponDto(c.CouponId, c.Code, c.Description, (byte)c.DiscountType, c.DiscountValue, c.MaxDiscountAmount, c.MinAmount, c.ValidFrom, c.ValidUntil,
				mods.TryGetValue(c.CouponId, out var ms) ? ms.Select(x => (byte)x).OrderBy(x => x).ToList() : new List<byte>(),
				c.UsesPerCustomer is int per && per > 0 ? Math.Max(0, per - mine.GetValueOrDefault(c.CouponId)) : null))
			.ToList();
	}

	public async Task<CouponPreviewDto> PreviewAsync(CouponPreviewRequest request, CancellationToken ct = default)
	{
		RequireCustomer();
		OfferModule m = Module(request.Module);
		(int? _, List<decimal> bases) = await PurchaseAsync(m, request, ct);
		(decimal cg, decimal sg) = await offers.TaxPercentsAsync(ct);
		OfferCalculation none = OfferEngine.NoOffer(bases, cg, sg);
		string code = CouponEngine.Normalize(request.Code);
		if (code.Length == 0)
		{
			return Result(false, false, "Enter a coupon code.", CouponEngine.Invalid, null, none, none, cg, sg);
		}
		(Coupon? c, CouponEngine.Failure? f) = await engine.EvaluateAsync(code, m, bases.Sum(), currentUser.UserId, null, ct);
		if (c == null)
		{
			bool found = f!.Code != CouponEngine.Invalid;
			return Result(found, false, f.Message, f.Code, null, none, none, cg, sg);
		}
		OfferCalculation calc = CouponEngine.Price(c, bases, cg, sg);
		var applied = new CouponAppliedDto(c.CouponId, c.Code, c.Description, (byte)c.DiscountType, c.DiscountValue, calc.Discount);
		return Result(true, true, null, null, applied, calc, none, cg, sg);
	}

	private static CouponPreviewDto Result(bool found, bool valid, string? error, string? errorCode, CouponAppliedDto? coupon, OfferCalculation calc, OfferCalculation none, decimal cg, decimal sg) =>
		new(found, valid, error, errorCode, coupon,
			coupon == null ? new List<OfferAppliedDto>() : new List<OfferAppliedDto> { new(0, "Coupon " + coupon.Code, coupon.Code, coupon.DiscountAmount) },
			calc.Base, calc.Discount, calc.Taxable, cg, calc.Cgst, sg, calc.Sgst, calc.Total, none.Total,
			calc.Items.Select(i => new OfferPriceItemDto(i.Base, i.Discount, i.Taxable, i.Cgst, i.Sgst, i.Total)).ToList());

	/// <summary>What is being bought: the price before GST of each item. Plans are priced by the pass
	/// engine itself (same as the offers preview).</summary>
	private async Task<(int? RefId, List<decimal> Bases)> PurchaseAsync(OfferModule m, CouponPreviewRequest request, CancellationToken ct)
	{
		if (m == OfferModule.Booking)
		{
			List<decimal> amounts = (request.BaseAmounts ?? Array.Empty<decimal>()).ToList();
			if (amounts.Count == 0 || amounts.Count > 62 || amounts.Any(a => a < 0m || a > 100_000_000m))
			{
				throw new BusinessRuleException("Give the price of the booking before GST.");
			}
			if (request.WorkspaceId is int wid && !await db.Workspaces.AnyAsync(w => w.WorkspaceId == wid, ct))
			{
				throw new NotFoundException("Workspace", wid);
			}
			return (null, amounts);
		}
		int planId = request.PassPlanId ?? throw new BusinessRuleException("Choose a plan first.");
		PassPlan plan = await db.PassPlans.AsNoTracking().FirstOrDefaultAsync(p => p.PassPlanId == planId && p.IsActive, ct) ?? throw new NotFoundException("PassPlan", planId);
		if (OfferEngine.ModuleForPass(plan.PassType) != m)
		{
			throw new BusinessRuleException(m == OfferModule.Pass ? "That plan is a membership, not a pass." : "That plan is a pass, not a membership.");
		}
		PassPurchaseOptionsDto options = await passes.GetPurchaseOptionsAsync((byte)plan.PassType, ct);
		decimal taxable = options.Plans.FirstOrDefault(p => p.PassPlanId == planId)?.TaxableAmount ?? plan.Price;
		return (planId, new List<decimal> { taxable });
	}

	public async Task<ApplyBookingOffersResultDto> ApplyToBookingsAsync(ApplyBookingCouponRequest request, CancellationToken ct = default)
	{
		RequireCustomer();
		long me = currentUser.UserId;
		long[] ids = (request.BookingIds ?? Array.Empty<long>()).ToArray();
		if (ids.Length == 0 || ids.Length > 62 || ids.Distinct().Count() != ids.Length)
		{
			throw new BusinessRuleException("Choose the booking to apply the coupon to.");
		}
		string code = CouponEngine.Normalize(request.Code);
		int holdMinutes = await settings.GetIntAsync("Booking.PendingHoldMinutes", 10, ct);
		(decimal cg, decimal sg) = await offers.TaxPercentsAsync(ct);

		ApplyBookingOffersResultDto result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
		{
			db.ChangeTracker.Clear();
			await using IDbContextTransaction tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
			List<Booking> bookings = await db.Bookings.Where(b => ids.Contains(b.BookingId)).ToListAsync(ct);
			if (bookings.Count != ids.Length || bookings.Any(b => b.CustomerId != me))
			{
				throw new NotFoundException("Booking", ids.First(id => !bookings.Any(b => b.BookingId == id && b.CustomerId == me)));
			}
			bookings = ids.Select(id => bookings.First(b => b.BookingId == id)).ToList();
			DateTime now = clock.UtcNow;
			foreach (Booking b in bookings)
			{
				if (b.Status != BookingStatus.Pending || b.BookingSource != BookingSource.Customer)
				{
					throw new BusinessRuleException($"{b.BookingNumber} is not awaiting payment, so a coupon cannot be applied to it.", CouponEngine.NotApplicable);
				}
				if (b.CreatedDate.AddMinutes(holdMinutes) <= now)
				{
					throw new BusinessRuleException($"The hold on {b.BookingNumber} has expired. Start a new booking.", CouponEngine.NotApplicable);
				}
			}
			if (await db.Payments.AnyAsync(p => p.BookingId != null && ids.Contains(p.BookingId.Value) && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Success), ct))
			{
				throw new BusinessRuleException("Payment for this booking has already started, so its discount can no longer be changed.", CouponEngine.NotApplicable);
			}

			List<decimal> bases = bookings.Select(b => b.BaseAmount).ToList();
			Coupon? coupon = null;
			OfferCalculation calc = OfferEngine.NoOffer(bases, cg, sg);
			if (code.Length > 0)
			{
				(coupon, calc) = await engine.CalculateAsync(code, OfferModule.Booking, bases, me, ids, ct);
				await engine.RecheckLimitsLockedAsync(coupon.CouponId, me, ids, ct);
			}

			// A coupon replaces whatever was on the hold before: offers and an earlier coupon.
			List<OfferRedemption> oldOffers = await db.Set<OfferRedemption>().Where(r => r.BookingId != null && ids.Contains(r.BookingId.Value)).ToListAsync(ct);
			List<CouponRedemption> oldCoupons = await db.Set<CouponRedemption>().Where(r => r.BookingId != null && ids.Contains(r.BookingId.Value)).ToListAsync(ct);
			db.Set<OfferRedemption>().RemoveRange(oldOffers);
			db.Set<CouponRedemption>().RemoveRange(oldCoupons);

			Guid key = Guid.NewGuid();
			for (int i = 0; i < bookings.Count; i++)
			{
				Booking b = bookings[i];
				OfferItemPrice price = calc.Items[i];
				bool changed = b.DiscountAmount != price.Discount || b.TotalAmount != price.Total;
				b.DiscountAmount = price.Discount;
				b.TaxAmount = price.Cgst + price.Sgst;
				b.TotalAmount = price.Total;
				if (coupon != null)
				{
					db.Set<CouponRedemption>().Add(new CouponRedemption
					{
						CouponId = coupon.CouponId,
						CustomerId = me,
						Module = OfferModule.Booking,
						BookingId = b.BookingId,
						PurchaseKey = key,
						BaseAmount = b.BaseAmount,
						DiscountAmount = price.Discount,
						CouponCode = coupon.Code,
						CreatedUtc = now
					});
				}
				if (changed || oldOffers.Count > 0 || oldCoupons.Count > 0)
				{
					db.Set<BookingStatusHistory>().Add(new BookingStatusHistory
					{
						BookingId = b.BookingId,
						FromStatus = BookingStatus.Pending,
						ToStatus = BookingStatus.Pending,
						ChangedByUserId = me,
						ChangedUtc = now,
						Reason = OfferEngine.Trim(coupon == null
							? $"Coupon removed; total ₹{price.Total:N2}."
							: $"Coupon {coupon.Code} applied: -₹{price.Discount:N2} before GST; total ₹{price.Total:N2}.")
					});
				}
			}
			await db.SaveChangesAsync(ct);
			await tx.CommitAsync(ct);
			return new ApplyBookingOffersResultDto(
				coupon == null ? new List<OfferAppliedDto>() : new List<OfferAppliedDto> { new(0, "Coupon " + coupon.Code, coupon.Code, calc.Discount) },
				calc.Discount, calc.Total,
				bookings.Select(b => new BookingOfferResultItemDto(b.BookingId, b.BookingNumber, b.BaseAmount, b.DiscountAmount, b.TaxAmount, b.TotalAmount)).ToList());
		});
		if (code.Length > 0)
		{
			logger.LogInformation("Customer {CustomerId} applied coupon {Code} to bookings {Bookings}: -{Discount}", me, code, string.Join(",", ids), result.DiscountAmount);
		}
		return result;
	}
}
