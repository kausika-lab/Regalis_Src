using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Booking;

/// <summary>TRN_BookingStatusHistory — append-only. This is why bookings never need deleting.</summary>
public class BookingStatusHistory
{
    public long BookingStatusHistoryId { get; set; }
    public long BookingId { get; set; }
    public BookingStatus? FromStatus { get; set; }
    public BookingStatus ToStatus { get; set; }
    public long ChangedByUserId { get; set; }
    public DateTime ChangedUtc { get; set; }
    public string? Reason { get; set; }

    public Booking Booking { get; set; } = null!;
}
