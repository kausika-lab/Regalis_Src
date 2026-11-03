using System;
using System.Collections.Generic;
using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Finance.Billing;

/// <summary>Overdue is not a status - it is a flag on anything unpaid past its due date
/// (build doc §4.5).</summary>
public enum PurchaseBillStatus : byte
{
    Draft = 1,
    AwaitingApproval = 2,
    Approved = 3,
    PartPaid = 4,
    Paid = 5,
    Rejected = 6,
    OnHold = 7,
    Cancelled = 8
}

public enum VendorPaymentMode : byte
{
    Neft = 1,
    Rtgs = 2,
    Imps = 3,
    Upi = 4,
    Cheque = 5,
    Cash = 6
}

/// <summary>TRN_PurchaseBill - what Regalis owes a supplier: the supplier's own invoice,
/// with their number, their date and their GST. Paid and balance are worked out from the
/// allocations of vendor payments, never typed. Unique on supplier + normalised bill
/// number, so the same bill cannot be entered twice even by two people at once.</summary>
public class PurchaseBill : AuditableEntity
{
    public long PurchaseBillId { get; set; }

    /// <summary>Internal number REG/PB/2026-27/00001.</summary>
    public string BillNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }

    /// <summary>The supplier's number exactly as printed.</summary>
    public string SupplierBillNo { get; set; } = string.Empty;

    /// <summary>Upper case, no spaces - the duplicate key.</summary>
    public string SupplierBillNoKey { get; set; } = string.Empty;

    public DateOnly BillDate { get; set; }
    public DateOnly DueDate { get; set; }

    /// <summary>0 = due on receipt; 7, 15, 30, 45, 60; anything else is "custom".</summary>
    public int PaymentTermsDays { get; set; }

    public long? PurchaseOrderId { get; set; }
    public int LocationId { get; set; }

    /// <summary>Two-digit GST state code of the supplier and of the location; equal means
    /// CGST + SGST, different means IGST.</summary>
    public string? SupplierStateCode { get; set; }
    public string? PlaceOfSupplyCode { get; set; }
    public bool IsInterState { get; set; }

    /// <summary>Snapshot of the supplier GSTIN at entry; input credit needs it.</summary>
    public string? SupplierGstin { get; set; }

    public decimal TaxableValue { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal BillTotal { get; set; }

    public string? TdsSection { get; set; }
    public decimal TdsRate { get; set; }

    /// <summary>Expected TDS on the taxable value. Only an estimate on the bill: the TDS
    /// payable is written when a payment is posted, never here (doc §10.3).</summary>
    public decimal TdsAmount { get; set; }

    public PurchaseBillStatus Status { get; set; } = PurchaseBillStatus.Draft;
    public long EnteredByUserId { get; set; }
    public DateTime? SubmittedUtc { get; set; }
    public long? ApprovedByUserId { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public string? ApprovalComment { get; set; }
    public string? RejectReason { get; set; }
    public string? HoldReason { get; set; }
    public string? CancelReason { get; set; }
    public string? Notes { get; set; }

    public ICollection<PurchaseBillLine> Lines { get; set; } = new List<PurchaseBillLine>();
    public ICollection<PurchaseBillAttachment> Attachments { get; set; } = new List<PurchaseBillAttachment>();
    public ICollection<PurchaseBillEvent> Events { get; set; } = new List<PurchaseBillEvent>();
}

public class PurchaseBillLine
{
    public long PurchaseBillLineId { get; set; }
    public long PurchaseBillId { get; set; }
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? HsnSac { get; set; }
    public decimal Quantity { get; set; }
    public decimal Rate { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal GstRate { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
}

public class PurchaseBillAttachment
{
    public long PurchaseBillAttachmentId { get; set; }
    public long PurchaseBillId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string StoredPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public long UploadedByUserId { get; set; }
    public DateTime UploadedUtc { get; set; }
}

/// <summary>Every event on a bill, append-only: entered, submitted, approved, rejected,
/// paid, reversed, held, cancelled, attachment added.</summary>
public class PurchaseBillEvent
{
    public long PurchaseBillEventId { get; set; }
    public long PurchaseBillId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string? Comment { get; set; }
    public long? UserId { get; set; }
    public DateTime EventUtc { get; set; }
}

/// <summary>TRN_VendorPayment - one payment to a supplier, usually settling several bills
/// less TDS. Amount is what leaves the bank (or petty cash); it equals the allocations
/// less TDS less advance adjusted plus any new advance. A payment with no allocations is
/// an advance. Reversed, never edited.</summary>
public class VendorPayment : AuditableEntity
{
    public long VendorPaymentId { get; set; }

    /// <summary>REG/VP/2026-27/00001.</summary>
    public string PaymentNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public DateOnly PaidOn { get; set; }

    /// <summary>What leaves the bank or the tin.</summary>
    public decimal Amount { get; set; }

    /// <summary>Total TDS held back across the allocations.</summary>
    public decimal TdsAmount { get; set; }

    /// <summary>Earlier advance used up by this payment.</summary>
    public decimal AdvanceAdjusted { get; set; }

    /// <summary>The part of Amount not allocated to any bill - a new advance.</summary>
    public decimal AdvanceAmount { get; set; }

    public VendorPaymentMode Mode { get; set; }
    public string? Reference { get; set; }

    /// <summary>Null when paid from petty cash.</summary>
    public int? BankAccountId { get; set; }
    public bool FromPettyCash { get; set; }

    public string? Note { get; set; }
    public bool IsReversed { get; set; }
    public long? ReversedByUserId { get; set; }
    public DateTime? ReversedUtc { get; set; }
    public string? ReverseReason { get; set; }
    public long PaidByUserId { get; set; }

    public ICollection<VendorPaymentAllocation> Allocations { get; set; } = new List<VendorPaymentAllocation>();
}

/// <summary>The join that lets one payment settle several bills. Amount is what the bill's
/// balance drops by: cash allocated + TDS on it.</summary>
public class VendorPaymentAllocation
{
    public long VendorPaymentAllocationId { get; set; }
    public long VendorPaymentId { get; set; }
    public long PurchaseBillId { get; set; }

    /// <summary>Settles this much of the bill (cash + TDS).</summary>
    public decimal Amount { get; set; }

    public decimal TdsAmount { get; set; }
}

/// <summary>MST_SupplierTaxProfile - PAN, default TDS section, payment terms and GST state
/// for a supplier (doc §19.3). PAN is five letters, four digits and a letter.</summary>
public class SupplierTaxProfile : AuditableEntity
{
    public int SupplierId { get; set; }
    public string? Pan { get; set; }
    public string? DefaultTdsSection { get; set; }
    public int? PaymentTermsDays { get; set; }
    public string? StateCode { get; set; }

    /// <summary>Individual or HUF - 194C at 1% instead of 2%.</summary>
    public bool IsIndividual { get; set; }
}
