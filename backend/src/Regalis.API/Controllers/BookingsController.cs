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
public class BookingsController(IBookingService bookingService) : ApiControllerBase
{
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
}
