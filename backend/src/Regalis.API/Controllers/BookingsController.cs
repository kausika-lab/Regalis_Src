using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Bookings;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>The booking engine endpoints (API_PLAN §6). A 409 SLOT_UNAVAILABLE is
/// the documented, expected outcome of losing a race — the frontend re-renders
/// availability rather than showing a generic error.</summary>
[Route("api/bookings")]
public class BookingsController(
    IBookingService bookingService,
    IBookingMutationService bookingMutations) : ApiControllerBase
{
    /// <summary>Cancels a booking (status change + history — never a delete). The
    /// policy refund credits the personal wallet.</summary>
    [HttpPost("{id:long}/cancel")]
    [HasPermission(PermissionCodes.Booking.Edit)]
    [ProducesResponseType(typeof(ApiResponse<CancelBookingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Cancel(long id, CancelBookingRequest request, CancellationToken ct) =>
        OkEnvelope(await bookingMutations.CancelAsync(id, request, ct), "Booking cancelled.");

    /// <summary>Reschedules to a new start (same duration); re-runs the locked
    /// availability check.</summary>
    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.Booking.Edit)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reschedule(long id, RescheduleBookingRequest request,
        CancellationToken ct) =>
        OkEnvelope(await bookingMutations.RescheduleAsync(id, request, ct), "Booking rescheduled.");

    /// <summary>Extends the booking end; availability-checked, paid and invoiced.</summary>
    [HttpPost("{id:long}/extend")]
    [HasPermission(PermissionCodes.Booking.Edit)]
    [ProducesResponseType(typeof(ApiResponse<ExtendBookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Extend(long id, ExtendBookingRequest request, CancellationToken ct) =>
        OkEnvelope(await bookingMutations.ExtendAsync(id, request, ct), "Booking extended.");

    /// <summary>Price + GST breakdown before payment. No write.</summary>
    [HttpPost("quote")]
    [HasPermission(PermissionCodes.Booking.View)]
    [ProducesResponseType(typeof(ApiResponse<QuoteResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Quote(QuoteRequest request, CancellationToken ct) =>
        OkEnvelope(await bookingService.QuoteAsync(request, ct));

    /// <summary>Creates a Pending hold that reserves the slot while payment
    /// completes (Booking.PendingHoldMinutes, default 10).</summary>
    [HttpPost]
    [HasPermission(PermissionCodes.Booking.Create)]
    // Confirming raises a GST invoice, which needs a billing address and a GSTIN. Held
    // here rather than at confirm so the member is stopped before a slot is reserved.
    [RequireCompleteProfile]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateHold(CreateBookingRequest request, CancellationToken ct)
    {
        var booking = await bookingService.CreateHoldAsync(request, ct);
        return CreatedEnvelope($"/api/bookings/{booking.BookingId}", booking,
            "Slot held. Complete payment to confirm the booking.");
    }

    /// <summary>Price of a desk or cabin for a 1/3/6/12-month term. No write.</summary>
    [HttpPost("monthly/quote")]
    [HasPermission(PermissionCodes.Booking.View)]
    [ProducesResponseType(typeof(ApiResponse<MonthlyQuoteResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QuoteMonthly(MonthlyBookingRequest request, CancellationToken ct) =>
        OkEnvelope(await bookingService.QuoteMonthlyAsync(request, ct));

    /// <summary>Holds a desk or cabin for a whole monthly term while payment completes;
    /// confirm it with POST /api/bookings/{id}/confirm like any other booking.</summary>
    [HttpPost("monthly")]
    [HasPermission(PermissionCodes.Booking.Create)]
    [RequireCompleteProfile]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMonthlyHold(MonthlyBookingRequest request, CancellationToken ct)
    {
        var booking = await bookingService.CreateMonthlyHoldAsync(request, ct);
        return CreatedEnvelope($"/api/bookings/{booking.BookingId}", booking,
            "Workspace held for the term. Complete payment to confirm the booking.");
    }

    /// <summary>Before a hold: may the signed-in customer pay later, and for which kinds.</summary>
    [HttpGet("pay-later")]
    [HasPermission(PermissionCodes.Booking.Create)]
    [ProducesResponseType(typeof(ApiResponse<Application.Finance.PayLaterStatusDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PayLaterStatus(CancellationToken ct) =>
        OkEnvelope(await bookingService.GetPayLaterStatusAsync(ct));

    /// <summary>Whether this Pending hold may be confirmed with "paylater", and why not.</summary>
    [HttpGet("{id:long}/pay-later")]
    [HasPermission(PermissionCodes.Booking.Create)]
    [ProducesResponseType(typeof(ApiResponse<Application.Finance.PayLaterEligibilityDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> PayLaterEligibility(long id, CancellationToken ct) =>
        OkEnvelope(await bookingService.GetPayLaterEligibilityAsync(id, ct));

    /// <summary>One booking — customers see only their own rows.</summary>
    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Booking.View)]
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) =>
        OkEnvelope(await bookingService.GetBookingAsync(id, ct));

    /// <summary>The atomic confirmation (Design Invariant 4): verified payment →
    /// Confirmed booking + GST invoice + QR access token + notification.</summary>
    [HttpPost("{id:long}/confirm")]
    [HasPermission(PermissionCodes.Booking.Create)]
    // Also gated: a hold created before onboarding lapsed must not become an invoice.
    [RequireCompleteProfile]
    [ProducesResponseType(typeof(ApiResponse<ConfirmBookingResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Confirm(long id, ConfirmBookingRequest request, CancellationToken ct) =>
        OkEnvelope(await bookingService.ConfirmAsync(id, request, ct),
            "Booking confirmed. Your QR access pass and invoice are ready.");

    /// <summary>Admin books on a customer's behalf — hold + immediate settlement.</summary>
    [HttpPost("admin")]
    [HasPermission(PermissionCodes.Booking.Create)]
    [ProducesResponseType(typeof(ApiResponse<ConfirmBookingResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminCreate(AdminCreateBookingRequest request, CancellationToken ct) =>
        OkEnvelope(await bookingService.AdminCreateAsync(request, ct), "Booking created and confirmed.");

    /// <summary>The caller's bookings: ?tab=upcoming|completed|cancelled.</summary>
    [HttpGet("my")]
    [HasPermission(PermissionCodes.Booking.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BookingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy([FromQuery] string tab, [FromQuery] PagedRequest request,
        CancellationToken ct) =>
        OkEnvelope(await bookingService.GetMyBookingsAsync(tab ?? MyBookingTabs.Upcoming, request, ct));

    /// <summary>Admin list with the common filter set.</summary>
    [HttpGet]
    [HasPermission(PermissionCodes.Booking.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<BookingDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] BookingListRequest request, CancellationToken ct)
    {
        // The admin grid; customers use /bookings/my (service scoping still applies).
        if (User.FindFirst("user_type")?.Value == "1")
        {
            return OkEnvelope(await bookingService.GetMyBookingsAsync(MyBookingTabs.Upcoming, request, ct));
        }
        return OkEnvelope(await bookingService.GetBookingsAsync(request, ct));
    }

    /// <summary>Bookings and revenue per month, January to December, of <c>year</c>
    /// (default: this year), under the admin list's filters. Staff only: it totals
    /// every customer's bookings.</summary>
    [HttpGet("monthly")]
    [HasPermission(PermissionCodes.Booking.View)]
    [StaffOnly]
    [ProducesResponseType(typeof(ApiResponse<BookingMonthlyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthly([FromQuery] BookingMonthlyRequest request,
        CancellationToken ct) =>
        OkEnvelope(await bookingService.GetMonthlyAsync(request, ct));
}
