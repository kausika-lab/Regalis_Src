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
    Housekeeping = 12,
    /// <summary>Profile, staff-account and password activity, raised to the admin feed.</summary>
    Account = 13
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

/// <summary>Maintenance checklist lifecycle. Pending → InProgress → Done (waiting for a
/// supervisor) → Verified. Rejecting a Done task sets Rework; the housekeeper starts it
/// again (InProgress). Overdue is set by the sweeper when the due time passes on a Pending
/// or InProgress task. Cancelled replaces deletion once a task has moved past Pending.</summary>
public enum MaintenanceWorkStatus : byte
{
    Pending = 1,
    InProgress = 2,
    Done = 3,
    Verified = 4,
    Rework = 5,
    Overdue = 6,
    Cancelled = 7
}

public enum MaintenancePriority : byte
{
    Low = 1,
    Medium = 2,
    High = 3
}

public enum HousekeeperShift : byte
{
    Morning = 1,
    Evening = 2,
    Night = 3
}

/// <summary>The type chips on the admin Notifications page. Deliberately broad: a new
/// event (KYC uploaded, KYC verified) is a new EventCode under an existing type, so it
/// needs no migration. Customer covers registration today and KYC later.</summary>
public enum AdminNotificationType : byte
{
    Customer = 1,
    Booking = 2,
    Payment = 3,
    Wallet = 4,
    Support = 5,
    AccountChange = 6,
    Maintenance = 7,
    Purchase = 8
}

/// <summary>ActionNeeded rows count towards "Needs action" until the record they are
/// about is resolved (ticket closed, invoice paid, task verified).</summary>
public enum AdminNotificationSeverity : byte
{
    Info = 1,
    ActionNeeded = 2
}

public enum AnnouncementStatus : byte
{
    Draft = 1,
    Scheduled = 2,
    Sent = 3
}

/// <summary>Companies / individuals are told apart by whether the profile has a company name.</summary>
public enum AnnouncementAudience : byte
{
    AllCustomers = 1,
    CompaniesOnly = 2,
    IndividualMembers = 3,
    OneCustomer = 4
}
