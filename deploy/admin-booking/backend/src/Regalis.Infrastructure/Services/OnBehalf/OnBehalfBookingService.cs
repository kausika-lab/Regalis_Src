using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Bookings;
using Regalis.Application.Bookings.OnBehalf;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Coupons;
using Regalis.Application.Finance;
using Regalis.Application.Offers;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services.Coupons;
using Regalis.Infrastructure.Services.Offers;

namespace Regalis.Infrastructure.Services.OnBehalf;

/// <summary>
/// admin-booking kit (27 Nov 2026). Runs the customer booking engine (BookingService,
/// PassService, OfferPortalService, CouponPortalService: the very classes behind the customer
/// "Book a Workspace" screen) with an <see cref="OnBehalfCurrentUser"/>, so a staff booking
/// gets the same validation, availability, closed-day, conflict (409), GST, monthly, Virtual
/// Office, offer and coupon rules as a customer booking. Nothing here prices, validates or
/// writes a booking itself.
///
/// What differs, by decision of 27 Nov 2026:
///  - settlement is always pay later: no payment, receipt or wallet row; the invoice stays due
///    and is paid on the Payments screen. The customer's pay-later eligibility is not checked
///    (BookingService / PassService skip PayLaterPolicy for an <see cref="IOnBehalfOfCustomer"/>);
///  - the booking's source is Admin ("Front desk") and CreatedByUserId is the staff user (set
///    in BookingService.HoldAsync). The audit interceptors keep using the real staff user.
/// Only holds made on this screen (source Admin) can be confirmed or discounted here.
/// </summary>
public partial class OnBehalfBookingService(IServiceProvider services, RegalisDbContext db, ICurrentUser currentUser, ILogger<OnBehalfBookingService> logger) : IOnBehalfBookingService
{
	/// <summary>Virtual Office plans: MEM-VIRTUAL-{n}M, as the customer screen reads them.</summary>
	[GeneratedRegex("^MEM-VIRTUAL-\\d+M$", RegexOptions.IgnoreCase)]
	private static partial Regex VirtualOfficeCode();

	/// <summary>The staff caller acting for an active customer.</summary>
	private async Task<OnBehalfCurrentUser> ActAsAsync(long customerId, CancellationToken ct)
	{
		if (!currentUser.IsAuthenticated || currentUser.UserType is null or UserType.Customer || currentUser is IOnBehalfOfCustomer)
		{
			throw new ForbiddenException("Booking for a customer is for staff users.");
		}
		var customer = await db.Users.AsNoTracking()
			.Where(u => u.UserId == customerId && u.UserType == UserType.Customer && u.Status == UserStatus.Active)
			.Select(u => new { u.UserId, u.Email, u.FullName })
			.FirstOrDefaultAsync(ct) ?? throw new BusinessRuleException("Choose an active customer to book for.", "CUSTOMER_NOT_FOUND");
		return new OnBehalfCurrentUser(currentUser, customer.UserId, customer.Email, customer.FullName);
	}

	/// <summary>A customer-engine service built for this request with the on-behalf identity
	/// (and any other replacement dependency); everything else comes from the request scope,
	/// so it shares the scope's RegalisDbContext and its audit interceptors.</summary>
	private T Engine<T>(OnBehalfCurrentUser who, params object[] more) =>
		ActivatorUtilities.CreateInstance<T>(services, new object[] { who }.Concat(more).ToArray());

	private PassService Passes(OnBehalfCurrentUser who) => Engine<PassService>(who);

	/// <summary>Every booking id belongs to the customer and was started on this screen.</summary>
	private async Task RequireScreenHoldsAsync(long customerId, IReadOnlyList<long>? bookingIds, CancellationToken ct)
	{
		long[] ids = (bookingIds ?? Array.Empty<long>()).Distinct().ToArray();
		var rows = await db.Bookings.AsNoTracking().Where(b => ids.Contains(b.BookingId))
			.Select(b => new { b.BookingId, b.BookingNumber, b.CustomerId, b.BookingSource }).ToListAsync(ct);
		foreach (long id in ids)
		{
			var b = rows.FirstOrDefault(r => r.BookingId == id);
			if (b == null || b.CustomerId != customerId)
			{
				throw new NotFoundException("Booking", id);
			}
			if (b.BookingSource != BookingSource.Admin)
			{
				throw new BusinessRuleException($"{b.BookingNumber} was not started on the admin booking screen, so it cannot be changed here.");
			}
		}
	}

	public async Task<BookingDto> CreateHoldAsync(long customerId, CreateBookingRequest request, CancellationToken ct = default)
	{
		OnBehalfCurrentUser who = await ActAsAsync(customerId, ct);
		BookingDto hold = await Engine<BookingService>(who).CreateHoldAsync(request, ct);
		logger.LogInformation("Staff {StaffId} held {BookingNumber} for customer {CustomerId}", who.StaffUserId, hold.BookingNumber, customerId);
		return hold;
	}

	public async Task<BookingDto> CreateMonthlyHoldAsync(long customerId, MonthlyBookingRequest request, CancellationToken ct = default)
	{
		OnBehalfCurrentUser who = await ActAsAsync(customerId, ct);
		BookingDto hold = await Engine<BookingService>(who).CreateMonthlyHoldAsync(request, ct);
		logger.LogInformation("Staff {StaffId} held monthly {BookingNumber} for customer {CustomerId}", who.StaffUserId, hold.BookingNumber, customerId);
		return hold;
	}

	public async Task<ConfirmBookingResponse> ConfirmAsync(long customerId, long bookingId, CancellationToken ct = default)
	{
		OnBehalfCurrentUser who = await ActAsAsync(customerId, ct);
		await RequireScreenHoldsAsync(customerId, new[] { bookingId }, ct);
		ConfirmBookingResponse done = await Engine<BookingService>(who).ConfirmAsync(bookingId, new ConfirmBookingRequest(PaymentMethods.PayLater, null, null, null), ct);
		logger.LogInformation("Staff {StaffId} confirmed {BookingNumber} for customer {CustomerId} without payment; invoice {InvoiceNumber} due", who.StaffUserId, done.Booking.BookingNumber, customerId, done.InvoiceNumber);
		return done;
	}

	public async Task<OfferPreviewDto> PreviewOffersAsync(long customerId, OfferPreviewRequest request, CancellationToken ct = default)
	{
		OnBehalfCurrentUser who = await ActAsAsync(customerId, ct);
		return await Engine<OfferPortalService>(who, (IPassService)Passes(who)).PreviewAsync(request, ct);
	}

	public async Task<ApplyBookingOffersResultDto> ApplyOffersAsync(long customerId, ApplyBookingOffersRequest request, CancellationToken ct = default)
	{
		OnBehalfCurrentUser who = await ActAsAsync(customerId, ct);
		await RequireScreenHoldsAsync(customerId, request.BookingIds, ct);
		return await Engine<OfferPortalService>(who, (IPassService)Passes(who)).ApplyToBookingsAsync(request, ct);
	}

	public async Task<CouponPreviewDto> PreviewCouponAsync(long customerId, CouponPreviewRequest request, CancellationToken ct = default)
	{
		OnBehalfCurrentUser who = await ActAsAsync(customerId, ct);
		return await Engine<CouponPortalService>(who, (IPassService)Passes(who)).PreviewAsync(request, ct);
	}

	public async Task<ApplyBookingOffersResultDto> ApplyCouponAsync(long customerId, ApplyBookingCouponRequest request, CancellationToken ct = default)
	{
		OnBehalfCurrentUser who = await ActAsAsync(customerId, ct);
		await RequireScreenHoldsAsync(customerId, request.BookingIds, ct);
		return await Engine<CouponPortalService>(who, (IPassService)Passes(who)).ApplyToBookingsAsync(request, ct);
	}

	public async Task<PurchasePassResponse> PurchaseVirtualOfficeAsync(long customerId, OnBehalfPassRequest request, CancellationToken ct = default)
	{
		OnBehalfCurrentUser who = await ActAsAsync(customerId, ct);
		string? code = await db.PassPlans.AsNoTracking().Where(p => p.PassPlanId == request.PassPlanId).Select(p => p.PlanCode).FirstOrDefaultAsync(ct);
		if (code == null || !VirtualOfficeCode().IsMatch(code))
		{
			throw new BusinessRuleException("Only Virtual Office plans can be bought on the admin booking screen.");
		}
		PurchasePassResponse done = await Passes(who).PurchaseAsync(new PurchasePassRequest(request.PassPlanId, request.ValidFrom, PaymentMethods.PayLater, null, null, null, request.OfferIds, request.CouponCode), ct);
		logger.LogInformation("Staff {StaffId} reserved Virtual Office {PassNumber} for customer {CustomerId} without payment", who.StaffUserId, done.Pass.PassNumber, customerId);
		return done;
	}
}
