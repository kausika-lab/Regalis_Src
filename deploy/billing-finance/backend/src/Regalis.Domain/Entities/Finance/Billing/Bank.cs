using System;
using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Finance.Billing;

/// <summary>What a bank line can be matched to (doc §9.3: one screen, both directions).</summary>
public enum BankMatchDocType : byte
{
    /// <summary>Credit: settles an invoice; writes a receipt.</summary>
    Invoice = 1,

    /// <summary>Credit: an existing PAY- payment (gateway, desk, or a recorded receipt).</summary>
    CustomerPayment = 2,

    /// <summary>Debit: settles a purchase bill; writes a vendor payment.</summary>
    PurchaseBill = 3,

    /// <summary>Debit: an existing vendor payment.</summary>
    VendorPayment = 4,

    /// <summary>Debit: an approved expense.</summary>
    Expense = 5,

    /// <summary>Debit: a petty cash top-up.</summary>
    PettyCashTopUp = 6
}

/// <summary>The six reasons a difference is allowed (doc §9.2). Kept as an enum because
/// each one does something different in code; MST_DifferenceReason holds their labels.</summary>
public enum BankDifferenceReason : byte
{
    TdsDeducted = 1,
    BankCharge = 2,
    InterestEarned = 3,
    Rounding = 4,
    ShortPaymentAccepted = 5,
    AdvanceReceived = 6
}

/// <summary>TRN_BankImportBatch - one statement file imported into one account. Every line
/// it created carries the batch id; the batch can be undone while none of them is matched.</summary>
public class BankImportBatch : AuditableEntity
{
    public long BankImportBatchId { get; set; }

    /// <summary>The value written to TRN_BankTransaction.ImportBatchId.</summary>
    public string BatchCode { get; set; } = string.Empty;

    public int BankAccountId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int ErrorCount { get; set; }
    public DateOnly? FirstDate { get; set; }
    public DateOnly? LastDate { get; set; }
    public decimal? FileClosingBalance { get; set; }
    public string ColumnMappingJson { get; set; } = "{}";
    public long ImportedByUserId { get; set; }
    public DateTime ImportedUtc { get; set; }
    public bool IsUndone { get; set; }
    public long? UndoneByUserId { get; set; }
    public DateTime? UndoneUtc { get; set; }
}

/// <summary>MST_BankColumnMapping - the remembered column mapping, one row per account.</summary>
public class BankColumnMapping : AuditableEntity
{
    public int BankAccountId { get; set; }
    public string MappingJson { get; set; } = "{}";
    public string DateFormat { get; set; } = "dd/MM/yyyy";
}

/// <summary>TRN_BankMatchAllocation - one document a bank line was matched to, and how much
/// of it. Unmatching soft-deletes the rows with who, when and why.</summary>
public class BankMatchAllocation : AuditableEntity
{
    public long BankMatchAllocationId { get; set; }
    public long BankTransactionId { get; set; }
    public BankMatchDocType DocType { get; set; }
    public long DocId { get; set; }
    public string DocNumber { get; set; } = string.Empty;

    /// <summary>How much of the document this line settles.</summary>
    public decimal Amount { get; set; }

    /// <summary>Records the match created (receipts, a vendor payment) - reversed on unmatch.</summary>
    public string? CreatedRefs { get; set; }

    public string? UnmatchReason { get; set; }
}

/// <summary>TRN_BankMatchDifference - an explained gap between the documents applied and the
/// bank line. Amount is always positive; the reason decides which way it counts.</summary>
public class BankMatchDifference : AuditableEntity
{
    public long BankMatchDifferenceId { get; set; }
    public long BankTransactionId { get; set; }
    public BankDifferenceReason Reason { get; set; }

    /// <summary>Signed effect on the bank figure: bank = applied + sum(SignedAmount).</summary>
    public decimal SignedAmount { get; set; }

    public string? TdsSection { get; set; }

    /// <summary>The invoice or bill a TDS or short-payment difference belongs to.</summary>
    public BankMatchDocType? TargetDocType { get; set; }
    public long? TargetDocId { get; set; }

    public long? CreatedExpenseId { get; set; }
    public long? CreatedTdsEntryId { get; set; }
    public long? CreatedWalletTransactionId { get; set; }
    public long? CreatedVendorPaymentId { get; set; }
    public string? Note { get; set; }
}

/// <summary>MST_DifferenceReason - labels and rules of the six reasons, as rows.</summary>
public class DifferenceReason
{
    public byte DifferenceReasonId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string? Help { get; set; }
    public string? RequiredPermission { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
}
