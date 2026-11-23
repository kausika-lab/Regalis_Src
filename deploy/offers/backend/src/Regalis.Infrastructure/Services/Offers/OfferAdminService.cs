using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Offers;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Offers;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.Offers;

/// <summary>Admin portal: Offers Management (brief §1, §2, §8). Offers are master data: audited and
/// soft-deleted. A delete is refused once the offer is on a purchase; switch it off instead.</summary>
public class OfferAdminService(RegalisDbContext db, OfferEngine engine, IDateTimeProvider clock, ILogger<OfferAdminService> logger) : IOfferAdminService
{
	public const int MaxImageBytes = 2 * 1024 * 1024;

	private static readonly Regex CodePattern = new("^[A-Z0-9][A-Z0-9_-]{2,29}$", RegexOptions.Compiled);

	// ---- list / overview -------------------------------------------------------------------------

	public async Task<OfferListDto> ListAsync(OfferListRequest request, CancellationToken ct = default)
	{
		DateOnly today = engine.Today;
		IQueryable<Offer> all = db.Set<Offer>().AsNoTracking();
		var summary = new OfferSummaryDto(
			await all.CountAsync(ct),
			await all.CountAsync(o => o.IsActive && o.StartDate <= today && o.EndDate >= today, ct),
			await all.CountAsync(o => !o.IsActive && o.EndDate >= today, ct),
			await all.CountAsync(o => o.EndDate < today, ct),
			await all.CountAsync(o => o.IsActive && o.StartDate > today, ct));

		IQueryable<Offer> q = all;
		if (!string.IsNullOrWhiteSpace(request.Search))
		{
			string s = request.Search.Trim();
			q = q.Where(o => o.OfferName.Contains(s) || o.Title.Contains(s) || (o.Code != null && o.Code.Contains(s)));
		}
		q = (request.Status ?? "all").Trim().ToLowerInvariant() switch
		{
			"active" => q.Where(o => o.IsActive && o.StartDate <= today && o.EndDate >= today),
			"inactive" => q.Where(o => !o.IsActive && o.EndDate >= today),
			"expired" => q.Where(o => o.EndDate < today),
			"scheduled" => q.Where(o => o.IsActive && o.StartDate > today),
			_ => q
		};
		if (request.Module is byte mb && mb >= 1 && mb <= 3)
		{
			OfferModule m = (OfferModule)mb;
			q = q.Where(o => db.Set<OfferModuleLink>().Any(l => l.OfferId == o.OfferId && l.Module == m));
		}
		int count = await q.CountAsync(ct);
		q = (request.Sort ?? "").Trim().ToLowerInvariant() switch
		{
			"newest" => q.OrderByDescending(o => o.CreatedDate).ThenByDescending(o => o.OfferId),
			"name" => q.OrderBy(o => o.OfferName).ThenBy(o => o.OfferId),
			"ending" => q.OrderBy(o => o.EndDate).ThenBy(o => o.OfferId),
			_ => q.OrderBy(o => o.SortOrder).ThenByDescending(o => o.OfferId)
		};
		List<Offer> page = await q.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
		List<OfferRowDto> rows = await RowsAsync(page, today, ct);
		return new OfferListDto(summary, PagedResult<OfferRowDto>.Create(rows, request.Page, request.PageSize, count));
	}

	private async Task<List<OfferRowDto>> RowsAsync(List<Offer> offers, DateOnly today, CancellationToken ct)
	{
		int[] ids = offers.Select(o => o.OfferId).ToArray();
		OfferEngine.Scope scope = await engine.ScopeAsync(ids, ct);
		Dictionary<int, int> uses = await engine.UsesAsync(ids, null, null, ct);
		Dictionary<int, decimal> given = await GivenAsync(ids, ct);
		return offers.Select(o => new OfferRowDto(o.OfferId, o.OfferName, o.Title, o.Code, (byte)o.DiscountType, o.DiscountValue, o.MaxDiscountAmount, o.MinAmount,
			Mods(scope, o.OfferId), o.StartDate, o.EndDate, o.IsActive, OfferEngine.StatusOf(o, today), o.CanCombine, o.ShowInPortal, o.SortOrder, o.CreatedDate,
			uses.GetValueOrDefault(o.OfferId), given.GetValueOrDefault(o.OfferId), o.HasImage, o.ImageVersion)).ToList();
	}

	private static IReadOnlyList<byte> Mods(OfferEngine.Scope scope, int id) =>
		scope.Modules.TryGetValue(id, out var ms) ? ms.Select(x => (byte)x).OrderBy(x => x).ToList() : new List<byte>();

	private async Task<Dictionary<int, decimal>> GivenAsync(int[] ids, CancellationToken ct)
	{
		var sums = await engine.Counting(db.Set<OfferRedemption>().AsNoTracking().Where(r => ids.Contains(r.OfferId)))
			.GroupBy(r => r.OfferId).Select(g => new { g.Key, Sum = g.Sum(r => r.DiscountAmount) }).ToListAsync(ct);
		return sums.ToDictionary(x => x.Key, x => x.Sum);
	}

	public async Task<OfferDetailDto> GetAsync(int offerId, CancellationToken ct = default)
	{
		Offer o = await db.Set<Offer>().AsNoTracking().FirstOrDefaultAsync(x => x.OfferId == offerId, ct) ?? throw new NotFoundException("Offer", offerId);
		return await DetailAsync(o, ct);
	}

	private async Task<OfferDetailDto> DetailAsync(Offer o, CancellationToken ct)
	{
		OfferEngine.Scope scope = await engine.ScopeAsync(new[] { o.OfferId }, ct);
		List<int> Refs(OfferModule m) => scope.Refs.TryGetValue((o.OfferId, m), out var r) ? r.OrderBy(x => x).ToList() : new List<int>();
		Dictionary<int, int> uses = await engine.UsesAsync(new[] { o.OfferId }, null, null, ct);
		Dictionary<int, decimal> given = await GivenAsync(new[] { o.OfferId }, ct);
		return new OfferDetailDto(o.OfferId, o.OfferName, o.Title, o.Description, o.Terms, o.Code, o.ShowInPortal, (byte)o.DiscountType, o.DiscountValue, o.MaxDiscountAmount, o.MinAmount,
			Mods(scope, o.OfferId), Refs(OfferModule.Booking), Refs(OfferModule.Pass), Refs(OfferModule.Membership), o.StartDate, o.EndDate, o.IsActive, OfferEngine.StatusOf(o, engine.Today),
			o.CanCombine, o.SortOrder, o.UsesPerCustomer, o.TotalUses, o.HasImage, o.ImageVersion, o.CreatedDate, o.ModifiedDate,
			uses.GetValueOrDefault(o.OfferId), given.GetValueOrDefault(o.OfferId), !await InUseAsync(o.OfferId, ct));
	}

	/// <summary>On a purchase that went through or is in progress (a cancelled booking still had its
	/// invoice): not safe to delete. Only expired holds and abandoned online orders are ignored.</summary>
	private async Task<bool> InUseAsync(int offerId, CancellationToken ct)
	{
		IQueryable<OfferRedemption> rows = db.Set<OfferRedemption>().AsNoTracking().Where(r => r.OfferId == offerId);
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

	public async Task<OfferOptionsDto> OptionsAsync(CancellationToken ct = default)
	{
		var types = await db.WorkspaceTypes.AsNoTracking().Where(t => t.IsActive).OrderBy(t => t.WorkspaceTypeId)
			.Select(t => new OfferOptionItemDto(t.WorkspaceTypeId, t.TypeName, null)).ToListAsync(ct);
		var plans = await db.PassPlans.AsNoTracking().OrderBy(p => p.DisplayOrder).ThenBy(p => p.PassPlanId)
			.Select(p => new { p.PassPlanId, p.PlanName, p.Price, p.PassType, p.IsActive }).ToListAsync(ct);
		return new OfferOptionsDto(types,
			plans.Where(p => p.PassType == PassType.DayPass).Select(p => new OfferOptionItemDto(p.PassPlanId, p.IsActive ? p.PlanName : p.PlanName + " (inactive)", p.Price)).ToList(),
			plans.Where(p => p.PassType != PassType.DayPass).Select(p => new OfferOptionItemDto(p.PassPlanId, p.IsActive ? p.PlanName : p.PlanName + " (inactive)", p.Price)).ToList());
	}

	// ---- create / update -------------------------------------------------------------------------

	public async Task<OfferDetailDto> CreateAsync(SaveOfferRequest request, CancellationToken ct = default)
	{
		Clean c = await ValidateAsync(request, null, ct);
		var o = new Offer();
		Apply(o, c);
		db.Set<Offer>().Add(o);
		await db.SaveChangesAsync(ct);
		await SaveScopeAsync(o.OfferId, c, ct);
		logger.LogInformation("Offer {OfferId} {Name} created", o.OfferId, o.OfferName);
		return await DetailAsync(o, ct);
	}

	public async Task<OfferDetailDto> UpdateAsync(int offerId, SaveOfferRequest request, CancellationToken ct = default)
	{
		Offer o = await db.Set<Offer>().FirstOrDefaultAsync(x => x.OfferId == offerId, ct) ?? throw new NotFoundException("Offer", offerId);
		Clean c = await ValidateAsync(request, o, ct);
		Apply(o, c);
		await db.SaveChangesAsync(ct);
		await SaveScopeAsync(o.OfferId, c, ct);
		return await DetailAsync(o, ct);
	}

	public async Task<OfferDetailDto> SetActiveAsync(int offerId, bool active, CancellationToken ct = default)
	{
		Offer o = await db.Set<Offer>().FirstOrDefaultAsync(x => x.OfferId == offerId, ct) ?? throw new NotFoundException("Offer", offerId);
		if (active && o.EndDate < engine.Today)
		{
			throw new BusinessRuleException($"This offer ended on {o.EndDate:dd MMM yyyy}. Edit it and move the end date first.");
		}
		if (active && !await db.Set<OfferModuleLink>().AnyAsync(l => l.OfferId == offerId, ct))
		{
			throw new BusinessRuleException("Choose at least one module (My Bookings, Passes or Membership) before activating.");
		}
		o.IsActive = active;
		await db.SaveChangesAsync(ct);
		return await DetailAsync(o, ct);
	}

	public async Task DeleteAsync(int offerId, CancellationToken ct = default)
	{
		Offer o = await db.Set<Offer>().FirstOrDefaultAsync(x => x.OfferId == offerId, ct) ?? throw new NotFoundException("Offer", offerId);
		if (await InUseAsync(offerId, ct))
		{
			throw new BusinessRuleException("Customers have used this offer, so it is kept for the record. Deactivate it instead.", "OFFER_IN_USE");
		}
		// Soft delete (the audit interceptor turns Remove into IsDeleted): the code is freed for reuse.
		o.IsActive = false;
		db.Set<Offer>().Remove(o);
		await db.SaveChangesAsync(ct);
		logger.LogInformation("Offer {OfferId} {Name} deleted", o.OfferId, o.OfferName);
	}

	private sealed record Clean(string Name, string Title, string? Description, string? Terms, string? Code, bool ShowInPortal, OfferDiscountType Type, decimal Value, decimal? Max, decimal? Min,
		List<OfferModule> Modules, List<int> Types, List<int> PassPlans, List<int> MemberPlans, DateOnly Start, DateOnly End, bool Active, bool Combine, int Sort, int? PerCustomer, int? Total);

	private static void Apply(Offer o, Clean c)
	{
		o.OfferName = c.Name;
		o.Title = c.Title;
		o.Description = c.Description;
		o.Terms = c.Terms;
		o.Code = c.Code;
		o.ShowInPortal = c.ShowInPortal;
		o.DiscountType = c.Type;
		o.DiscountValue = c.Value;
		o.MaxDiscountAmount = c.Max;
		o.MinAmount = c.Min;
		o.StartDate = c.Start;
		o.EndDate = c.End;
		o.IsActive = c.Active;
		o.CanCombine = c.Combine;
		o.SortOrder = c.Sort;
		o.UsesPerCustomer = c.PerCustomer;
		o.TotalUses = c.Total;
	}

	private async Task SaveScopeAsync(int offerId, Clean c, CancellationToken ct)
	{
		db.Set<OfferModuleLink>().RemoveRange(await db.Set<OfferModuleLink>().Where(l => l.OfferId == offerId).ToListAsync(ct));
		db.Set<OfferScope>().RemoveRange(await db.Set<OfferScope>().Where(s => s.OfferId == offerId).ToListAsync(ct));
		await db.SaveChangesAsync(ct);
		foreach (OfferModule m in c.Modules)
		{
			db.Set<OfferModuleLink>().Add(new OfferModuleLink { OfferId = offerId, Module = m });
			List<int> refs = m == OfferModule.Booking ? c.Types : m == OfferModule.Pass ? c.PassPlans : c.MemberPlans;
			foreach (int id in refs)
			{
				db.Set<OfferScope>().Add(new OfferScope { OfferId = offerId, Module = m, RefId = id });
			}
		}
		await db.SaveChangesAsync(ct);
	}

	private async Task<Clean> ValidateAsync(SaveOfferRequest r, Offer? existing, CancellationToken ct)
	{
		var errors = new List<ApiError>();
		void Err(string field, string message) => errors.Add(new ApiError("VALIDATION_ERROR", message, field));
		static string? T(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

		string? name = T(r.OfferName);
		string? title = T(r.Title);
		string? description = T(r.Description);
		string? terms = T(r.Terms);
		string? code = T(r.Code)?.ToUpperInvariant();
		if (name == null) Err("offerName", "Give the offer a name.");
		else if (name.Length > 120) Err("offerName", "Keep the name under 120 characters.");
		if (title == null) Err("title", "Give the offer a title customers will see.");
		else if (title.Length > 150) Err("title", "Keep the title under 150 characters.");
		if (description?.Length > 1000) Err("description", "Keep the description under 1,000 characters.");
		if (terms?.Length > 4000) Err("terms", "Keep the terms under 4,000 characters.");
		if (code != null)
		{
			if (!CodePattern.IsMatch(code))
			{
				Err("code", "Use 3 to 30 letters, digits, - or _ for the code.");
			}
			else
			{
				int self = existing?.OfferId ?? 0;
				if (await db.Set<Offer>().AnyAsync(o => o.Code == code && o.OfferId != self, ct))
				{
					Err("code", $"Another offer already uses the code {code}.");
				}
			}
		}
		if (!r.ShowInPortal && code == null) Err("code", "An offer hidden from the portal needs a code, or customers cannot use it.");

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
		if (modules.Count == 0) Err("modules", "Choose where the offer applies: My Bookings, Passes and/or Membership.");
		List<int> types = modules.Contains(OfferModule.Booking) ? (r.WorkspaceTypeIds ?? Array.Empty<int>()).Distinct().ToList() : new List<int>();
		List<int> passPlans = modules.Contains(OfferModule.Pass) ? (r.PassPlanIds ?? Array.Empty<int>()).Distinct().ToList() : new List<int>();
		List<int> memberPlans = modules.Contains(OfferModule.Membership) ? (r.MembershipPlanIds ?? Array.Empty<int>()).Distinct().ToList() : new List<int>();
		if (types.Count > 0 && await db.WorkspaceTypes.CountAsync(t => types.Contains(t.WorkspaceTypeId), ct) != types.Count) Err("workspaceTypeIds", "One of the workspace types does not exist.");
		if (passPlans.Count > 0 && await db.PassPlans.CountAsync(p => passPlans.Contains(p.PassPlanId) && p.PassType == PassType.DayPass, ct) != passPlans.Count) Err("passPlanIds", "Pick day-pass plans for Passes.");
		if (memberPlans.Count > 0 && await db.PassPlans.CountAsync(p => memberPlans.Contains(p.PassPlanId) && p.PassType != PassType.DayPass, ct) != memberPlans.Count) Err("membershipPlanIds", "Pick membership plans for Membership.");

		DateOnly today = engine.Today;
		if (r.StartDate == null) Err("startDate", "Choose the start date.");
		if (r.EndDate == null) Err("endDate", "Choose the end date.");
		if (r.StartDate is DateOnly s && r.EndDate is DateOnly e)
		{
			if (e < s) Err("endDate", "The end date must be on or after the start date.");
			else if (e < today && (existing == null || existing.EndDate != e)) Err("endDate", "The end date is in the past.");
		}
		if (r.SortOrder < 0 || r.SortOrder > 9999) Err("sortOrder", "Use a priority from 0 to 9999 (lower shows first).");
		if (r.UsesPerCustomer is int per && (per < 1 || per > 100_000)) Err("usesPerCustomer", "Leave it empty for no limit, or use 1 or more.");
		if (r.TotalUses is int tot && (tot < 1 || tot > 10_000_000)) Err("totalUses", "Leave it empty for no limit, or use 1 or more.");
		if (errors.Count > 0)
		{
			throw new AppValidationException(errors);
		}
		return new Clean(name!, title!, description, terms, code, r.ShowInPortal, type, r.DiscountValue, max, min, modules, types, passPlans, memberPlans,
			r.StartDate!.Value, r.EndDate!.Value, r.IsActive && r.EndDate!.Value >= today, r.CanCombine, r.SortOrder, r.UsesPerCustomer, r.TotalUses);
	}

	// ---- image -----------------------------------------------------------------------------------

	public async Task<OfferDetailDto> SetImageAsync(int offerId, string fileName, string? contentType, byte[] data, CancellationToken ct = default)
	{
		Offer o = await db.Set<Offer>().FirstOrDefaultAsync(x => x.OfferId == offerId, ct) ?? throw new NotFoundException("Offer", offerId);
		if (data.Length == 0) throw new BusinessRuleException("The file is empty.");
		if (data.Length > MaxImageBytes) throw new BusinessRuleException("Use a picture under 2 MB.");
		string ext = Path.GetExtension(fileName ?? "").ToLowerInvariant();
		string? type = ext switch { ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", ".webp" => "image/webp", _ => null };
		if (type == null || !FileTypeSniffer.Matches(ext, data))
		{
			throw new BusinessRuleException("Use a PNG, JPG or WebP picture.");
		}
		OfferImage? img = await db.Set<OfferImage>().FirstOrDefaultAsync(i => i.OfferId == offerId, ct);
		if (img == null)
		{
			img = new OfferImage { OfferId = offerId };
			db.Set<OfferImage>().Add(img);
		}
		img.ContentType = type;
		img.Data = data;
		img.UpdatedUtc = clock.UtcNow;
		o.HasImage = true;
		o.ImageVersion++;
		await db.SaveChangesAsync(ct);
		return await DetailAsync(o, ct);
	}

	public async Task<OfferDetailDto> RemoveImageAsync(int offerId, CancellationToken ct = default)
	{
		Offer o = await db.Set<Offer>().FirstOrDefaultAsync(x => x.OfferId == offerId, ct) ?? throw new NotFoundException("Offer", offerId);
		OfferImage? img = await db.Set<OfferImage>().FirstOrDefaultAsync(i => i.OfferId == offerId, ct);
		if (img != null)
		{
			db.Set<OfferImage>().Remove(img);
		}
		o.HasImage = false;
		o.ImageVersion++;
		await db.SaveChangesAsync(ct);
		return await DetailAsync(o, ct);
	}

	// ---- monitoring ------------------------------------------------------------------------------

	public async Task<List<OfferRedemptionRowDto>> RedemptionsAsync(int offerId, CancellationToken ct = default)
	{
		if (!await db.Set<Offer>().AnyAsync(o => o.OfferId == offerId, ct))
		{
			throw new NotFoundException("Offer", offerId);
		}
		DateTime cutoff = clock.UtcNow.AddMinutes(-OfferEngine.OrderHoldMinutes);
		var rows = await (from r in db.Set<OfferRedemption>().AsNoTracking()
			where r.OfferId == offerId
			join u in db.Users.IgnoreQueryFilters() on r.CustomerId equals u.UserId
			orderby r.RedemptionId descending
			select new
			{
				r,
				u.FullName,
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
				state = x.Booking == null ? "Released"
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
			return new OfferRedemptionRowDto(x.r.RedemptionId, x.r.CreatedUtc, x.r.CustomerId, x.FullName, (byte)x.r.Module, reference, x.r.BaseAmount, x.r.DiscountAmount, state);
		}).ToList();
	}
}
