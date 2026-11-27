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
using Regalis.Application.Finance;
using Regalis.Application.Offers;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Entities.Offers;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.Offers;

/// <summary>Customer portal side of offers (brief §3-§7). Every amount comes from OfferEngine, so the
/// preview and the charge cannot disagree.</summary>
public class OfferPortalService(RegalisDbContext db, OfferEngine engine, ISettingService settings, IPassService passes, ICurrentUser currentUser, IDateTimeProvider clock, ILogger<OfferPortalService> logger) : IOfferPortalService
{
	public static string ImageUrl(Offer o) => o.HasImage ? $"/api/offers/{o.OfferId}/image?v={o.ImageVersion}" : null!;

	private void RequireCustomer()
	{
		if (currentUser.UserType != UserType.Customer)
		{
			throw new ForbiddenException("Offers are applied by customers in the customer portal.");
		}
	}

	private static OfferModule Module(string? value) =>
		OfferEngine.ParseModule(value) ?? throw new BusinessRuleException("Choose the module: booking, pass or membership.");

	public async Task<List<PortalOfferDto>> ListAsync(string? module, CancellationToken ct = default)
	{
		OfferModule m = Module(module);
		List<Offer> offers = await engine.Live(m).Where(o => o.ShowInPortal).AsNoTracking().OrderBy(o => o.SortOrder).ThenBy(o => o.EndDate).ThenBy(o => o.OfferId).ToListAsync(ct);
		return await ToPortalAsync(offers, ct);
	}

	private async Task<List<PortalOfferDto>> ToPortalAsync(IReadOnlyList<Offer> offers, CancellationToken ct)
	{
		int[] ids = offers.Select(o => o.OfferId).ToArray();
		OfferEngine.Scope scope = await engine.ScopeAsync(ids, ct);
		Dictionary<int, int> mine = currentUser.UserType == UserType.Customer ? await engine.UsesAsync(ids, currentUser.UserId, null, ct) : new Dictionary<int, int>();
		int[] planIds = scope.Refs.Where(kv => kv.Key.Item2 != OfferModule.Booking).SelectMany(kv => kv.Value).Distinct().ToArray();
		int[] typeIds = scope.Refs.Where(kv => kv.Key.Item2 == OfferModule.Booking).SelectMany(kv => kv.Value).Distinct().ToArray();
		Dictionary<int, string> plans = await db.PassPlans.AsNoTracking().Where(p => planIds.Contains(p.PassPlanId)).ToDictionaryAsync(p => p.PassPlanId, p => p.PlanName, ct);
		Dictionary<int, string> types = await db.WorkspaceTypes.AsNoTracking().Where(t => typeIds.Contains(t.WorkspaceTypeId)).ToDictionaryAsync(t => t.WorkspaceTypeId, t => t.TypeName, ct);
		return offers.Select(o =>
		{
			IReadOnlyList<byte> mods = scope.Modules.TryGetValue(o.OfferId, out var ms) ? ms.Select(x => (byte)x).OrderBy(x => x).ToList() : new List<byte>();
			var applies = new List<string>();
			foreach (var kv in scope.Refs.Where(kv => kv.Key.Item1 == o.OfferId))
			{
				foreach (int id in kv.Value)
				{
					string? name = kv.Key.Item2 == OfferModule.Booking ? types.GetValueOrDefault(id) : plans.GetValueOrDefault(id);
					if (name != null)
					{
						applies.Add(name);
					}
				}
			}
			int? left = o.UsesPerCustomer is int per && per > 0 ? Math.Max(0, per - mine.GetValueOrDefault(o.OfferId)) : null;
			return new PortalOfferDto(o.OfferId, o.Title, o.Description, o.Terms, o.Code, (byte)o.DiscountType, o.DiscountValue, o.MaxDiscountAmount, o.MinAmount, o.StartDate, o.EndDate, o.CanCombine, mods, applies, o.HasImage ? ImageUrl(o) : null, left);
		}).ToList();
	}

	public async Task<OfferPreviewDto> PreviewAsync(OfferPreviewRequest request, CancellationToken ct = default)
	{
		RequireCustomer();
		OfferModule m = Module(request.Module);
		long me = currentUser.UserId;
		(int? refId, List<decimal> bases) = await PurchaseAsync(m, request, ct);
		List<int> selected = (request.OfferIds ?? Array.Empty<int>()).Distinct().ToList();
		string? error = null;
		int? codeOfferId = null;
		string code = (request.Code ?? "").Trim().ToUpperInvariant();
		if (code.Length > 0)
		{
			Offer? byCode = await db.Set<Offer>().AsNoTracking().FirstOrDefaultAsync(o => o.Code == code, ct);
			if (byCode == null)
			{
				error = $"No offer matches the code {code}.";
			}
			else
			{
				codeOfferId = byCode.OfferId;
				if (!selected.Contains(byCode.OfferId))
				{
					selected.Add(byCode.OfferId);
				}
			}
		}
		// The module's listed offers, plus any picked by code, with why each can or cannot be used here.
		List<Offer> listed = await engine.Live(m).AsNoTracking().Where(o => o.ShowInPortal || selected.Contains(o.OfferId)).OrderBy(o => o.SortOrder).ThenBy(o => o.EndDate).ThenBy(o => o.OfferId).ToListAsync(ct);
		decimal baseTotal = bases.Sum();
		Dictionary<int, string?> reasons = await engine.ReasonsAsync(listed, m, refId, baseTotal, me, null, ct);
		List<PortalOfferDto> portal = await ToPortalAsync(listed, ct);
		var eligibility = portal.Select(p => new PortalOfferEligibilityDto(p, reasons[p.OfferId] == null, reasons[p.OfferId])).ToList();

		(decimal cg, decimal sg) = await engine.TaxPercentsAsync(ct);
		OfferCalculation none = OfferEngine.NoOffer(bases, cg, sg);
		if (request.Auto && selected.Count == 0 && code.Length == 0)
		{
			selected = BestSet(listed.Where(o => o.ShowInPortal && reasons[o.OfferId] == null).ToList(), bases, cg, sg);
		}
		OfferCalculation calc = none;
		if (error == null && selected.Count > 0)
		{
			try
			{
				calc = await engine.CalculateAsync(m, refId, bases, selected, me, null, ct);
			}
			catch (BusinessRuleException ex)
			{
				error = ex.Message;
				codeOfferId = null;
			}
		}
		return new OfferPreviewDto(eligibility,
			calc.Lines.Select(l => new OfferAppliedDto(l.Offer.OfferId, l.Offer.Title, l.Offer.Code, l.Discount)).ToList(),
			calc.Base, calc.Discount, calc.Taxable, cg, calc.Cgst, sg, calc.Sgst, calc.Total, none.Total,
			calc.Items.Select(i => new OfferPriceItemDto(i.Base, i.Discount, i.Taxable, i.Cgst, i.Sgst, i.Total)).ToList(),
			codeOfferId, error);
	}

	/// <summary>The eligible offers that save the customer the most on this purchase: the best single
	/// offer, or all the combinable ones together when that saves more. Ties go to display priority.</summary>
	private static List<int> BestSet(List<Offer> eligible, List<decimal> bases, decimal cg, decimal sg)
	{
		if (eligible.Count == 0)
		{
			return new List<int>();
		}
		var candidates = eligible.Select(o => new List<Offer> { o }).ToList();
		List<Offer> combinable = eligible.Where(o => o.CanCombine).ToList();
		if (combinable.Count > 1)
		{
			candidates.Add(combinable);
		}
		List<Offer> best = candidates
			.Select((set, i) => (set, i, saving: OfferEngine.Price(set, bases, cg, sg).Discount))
			.OrderByDescending(x => x.saving).ThenBy(x => x.i)
			.First().set;
		return best.Select(o => o.OfferId).ToList();
	}

	/// <summary>What is being bought: the plan / workspace type it is narrowed by, and the price
	/// before GST of each item. Plans are priced by the pass engine itself.</summary>
	private async Task<(int? RefId, List<decimal> Bases)> PurchaseAsync(OfferModule m, OfferPreviewRequest request, CancellationToken ct)
	{
		if (m == OfferModule.Booking)
		{
			List<decimal> amounts = (request.BaseAmounts ?? Array.Empty<decimal>()).ToList();
			if (amounts.Count == 0 || amounts.Count > 62 || amounts.Any(a => a < 0m || a > 100_000_000m))
			{
				throw new BusinessRuleException("Give the price of the booking before GST.");
			}
			int? typeId = null;
			if (request.WorkspaceId is int wid)
			{
				typeId = await db.Workspaces.AsNoTracking().Where(w => w.WorkspaceId == wid).Select(w => (int?)w.WorkspaceTypeId).FirstOrDefaultAsync(ct)
					?? throw new NotFoundException("Workspace", wid);
			}
			return (typeId, amounts);
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

	public async Task<ApplyBookingOffersResultDto> ApplyToBookingsAsync(ApplyBookingOffersRequest request, CancellationToken ct = default)
	{
		RequireCustomer();
		long me = currentUser.UserId;
		long[] ids = (request.BookingIds ?? Array.Empty<long>()).ToArray();
		if (ids.Length == 0 || ids.Length > 62 || ids.Distinct().Count() != ids.Length)
		{
			throw new BusinessRuleException("Choose the booking to apply the offer to.");
		}
		List<int> offerIds = (request.OfferIds ?? Array.Empty<int>()).ToList();
		int holdMinutes = await settings.GetIntAsync("Booking.PendingHoldMinutes", 10, ct);
		(decimal cg, decimal sg) = await engine.TaxPercentsAsync(ct);

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
				// admin-booking kit: a hold staff made for the customer (source Admin) takes offers too.
				if (b.Status != BookingStatus.Pending || (b.BookingSource != BookingSource.Customer && !(currentUser is IOnBehalfOfCustomer && b.BookingSource == BookingSource.Admin)))
				{
					throw new BusinessRuleException($"{b.BookingNumber} is not awaiting payment, so offers cannot be changed on it.", OfferEngine.NotEligible);
				}
				if (b.CreatedDate.AddMinutes(holdMinutes) <= now)
				{
					throw new BusinessRuleException($"The hold on {b.BookingNumber} has expired. Start a new booking.", OfferEngine.NotEligible);
				}
			}
			if (bookings.Select(b => b.WorkspaceTypeId).Distinct().Count() > 1)
			{
				throw new BusinessRuleException("Offers can be applied to one workspace booking at a time.", OfferEngine.NotEligible);
			}
			if (await db.Payments.AnyAsync(p => p.BookingId != null && ids.Contains(p.BookingId.Value) && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Success), ct))
			{
				throw new BusinessRuleException("Payment for this booking has already started, so its offers can no longer be changed.", OfferEngine.NotEligible);
			}

			List<decimal> bases = bookings.Select(b => b.BaseAmount).ToList();
			OfferCalculation calc = offerIds.Count == 0
				? OfferEngine.NoOffer(bases, cg, sg)
				: await engine.CalculateAsync(OfferModule.Booking, bookings[0].WorkspaceTypeId, bases, offerIds, me, ids, ct);

			// Replace whatever was applied before.
			List<OfferRedemption> old = await db.Set<OfferRedemption>().Where(r => r.BookingId != null && ids.Contains(r.BookingId.Value)).ToListAsync(ct);
			db.Set<OfferRedemption>().RemoveRange(old);

			Guid key = Guid.NewGuid();
			string labels = string.Join(", ", calc.Lines.Select(l => OfferEngine.Label(l.Offer)));
			for (int i = 0; i < bookings.Count; i++)
			{
				Booking b = bookings[i];
				OfferItemPrice price = calc.Items[i];
				bool changed = b.DiscountAmount != price.Discount || b.TotalAmount != price.Total;
				b.DiscountAmount = price.Discount;
				b.TaxAmount = price.Cgst + price.Sgst;
				b.TotalAmount = price.Total;
				for (int k = 0; k < calc.Lines.Count; k++)
				{
					db.Set<OfferRedemption>().Add(new OfferRedemption
					{
						OfferId = calc.Lines[k].Offer.OfferId,
						CustomerId = me,
						Module = OfferModule.Booking,
						BookingId = b.BookingId,
						PurchaseKey = key,
						BaseAmount = b.BaseAmount,
						DiscountAmount = price.PerOffer[k],
						OfferLabel = OfferEngine.Trim(OfferEngine.Label(calc.Lines[k].Offer)),
						CreatedUtc = now
					});
				}
				if (changed || old.Count > 0)
				{
					db.Set<BookingStatusHistory>().Add(new BookingStatusHistory
					{
						BookingId = b.BookingId,
						FromStatus = BookingStatus.Pending,
						ToStatus = BookingStatus.Pending,
						ChangedByUserId = me,
						ChangedUtc = now,
						Reason = OfferEngine.Trim(calc.Lines.Count == 0
							? $"Offers removed; total ₹{price.Total:N2}."
							: $"Offer applied ({labels}): -₹{price.Discount:N2} before GST; total ₹{price.Total:N2}.")
					});
				}
			}
			await db.SaveChangesAsync(ct);
			await tx.CommitAsync(ct);
			return new ApplyBookingOffersResultDto(
				calc.Lines.Select(l => new OfferAppliedDto(l.Offer.OfferId, l.Offer.Title, l.Offer.Code, l.Discount)).ToList(),
				calc.Discount, calc.Total,
				bookings.Select(b => new BookingOfferResultItemDto(b.BookingId, b.BookingNumber, b.BaseAmount, b.DiscountAmount, b.TaxAmount, b.TotalAmount)).ToList());
		});
		if (result.Applied.Count > 0)
		{
			logger.LogInformation("Customer {CustomerId} applied offers {Offers} to bookings {Bookings}: -{Discount}", me, string.Join(",", result.Applied.Select(a => a.OfferId)), string.Join(",", ids), result.DiscountAmount);
		}
		return result;
	}

	public async Task<(byte[] Data, string ContentType)?> ImageAsync(int offerId, CancellationToken ct = default)
	{
		bool exists = await db.Set<Offer>().AnyAsync(o => o.OfferId == offerId && o.HasImage, ct);
		if (!exists)
		{
			return null;
		}
		OfferImage? img = await db.Set<OfferImage>().AsNoTracking().FirstOrDefaultAsync(i => i.OfferId == offerId, ct);
		return img == null ? null : (img.Data, img.ContentType);
	}
}
