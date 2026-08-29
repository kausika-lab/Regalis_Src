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
    [ProducesResponseType(typeof(ApiResponse<BookingDto>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateHold(CreateBookingRequest request, CancellationToken ct)
    {
        var booking = await bookingService.CreateHoldAsync(request, ct);
        return CreatedEnvelope($"/api/bookings/{booking.BookingId}", booking,
            "Slot held. Complete payment to confirm the booking.");
    }

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
}
