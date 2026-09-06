using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_Quotation — the priced offer that precedes an invoice (document §6).
/// This is where "Draft" lives: a quotation is freely editable while Draft, and the
/// invoice it becomes is immutable from the moment it is issued. Customer, company and
/// tax details are snapshotted when the quotation is sent, so the customer's copy and
/// the resulting invoice agree even if settings or the profile change afterwards.</summary>
public class Quotation : AuditableEntity
{
    public long QuotationId { get; set; }
    /// <summary>REG/QTN/{FY}/{00001} — gap-free, from MST_NumberSequence.</summary>
    public string QuotationNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public int LocationId { get; set; }
    public DateOnly QuotationDate { get; set; }
    /// <summary>Validity date from the document; the expiry sweeper reads this.</summary>
    public DateOnly ValidUntil { get; set; }

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public decimal IgstPercent { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;

    // Lifecycle stamps — the document requires customer identity, date and time on
    // the approve/reject decision (§8).
    public DateTime? SentUtc { get; set; }
    public DateTime? ViewedUtc { get; set; }
    public DateTime? RespondedUtc { get; set; }
    public long? RespondedByUserId { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime? ExpiredUtc { get; set; }

    /// <summary>Set when the approved quotation is converted; a quotation yields at
    /// most one invoice today (see the open question on recurring billing).</summary>
    public long? InvoiceId { get; set; }

    // Snapshotted when sent — mirrors TRN_Invoice.
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerMobile { get; set; }
    public string? CustomerGstin { get; set; }
    public string? BillingAddress { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyGstin { get; set; }
    public string? CompanyAddress { get; set; }
    public string? PlaceOfSupply { get; set; }

    public string? TermsAndConditions { get; set; }
    public string? Remarks { get; set; }
    public string? PdfPath { get; set; }

    public ICollection<QuotationLine> Lines { get; set; } = new List<QuotationLine>();
    public ICollection<QuotationStatusHistory> StatusHistory { get; set; } = new List<QuotationStatusHistory>();
}
