using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Coupons;
using Regalis.Domain.Entities.Coupons;
using Regalis.Domain.Entities.Offers;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services.Offers;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.Coupons;

/// <summary>Admin portal: the Coupon codes tab of Sales > Offers. Coupons are master data: audited and
/// soft-deleted. A delete is refused once the coupon is on a purchase; switch it off instead.</summary>
public class CouponAdminService(RegalisDbContext db, CouponEngine engine, IDateTimeProvider clock, ILogger<CouponAdminService> logger) : ICouponAdminService
{
	/// <summary>Same rule as offer codes.</summary>
	private static readonly Regex CodePattern = new("^[A-Z0-9][A-Z0-9_-]{2,29}$", RegexOptions.Compiled);

	public async Task<CouponListDto> ListAsync(CouponListRequest request, CancellationToken ct = default)
	{
		DateOnly today = engine.Today;
		IQueryable<Coupon> all = db.Set<Coupon>().AsNoTracking();
		var summary = new CouponSummaryDto(
			await all.CountAsync(ct),
			await all.CountAsync(c => c.IsActive && c.ValidFrom <= today && c.ValidUntil >= today, ct),
			await all.CountAsync(c => !c.IsActive && c.ValidUntil >= today, ct),
			await all.CountAsync(c => c.ValidUntil < today, ct),
			await all.CountAsync(c => c.IsActive && c.ValidFrom > today, ct));

		IQueryable<Coupon> q = all;
		if (!string.IsNullOrWhiteSpace(request.Search))
		{
			string s = request.Search.Trim();
			q = q.Where(c => c.Code.Contains(s) || (c.Description != null && c.Description.Contains(s)));
		}
		q = (request.Status ?? "all").Trim().ToLowerInvariant() switch
		{
			"active" => q.Where(c => c.IsActive && c.ValidFrom <= today && c.ValidUntil >= today),
			"inactive" => q.Where(c => !c.IsActive && c.ValidUntil >= today),
			"expired" => q.Where(c => c.ValidUntil < today),
			"scheduled" => q.Where(c => c.IsActive && c.ValidFrom > today),
			_ => q
		};
		if (request.Module is byte mb && mb >= 1 && mb <= 3)
		{
			OfferModule m = (OfferModule)mb;
			q = q.Where(c => db.Set<CouponModuleLink>().Any(l => l.CouponId == c.CouponId && l.Module == m));
		}
		int count = await q.CountAsync(ct);
		q = (request.Sort ?? "").Trim().ToLowerInvariant() switch
		{
			"code" => q.OrderBy(c => c.Code),
			"ending" => q.OrderBy(c => c.ValidUntil).ThenBy(c => c.CouponId),
			_ => q.OrderByDescending(c => c.CreatedDate).ThenByDescending(c => c.CouponId)
		};
		List<Coupon> page = await q.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
		int[] ids = page.Select(c => c.CouponId).ToArray();
		Dictionary<int, HashSet<OfferModule>> mods = await engine.ModulesAsync(ids, ct);
		Dictionary<int, int> uses = await engine.UsesAsync(ids, null, null, ct);
		Dictionary<int, decimal> given = await GivenAsync(ids, ct);
		HashSet<string> clashes = await ClashesAsync(page.Select(c => c.Code).ToArray(), ct);
		List<CouponRowDto> rows = page.Select(c => new CouponRowDto(c.CouponId, c.Code, c.Description, (byte)c.DiscountType, c.DiscountValue, c.MaxDiscountAmount, c.MinAmount,
			Mods(mods, c.CouponId), c.ValidFrom, c.ValidUntil, c.TotalUses, c.UsesPerCustomer, c.IsActive, CouponEngine.StatusOf(c, today), c.CreatedDate,
			uses.GetValueOrDefault(c.CouponId), given.GetValueOrDefault(c.CouponId), clashes.Contains(c.Code))).ToList();
		return new CouponListDto(summary, PagedResult<CouponRowDto>.Create(rows, request.Page, request.PageSize, count));
	}

	private static IReadOnlyList<byte> Mods(Dictionary<int, HashSet<OfferModule>> mods, int id) =>
		mods.TryGetValue(id, out var ms) ? ms.Select(x => (byte)x).OrderBy(x => x).ToList() : new List<byte>();

	private async Task<Dictionary<int, decimal>> GivenAsync(int[] ids, CancellationToken ct)
	{
		var sums = await engine.Counting(db.Set<CouponRedemption>().AsNoTracking().Where(r => ids.Contains(r.CouponId)))
			.GroupBy(r => r.CouponId).Select(g => new { g.Key, Sum = g.Sum(r => r.DiscountAmount) }).ToListAsync(ct);
		return sums.ToDictionary(x => x.Key, x => x.Sum);
	}

	/// <summary>Codes that an offer (not deleted) also uses.</summary>
	private async Task<HashSet<string>> ClashesAsync(string[] codes, CancellationToken ct) =>
		(await db.Set<Offer>().AsNoTracking().Where(o => o.Code != null && codes.Contains(o.Code)).Select(o => o.Code!).ToListAsync(ct)).ToHashSet();

	public async Task<CouponDetailDto> GetAsync(int couponId, CancellationToken ct = default)
	{
		Coupon c = await db.Set<Coupon>().AsNoTracking().FirstOrDefaultAsync(x => x.CouponId == couponId, ct) ?? throw new NotFoundException("Coupon", couponId);
		return await DetailAsync(c, ct);
	}

	private async Task<CouponDetailDto> DetailAsync(Coupon c, CancellationToken ct)
	{
		Dictionary<int, HashSet<OfferModule>> mods = await engine.ModulesAsync(new[] { c.CouponId }, ct);
		Dictionary<int, int> uses = await engine.UsesAsync(new[] { c.CouponId }, null, null, ct);
		Dictionary<int, decimal> given = await GivenAsync(new[] { c.CouponId }, ct);
		bool clash = (await ClashesAsync(new[] { c.Code }, ct)).Count > 0;
		return new CouponDetailDto(c.CouponId, c.Code, c.Description, (byte)c.DiscountType, c.DiscountValue, c.MaxDiscountAmount, c.MinAmount, Mods(mods, c.CouponId),
			c.ValidFrom, c.ValidUntil, c.TotalUses, c.UsesPerCustomer, c.IsActive, CouponEngine.StatusOf(c, engine.Today), c.CreatedDate, c.ModifiedDate,
			uses.GetValueOrDefault(c.CouponId), given.GetValueOrDefault(c.CouponId), !await InUseAsync(c.CouponId, ct), clash);
	}

	/// <summary>On a purchase that went through or is in progress (a cancelled booking still had its
	/// invoice): not safe to delete. Only expired holds and abandoned online orders are ignored.</summary>
	private async Task<bool> InUseAsync(int couponId, CancellationToken ct)
	{
		IQueryable<CouponRedemption> rows = db.Set<CouponRedemption>().AsNoTracking().Where(r => r.CouponId == couponId);
		if (await rows.AnyAsync(r => r.CustomerPassId != null, ct))
		{
			return true;
		}
		if (await rows.AnyAsync(r => r.BookingId != null && db.Bookings.Any(b => b.BookingId == r.BookingId && b.Status != BookingStatus.Expired), ct))
		{
			return true;
		}
		return await engine.Counting(rows).AnyAsync(ct);
	}

	public async Task<CouponDetailDto> CreateAsync(SaveCouponRequest request, CancellationToken ct = default)
	{
		Clean c = await ValidateAsync(request, null, ct);
		var coupon = new Coupon();
		Apply(coupon, c);
		db.Set<Coupon>().Add(coupon);
		await db.SaveChangesAsync(ct);
		await SaveModulesAsync(coupon.CouponId, c.Modules, ct);
		logger.LogInformation("Coupon {CouponId} {Code} created", coupon.CouponId, coupon.Code);
		return await DetailAsync(coupon, ct);
	}

	public async Task<CouponDetailDto> UpdateAsync(int couponId, SaveCouponRequest request, CancellationToken ct = default)
	{
		Coupon coupon = await db.Set<Coupon>().FirstOrDefaultAsync(x => x.CouponId == couponId, ct) ?? throw new NotFoundException("Coupon", couponId);
		Clean c = await ValidateAsync(request, coupon, ct);
		if (c.Code != coupon.Code && await InUseAsync(couponId, ct))
		{
			throw new AppValidationException(new List<ApiError> { new("VALIDATION_ERROR", "Customers have used this coupon, so its code cannot change. Create a new coupon instead.", "code") });
		}
		Apply(coupon, c);
		await db.SaveChangesAsync(ct);
		await SaveModulesAsync(coupon.CouponId, c.Modules, ct);
		return await DetailAsync(coupon, ct);
	}

	public async Task<CouponDetailDto> SetActiveAsync(int couponId, bool active, CancellationToken ct = default)
	{
		Coupon c = await db.Set<Coupon>().FirstOrDefaultAsync(x => x.CouponId == couponId, ct) ?? throw new NotFoundException("Coupon", couponId);
		if (active && c.ValidUntil < engine.Today)
		{
			throw new BusinessRuleException($"This coupon expired on {c.ValidUntil:dd MMM yyyy}. Edit it and move the Valid until date first.");
		}
		if (active && !await db.Set<CouponModuleLink>().AnyAsync(l => l.CouponId == couponId, ct))
		{
			throw new BusinessRuleException("Choose at least one service (My Bookings, Passes or Membership) before activating.");
		}
		c.IsActive = active;
		await db.SaveChangesAsync(ct);
		return await DetailAsync(c, ct);
	}

	public async Task DeleteAsync(int couponId, CancellationToken ct = default)
	{
		Coupon c = await db.Set<Coupon>().FirstOrDefaultAsync(x => x.CouponId == couponId, ct) ?? throw new NotFoundException("Coupon", couponId);
		if (await InUseAsync(couponId, ct))
		{
			throw new BusinessRuleException("Customers have used this coupon, so it is kept for the record. Deactivate it instead.", "COUPON_IN_USE");
		}
		// Soft delete (the audit interceptor turns Remove into IsDeleted): the code is freed for reuse.
		c.IsActive = false;
		db.Set<Coupon>().Remove(c);
		await db.SaveChangesAsync(ct);
		logger.LogInformation("Coupon {CouponId} {Code} deleted", c.CouponId, c.Code);
	}

	private sealed record Clean(string Code, string? Description, OfferDiscountType Type, decimal Value, decimal? Max, decimal? Min, List<OfferModule> Modules, DateOnly From, DateOnly Until, int? Total, int? PerCustomer, bool Active);

	private static void Apply(Coupon c, Clean v)
	{
		c.Code = v.Code;
		c.Description = v.Description;
		c.DiscountType = v.Type;
		c.DiscountValue = v.Value;
		c.MaxDiscountAmount = v.Max;
		c.MinAmount = v.Min;
		c.ValidFrom = v.From;
		c.ValidUntil = v.Until;
		c.TotalUses = v.Total;
		c.UsesPerCustomer = v.PerCustomer;
		c.IsActive = v.Active;
	}

	private async Task SaveModulesAsync(int couponId, List<OfferModule> modules, CancellationToken ct)
	{
		db.Set<CouponModuleLink>().RemoveRange(await db.Set<CouponModuleLink>().Where(l => l.CouponId == couponId).ToListAsync(ct));
		await db.SaveChangesAsync(ct);
		foreach (OfferModule m in modules)
		{
			db.Set<CouponModuleLink>().Add(new CouponModuleLink { CouponId = couponId, Module = m });
		}
		await db.SaveChangesAsync(ct);
	}

	private async Task<Clean> ValidateAsync(SaveCouponRequest r, Coupon? existing, CancellationToken ct)
	{
		var errors = new List<ApiError>();
		void Err(string field, string message) => errors.Add(new ApiError("VALIDATION_ERROR", message, field));
		static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

		string? code = T(r.Code)?.ToUpperInvariant();
		string? description = T(r.Description);
		if (code == null)
		{
			Err("code", "Enter the coupon code.");
		}
		else if (!CodePattern.IsMatch(code))
		{
			Err("code", "Use 3 to 30 letters, digits, - or _ for the code.");
		}
		else
		{
			int self = existing?.CouponId ?? 0;
			if (await db.Set<Coupon>().AnyAsync(c => c.Code == code && c.CouponId != self, ct))
			{
				Err("code", $"Another coupon already uses the code {code}.");
			}
			else if (await db.Set<Offer>().AnyAsync(o => o.Code == code, ct) && existing?.Code != code)
			{
				Err("code", $"The offer code {code} already exists. Choose another code.");
			}
		}
		if (description?.Length > 300) Err("description", "Keep the description under 300 characters.");

		OfferDiscountType type = (OfferDiscountType)r.DiscountType;
		if (type != OfferDiscountType.Percentage && type != OfferDiscountType.Fixed) Err("discountType", "Choose percentage or fixed amount.");
		if (r.DiscountValue <= 0m) Err("discountValue", "The discount must be more than zero.");
		else if (type == OfferDiscountType.Percentage && r.DiscountValue > 100m) Err("discountValue", "A percentage cannot be more than 100.");
		else if (r.DiscountValue > 10_000_000m) Err("discountValue", "That discount is too large.");
		else if (decimal.Round(r.DiscountValue, 2) != r.DiscountValue) Err("discountValue", "Use at most two decimals.");
		decimal? max = type == OfferDiscountType.Percentage ? r.MaxDiscountAmount : null;
		if (max.HasValue && max <= 0m) Err("maxDiscountAmount", "Leave the maximum empty, or make it more than zero.");
		if (r.MinAmount.HasValue && r.MinAmount < 0m) Err("minAmount", "The minimum cannot be negative.");
		decimal? min = r.MinAmount is decimal mm && mm > 0m ? mm : null;

		List<OfferModule> modules = (r.Modules ?? Array.Empty<byte>()).Distinct().Where(b => b >= 1 && b <= 3).Select(b => (OfferModule)b).OrderBy(b => b).ToList();
		if (modules.Count == 0) Err("modules", "Choose where the coupon can be used: My Bookings, Passes and/or Membership.");

		DateOnly today = engine.Today;
		if (r.ValidFrom == null) Err("validFrom", "Choose the Valid from date.");
		if (r.ValidUntil == null) Err("validUntil", "Choose the Valid until date.");
		if (r.ValidFrom is DateOnly s && r.ValidUntil is DateOnly e)
		{
			if (e < s) Err("validUntil", "Valid until must be on or after Valid from.");
			else if (e < today && (existing == null || existing.ValidUntil != e)) Err("validUntil", "Valid until is in the past.");
		}
		if (r.TotalUses is int tot && (tot < 1 || tot > 10_000_000)) Err("totalUses", "Leave it empty for no limit, or use 1 or more.");
		if (r.UsesPerCustomer is int per && (per < 1 || per > 100_000)) Err("usesPerCustomer", "Leave it empty for no limit, or use 1 or more.");
		if (r.TotalUses is int t2 && r.UsesPerCustomer is int p2 && p2 > t2) Err("usesPerCustomer", "Uses per customer cannot be more than the total uses.");
		if (errors.Count > 0)
		{
			throw new AppValidationException(errors);
		}
		return new Clean(code!, description, type, r.DiscountValue, max, min, modules, r.ValidFrom!.Value, r.ValidUntil!.Value, r.TotalUses, r.UsesPerCustomer,
			r.IsActive && r.ValidUntil!.Value >= today);
	}

	public async Task<List<CouponRedemptionRowDto>> RedemptionsAsync(int couponId, CancellationToken ct = default)
	{
		if (!await db.Set<Coupon>().AnyAsync(c => c.CouponId == couponId, ct))
		{
			throw new NotFoundException("Coupon", couponId);
		}
		DateTime cutoff = clock.UtcNow.AddMinutes(-OfferEngine.OrderHoldMinutes);
		var rows = await (from r in db.Set<CouponRedemption>().AsNoTracking()
			where r.CouponId == couponId
			join u in db.Users.IgnoreQueryFilters() on r.CustomerId equals u.UserId
			orderby r.RedemptionId descending
			select new
			{
				r,
				u.FullName,
				Replaced = r.BookingId != null && db.Set<OfferRedemption>().Any(o => o.BookingId == r.BookingId),
				Booking = db.Bookings.IgnoreQueryFilters().Where(b => b.BookingId == r.BookingId).Select(b => new { b.BookingNumber, b.Status }).FirstOrDefault(),
				Pass = db.CustomerPasses.IgnoreQueryFilters().Where(p => p.CustomerPassId == r.CustomerPassId).Select(p => new { p.PassNumber, p.Status }).FirstOrDefault(),
				Pay = db.Payments.IgnoreQueryFilters().Where(p => p.PaymentId == r.PaymentId).Select(p => new { p.PaymentNumber, p.Status, p.CreatedDate }).FirstOrDefault()
			}).Take(500).ToListAsync(ct);
		return rows.Select(x =>
		{
			string reference;
			string state;
			if (x.r.BookingId != null)
			{
				reference = x.Booking?.BookingNumber ?? $"Booking {x.r.BookingId}";
				state = x.Booking == null || x.Replaced ? "Released"
					: x.Booking.Status == BookingStatus.Pending ? "Held"
					: (x.Booking.Status == BookingStatus.Cancelled || x.Booking.Status == BookingStatus.Expired) ? "Released" : "Used";
			}
			else if (x.r.CustomerPassId != null)
			{
				reference = x.Pass?.PassNumber ?? $"Pass {x.r.CustomerPassId}";
				state = x.Pass == null || x.Pass.Status == CustomerPassStatus.Cancelled ? "Released" : "Used";
			}
			else
			{
				reference = x.Pay?.PaymentNumber ?? "Online order";
				state = x.Pay != null && ((x.Pay.Status == PaymentStatus.Pending && x.Pay.CreatedDate > cutoff) || x.Pay.Status == PaymentStatus.Success) ? "Held" : "Released";
			}
			return new CouponRedemptionRowDto(x.r.RedemptionId, x.r.CreatedUtc, x.r.CustomerId, x.FullName, (byte)x.r.Module, reference, x.r.BaseAmount, x.r.DiscountAmount, state);
		}).ToList();
	}
}
