using Regalis.Domain.Common;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Booking;

/// <summary>TRN_Booking — the core occupancy record. Intervals are half-open
/// [StartUtc, EndUtc): a booking ending at 12:00 and one starting at 12:00 do not conflict.
/// Bookings are never deleted (Invariant 3) — cancellation is a status change plus history.</summary>
public class Booking : AuditableEntity
{
    public long BookingId { get; set; }
    /// <summary>REG-000123 — gap-free, from MST_NumberSequence.</summary>
    public string BookingNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public int LocationId { get; set; }
    public int WorkspaceId { get; set; }
    /// <summary>Denormalised for reporting/filtering.</summary>
    public int WorkspaceTypeId { get; set; }
    /// <summary>Local booking date, for grouping.</summary>
    public DateOnly BookingDate { get; set; }
    /// <summary>Inclusive.</summary>
    public DateTime StartUtc { get; set; }
    /// <summary>Exclusive.</summary>
    public DateTime EndUtc { get; set; }
    public int DurationMinutes { get; set; }
    public decimal BaseAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public BookingStatus Status { get; set; } = BookingStatus.Pending;
    public BookingPaymentStatus PaymentStatus { get; set; } = BookingPaymentStatus.Pending;
    public BookingSource BookingSource { get; set; } = BookingSource.Customer;
    /// <summary>Set when a Day/Monthly pass covers the booking.</summary>
    public long? CustomerPassId { get; set; }
    /// <summary>Who actually created it (differs from CustomerId for admin bookings).</summary>
    public long CreatedByUserId { get; set; }
    public DateTime? CheckInUtc { get; set; }
    public DateTime? CheckOutUtc { get; set; }
    public DateTime? CancelledUtc { get; set; }
    public long? CancelledByUserId { get; set; }
    public string? CancellationReason { get; set; }
    public decimal? RefundAmount { get; set; }
    public string? Notes { get; set; }
    /// <summary>Set for a desk or cabin booked by the month (1, 3, 6 or 12): the booking
    /// holds the workspace for the whole term, is priced at PricePerMonth x months, and a
    /// daily check-out does not complete it. Null for an ordinary hourly booking.</summary>
    public byte? BillingMonths { get; set; }
    public byte[] RowVersion { get; set; } = [];

    public User Customer { get; set; } = null!;
    public Location Location { get; set; } = null!;
    public Workspace Workspace { get; set; } = null!;
    public WorkspaceType WorkspaceType { get; set; } = null!;
    public ICollection<BookingStatusHistory> StatusHistory { get; set; } = new List<BookingStatusHistory>();

    /// <summary>Statuses that occupy the slot: Pending (held), Confirmed, CheckedIn.</summary>
    public static readonly BookingStatus[] OccupancyStatuses =
        [BookingStatus.Pending, BookingStatus.Confirmed, BookingStatus.CheckedIn];
}
