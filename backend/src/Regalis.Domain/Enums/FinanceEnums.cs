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
    Refunded = 5
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
