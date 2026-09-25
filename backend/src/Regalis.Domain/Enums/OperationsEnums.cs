namespace Regalis.Domain.Enums;

public enum InventoryTransactionType : byte
{
    In = 1,
    Out = 2,
    Damage = 3,
    OpeningBalance = 4,
    /// <summary>A mistake is corrected with a compensating Adjustment row —
    /// there is no update or delete path (Invariant 11).</summary>
    Adjustment = 5
}

public enum HousekeepingTaskType : byte
{
    Housekeeping = 1,
    Maintenance = 2,
    Cleaning = 3,
    Repair = 4,
    Inspection = 5
}

public enum HousekeepingStatus : byte
{
    Scheduled = 1,
    InProgress = 2,
    Completed = 3,
    Cancelled = 4,
    Missed = 5
}

public enum NotificationAudience : byte
{
    SingleUser = 1,
    AllCustomers = 2,
    AllAdmins = 3,
    ByLocation = 4,
    ByRole = 5
}

public enum NotificationCategory : byte
{
    Booking = 1,
    MeetingRoom = 2,
    CheckInOut = 3,
    Payment = 4,
    Invoice = 5,
    ServiceOrder = 6,
    Event = 7,
    SupportTicket = 8,
    Announcement = 9,
    Registration = 10,
    Pass = 11,
    Housekeeping = 12
}

public enum NotificationPriority : byte
{
    Low = 1,
    Normal = 2,
    High = 3
}

public enum TicketPriority : byte
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}

public enum TicketStatus : byte
{
    Open = 1,
    Assigned = 2,
    InProgress = 3,
    WaitingForCustomer = 4,
    Resolved = 5,
    Closed = 6,
    Reopened = 7
}

public enum TicketSenderType : byte
{
    Customer = 1,
    Admin = 2
}

/// <summary>What an item-master row is sold as. Goods carry an HSN code and stock;
/// services carry a SAC code and are kept off the stock screens.</summary>
public enum InventoryItemType : byte
{
    Goods = 1,
    Service = 2
}
