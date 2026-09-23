using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_Quotation — the priced offer that precedes an invoice (document §6).
/// This is where "Draft" lives: a quotation is freely editable while Draft, and the
/// invoice it becomes is immutable from the moment it is issued. Customer, company and
/// tax details are snapshotted when the quotation is sent, so the customer's copy and
/// the resulting invoice agree even if settings or the profile change afterwards.
///
/// A sent quotation is never edited again. New prices or terms go out as a revision
/// (…-R1), which is a fresh row pointing back through <see cref="RevisionOfQuotationId"/>
/// while the one the customer already holds is marked Revised — so the document they
/// approve is always exactly the document they were sent.</summary>
public class Quotation : AuditableEntity
{
    public long QuotationId { get; set; }
    /// <summary>REG/QTN/{FY}/{00001} — gap-free, from MST_NumberSequence. A revision
    /// keeps the original number and appends -R1, -R2 … so the two read as one thread.</summary>
    public string QuotationNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public int LocationId { get; set; }
    public DateOnly QuotationDate { get; set; }
    /// <summary>Validity date from the document; the expiry sweeper reads this.</summary>
    public DateOnly ValidUntil { get; set; }

    /// <summary>The quotation this one replaces. Null on an original (document §5.1).</summary>
    public long? RevisionOfQuotationId { get; set; }
    /// <summary>0 on an original, 1 for -R1, 2 for -R2 …</summary>
    public int RevisionNumber { get; set; }

    /// <summary>Customer PO or enquiry number (document §2.2).</summary>
    public string? ReferenceNumber { get; set; }
    /// <summary>Printed at the top of the PDF and used as the email subject (§2.2).</summary>
    public string? Subject { get; set; }
    /// <summary>The admin credited in the sales reports; defaults to the creator.</summary>
    public long? SalespersonId { get; set; }

    public decimal SubTotal { get; set; }
    /// <summary>Whole-quotation discount as the admin entered it (§2.2); the rupee
    /// value it resolves to is <see cref="DiscountAmount"/>.</summary>
    public QuotationDiscountType DiscountType { get; set; } = QuotationDiscountType.Percentage;
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }

    /// <summary>Intra-state splits CGST + SGST, inter-state charges IGST (§2.3).</summary>
    public QuotationTaxMode TaxMode { get; set; } = QuotationTaxMode.IntraState;
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public decimal IgstPercent { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }

    /// <summary>Optional deduction for corporate customers, e.g. 194-I Rent 10%
    /// (document §2.3). Computed on the taxable value and shown as a minus.</summary>
    public string? TdsCode { get; set; }
    public string? TdsLabel { get; set; }
    public decimal TdsPercent { get; set; }
    public decimal TdsAmount { get; set; }

    /// <summary>Free-form correction with its own label, positive or negative (§2.2).</summary>
    public string? AdjustmentLabel { get; set; }
    public decimal AdjustmentAmount { get; set; }
    /// <summary>Difference to the nearest rupee; can be switched off in settings (§2.3).</summary>
    public decimal RoundOffAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    // Lifecycle stamps — the document requires customer identity, date and time on
    // the approve/reject decision (§8).
    public DateTime? SentUtc { get; set; }
    public DateTime? ViewedUtc { get; set; }
    public DateTime? RespondedUtc { get; set; }
    public long? RespondedByUserId { get; set; }
    public string? RejectionReason { get; set; }
    /// <summary>Which of the fixed reasons the customer picked (document §3.7).</summary>
    public string? RejectionCode { get; set; }
    public DateTime? ExpiredUtc { get; set; }

    /// <summary>The caller's address at the moment of the decision — the document asks
    /// that an approval be attributable, not merely timestamped (§5.3).</summary>
    public string? DecisionIpAddress { get; set; }
    /// <summary>True when an admin recorded the decision for a customer who confirmed
    /// by phone or in person (document §3.6).</summary>
    public bool DecisionOnBehalf { get; set; }
    public QuotationDecisionMode? DecisionMode { get; set; }
    public string? DecisionConfirmedBy { get; set; }
    public string? DecisionNote { get; set; }

    /// <summary>Set when the approved quotation is converted; a quotation yields at
    /// most one invoice today (see the open question on recurring billing).</summary>
    public long? InvoiceId { get; set; }

    /// <summary>Raise a security-deposit invoice automatically on approval (§3.4).
    /// Off by default; the amount is either N months of the quoted rent or a flat sum.</summary>
    public bool AdvanceInvoiceEnabled { get; set; }
    public QuotationAdvanceType AdvanceType { get; set; } = QuotationAdvanceType.Months;
    public decimal AdvanceValue { get; set; }

    // Snapshotted when sent — mirrors TRN_Invoice.
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerMobile { get; set; }
    public string? CustomerGstin { get; set; }
    public string? CustomerCompanyName { get; set; }
    public string? BillingAddress { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyGstin { get; set; }
    public string? CompanyAddress { get; set; }
    public string? PlaceOfSupply { get; set; }
    /// <summary>GST state code, "29" for Karnataka. Decides the tax mode (§2.2).</summary>
    public string? PlaceOfSupplyStateCode { get; set; }

    public string? TermsAndConditions { get; set; }
    /// <summary>Customer-facing notes, printed on the PDF.</summary>
    public string? Remarks { get; set; }
    /// <summary>Admin-only; never shown to the customer and never printed (§3.6).</summary>
    public string? InternalNote { get; set; }
    /// <summary>Written once on send and never regenerated, so the customer's copy
    /// never changes underneath them (document §3.8).</summary>
    public string? PdfPath { get; set; }

    public ICollection<QuotationLine> Lines { get; set; } = new List<QuotationLine>();
    public ICollection<QuotationStatusHistory> StatusHistory { get; set; } = new List<QuotationStatusHistory>();
    public ICollection<QuotationAttachment> Attachments { get; set; } = new List<QuotationAttachment>();
    public ICollection<QuotationEvent> Events { get; set; } = new List<QuotationEvent>();
}
