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
    MonthlyPass = 2
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
    Cancelled = 7
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
