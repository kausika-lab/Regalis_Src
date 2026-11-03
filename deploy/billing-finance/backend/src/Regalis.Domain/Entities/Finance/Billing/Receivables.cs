using System;
using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Finance.Billing;

/// <summary>How a customer paid an invoice outside the gateway (build doc §3).</summary>
public enum InvoicePaymentMode : byte
{
    Cash = 1,
    Upi = 2,
    BankTransfer = 3,
    Cheque = 4,
    Card = 5,
    Wallet = 6,

    /// <summary>Settled by TDS the customer deducted (written by bank matching).</summary>
    TdsCredit = 7,

    /// <summary>A short payment accepted as settled (bank matching, BANK.WRITEOFF).</summary>
    WriteOff = 8
}

/// <summary>TRN_InvoicePayment - one receipt against one invoice, taken at the desk, by
/// bank, or from the customer's wallet. Each row also writes a PAY- payment (so the
/// Payments page, the receipt PDF and bank matching keep working) and moves the invoice's
/// paid amount in the same transaction. Never edited: a mistake is reversed, which writes
/// a negative row pointing at the original and leaves both on the record.</summary>
public class InvoicePayment : AuditableEntity
{
    public long InvoicePaymentId { get; set; }

    /// <summary>REG/RCP/2026-27/00042 - from MST_NumberSequence (key InvoiceReceipt).</summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    public long InvoiceId { get; set; }
    public long CustomerId { get; set; }

    /// <summary>The PAY- payment this receipt created (null on a reversal row).</summary>
    public long? PaymentId { get; set; }

    /// <summary>Negative on a reversal row.</summary>
    public decimal Amount { get; set; }

    public DateOnly PaidOn { get; set; }
    public InvoicePaymentMode Mode { get; set; }

    /// <summary>UTR, UPI id, cheque number or card slip. Null only for cash and wallet.</summary>
    public string? Reference { get; set; }

    public int? BankAccountId { get; set; }
    public string? Note { get; set; }
    public long? WalletTransactionId { get; set; }
    public long? CashCollectionId { get; set; }

    /// <summary>Set when this receipt came from matching a bank line.</summary>
    public long? BankTransactionId { get; set; }

    public bool IsReversed { get; set; }
    public long? ReversedByUserId { get; set; }
    public DateTime? ReversedUtc { get; set; }
    public string? ReverseReason { get; set; }

    /// <summary>On a reversal row: the receipt it reverses.</summary>
    public long? ReversalOfId { get; set; }

    public long RecordedByUserId { get; set; }
    public DateTime RecordedUtc { get; set; }
}

/// <summary>MST_CustomerTaxProfile - the customer's TAN (needed only where they deduct
/// TDS) and the "do not chase" switch for an open dispute. One row per customer, created
/// on first save.</summary>
public class CustomerTaxProfile : AuditableEntity
{
    public long CustomerId { get; set; }
    public string? Tan { get; set; }
    public bool DoNotChase { get; set; }
    public string? DoNotChaseReason { get; set; }
}
