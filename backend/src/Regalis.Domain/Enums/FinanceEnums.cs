namespace Regalis.Domain.Enums;

public enum WalletDirection : byte
{
    Credit = 1,
    Debit = 2
}

public enum WalletCategory : byte
{
    Recharge = 1,
    Booking = 2,
    Refund = 3,
    DayPass = 4,
    MonthlyPass = 5,
    ServiceOrder = 6,
    SecurityDeposit = 7,
    AdminAdjustment = 8,
    Cancellation = 9
}

public enum PaymentMethod : byte
{
    Wallet = 1,
    Upi = 2,
    CreditCard = 3,
    DebitCard = 4,
    NetBanking = 5,
    Cash = 6,
    Other = 7
}

public enum PaymentStatus : byte
{
    Pending = 1,
    Success = 2,
    Failed = 3,
    Refunded = 4,
    Cancelled = 5
}

public enum InvoiceStatus : byte
{
    Pending = 1,
    Paid = 2,
    PartiallyPaid = 3,
    Cancelled = 4,
    Refunded = 5,

    /// <summary>Issued and delivered to the customer, nothing paid yet. Appended
    /// rather than inserted so existing tinyint values keep their meaning.</summary>
    Sent = 6
}

public enum PassType : byte
{
    DayPass = 1,
    MonthlyPass = 2,
    WeeklyPass = 3,
    YearlyPass = 4
}

public enum CustomerPassStatus : byte
{
    Active = 1,
    Used = 2,
    Expired = 3,
    Cancelled = 4
}

public enum ServiceOrderStatus : byte
{
    Placed = 1,
    Confirmed = 2,
    Ready = 3,
    Delivered = 4,
    Cancelled = 5
}

/// <summary>The quotation lifecycle from document §6/§20. Draft lives here, not on
/// the invoice: an invoice exists only once issued, and is immutable from that moment.</summary>
public enum QuotationStatus : byte
{
    Draft = 1,
    Sent = 2,
    Viewed = 3,
    Approved = 4,
    Rejected = 5,
    Expired = 6,
    Cancelled = 7,
    /// <summary>Superseded by a newer revision (…-R1). A final state: the customer
    /// sees it as "Replaced", and only the newest revision can still be approved.</summary>
    Revised = 8
}

/// <summary>Whether the whole-quotation discount is a percentage of the sub total or
/// a flat rupee amount (document §2.2). The percentage is stored as typed so a later
/// price change never silently rewrites what the customer was offered.</summary>
public enum QuotationDiscountType : byte
{
    Percentage = 1,
    Amount = 2
}

/// <summary>GST split for a quotation. Karnataka (state code 29) is intra-state and
/// splits CGST + SGST; every other place of supply is inter-state and charges IGST
/// (document §2.3).</summary>
public enum QuotationTaxMode : byte
{
    IntraState = 1,
    InterState = 2
}

/// <summary>What a quotation line represents. Header is a text-only grouping row: it
/// carries no money and is skipped by every total (document §3.3).</summary>
public enum QuotationLineType : byte
{
    Service = 1,
    Custom = 2,
    Header = 3
}

/// <summary>The workspace service a line quotes (document §2.1). Kept alongside the
/// existing WorkspaceTypeId so the grouped picker can offer passes and free-text
/// lines, which have no MST_WorkspaceType row.</summary>
public enum QuotationServiceType : byte
{
    Cabin = 1,
    Desk = 2,
    MeetingRoom = 3,
    Pass = 4,
    Other = 5
}

/// <summary>How a quotation line's duration is counted (document §2.1). OneTime is a
/// flat charge — a pass or a setup fee — and never multiplies the rate.</summary>
public enum QuotationDurationUnit : byte
{
    Hour = 1,
    Day = 2,
    Month = 3,
    OneTime = 4
}

/// <summary>How the approve/reject decision reached Regalis. Portal is the customer's
/// own click; the rest are an admin recording a decision on their behalf (§3.6).</summary>
public enum QuotationDecisionMode : byte
{
    Portal = 1,
    Phone = 2,
    InPerson = 3,
    Email = 4,
    WhatsApp = 5
}

/// <summary>The security-deposit advance invoice raised on approval (document §3.4):
/// either a number of months of the quoted rent, or a flat amount.</summary>
public enum QuotationAdvanceType : byte
{
    Months = 1,
    FixedAmount = 2
}

/// <summary>Audit trail for a quotation (document §5.1). Wider than the status
/// history it sits beside: it records sends, downloads and notes, which are not
/// status changes but still have to be answerable for.</summary>
public enum QuotationEventType : byte
{
    Created = 1,
    Updated = 2,
    SentEmail = 3,
    SentWhatsApp = 4,
    Viewed = 5,
    Approved = 6,
    Rejected = 7,
    Expired = 8,
    Revised = 9,
    Cloned = 10,
    Converted = 11,
    NoteAdded = 12,
    Deleted = 13,
    Cancelled = 14,
    AttachmentAdded = 15,
    AttachmentRemoved = 16
}

/// <summary>Who caused a quotation event, for the activity timeline (§3.6).</summary>
public enum QuotationActorType : byte
{
    Admin = 1,
    Customer = 2,
    System = 3
}

/// <summary>Delivery outcome of one send attempt, shown per channel on the activity
/// timeline with a Retry when it failed (§3.6).</summary>
public enum QuotationDeliveryStatus : byte
{
    Pending = 1,
    Sent = 2,
    Delivered = 3,
    Read = 4,
    Failed = 5
}

/// <summary>Purchase order lifecycle from document §4/§20. PartiallyReceived and
/// Received are derived from the lines' ReceivedQuantity, never set by hand.</summary>
public enum PurchaseOrderStatus : byte
{
    Draft = 1,
    Sent = 2,
    PartiallyReceived = 3,
    Received = 4,
    Cancelled = 5
}

/// <summary>Direction of a bank statement line. Separate from WalletDirection —
/// that one describes the customer's wallet, this one the company's bank account.</summary>
public enum BankTransactionDirection : byte
{
    Credit = 1,
    Debit = 2
}

/// <summary>Reconciliation lifecycle from document §14. Matched means a candidate
/// system record is linked; Reconciled means a human confirmed it.</summary>
public enum ReconciliationStatus : byte
{
    Unreconciled = 1,
    Matched = 2,
    Reconciled = 3
}
