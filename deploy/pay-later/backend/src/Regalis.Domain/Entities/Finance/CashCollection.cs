using System;
using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_CashCollection — money taken at the front desk against a pay-later
/// booking: cash, UPI or card. One row per receipt. Never deleted or edited: a mistake
/// is voided (the row stays, with who, when and why) and the totals it moved are
/// reversed. A negative Amount is money handed back.</summary>
public class CashCollection : AuditableEntity
{
    public long CashCollectionId { get; set; }

    /// <summary>REG/RCPT/2026-27/000123 — gap-free, from MST_NumberSequence.</summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    public long BookingId { get; set; }
    public long? InvoiceId { get; set; }

    /// <summary>The PAY- payment this receipt created.</summary>
    public long? PaymentId { get; set; }

    public long CustomerId { get; set; }
    public int LocationId { get; set; }

    /// <summary>Cash, Upi, CreditCard or DebitCard.</summary>
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;

    public decimal Amount { get; set; }

    /// <summary>Cash only: what the customer handed over, and the change given back.</summary>
    public decimal? AmountTendered { get; set; }
    public decimal? ChangeReturned { get; set; }

    /// <summary>UPI transaction id or card slip number; required unless cash.</summary>
    public string? ReferenceNumber { get; set; }

    public long CollectedByUserId { get; set; }
    public DateTime CollectedUtc { get; set; }

    /// <summary>The collector's register session (drawer + day) it belongs to.</summary>
    public long? CashRegisterSessionId { get; set; }

    public CashCollectionStatus Status { get; set; } = CashCollectionStatus.Recorded;
    public long? VoidedByUserId { get; set; }
    public DateTime? VoidedUtc { get; set; }
    public string? VoidReason { get; set; }

    public string? Notes { get; set; }

    public Booking.Booking Booking { get; set; } = null!;
    public CashRegisterSession? CashRegisterSession { get; set; }
}
