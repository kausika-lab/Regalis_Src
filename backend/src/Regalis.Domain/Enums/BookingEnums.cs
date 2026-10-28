namespace Regalis.Domain.Enums;

public enum WorkspaceStatus : byte
{
    Available = 1,
    Occupied = 2,
    Maintenance = 3,
    Disabled = 4
}

public enum BookingStatus : byte
{
    /// <summary>Holds the slot for Booking.PendingHoldMinutes while payment completes.</summary>
    Pending = 1,
    Confirmed = 2,
    CheckedIn = 3,
    Completed = 4,
    Cancelled = 5,
    Expired = 6,
    NoShow = 7
}

public enum BookingPaymentStatus : byte
{
    Pending = 1,
    Paid = 2,
    Failed = 3,
    Refunded = 4,
    PartiallyRefunded = 5,
    /// <summary>Pay later: some cash taken, a balance still due.</summary>
    PartiallyPaid = 6
}

public enum BookingSource : byte
{
    Customer = 1,
    Admin = 2,
    Pass = 3
}

public enum BlockReason : byte
{
    Maintenance = 1,
    InternalUse = 2,
    Cleaning = 3,
    Repair = 4,
    PrivateEvent = 5,
    Housekeeping = 6,
    Other = 7
}
