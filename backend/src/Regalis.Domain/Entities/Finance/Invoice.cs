using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_Invoice. Customer and company details are snapshotted at issue time
/// so a later profile edit never rewrites history.</summary>
public class Invoice : AuditableEntity
{
    public long InvoiceId { get; set; }
    /// <summary>REG/INV/{FY}/{00001} — gap-free, from MST_NumberSequence.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public long? BookingId { get; set; }
    public long? CustomerPassId { get; set; }
    public long? ServiceOrderId { get; set; }
    /// <summary>Set when this invoice was generated from an approved quotation
    /// (document §9, rule 5). Null for booking, pass and service-order invoices.</summary>
    public long? QuotationId { get; set; }
    public int LocationId { get; set; }
    public DateOnly InvoiceDate { get; set; }
    public DateOnly DueDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CgstPercent { get; set; }
    public decimal SgstPercent { get; set; }
    public decimal IgstPercent { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    /// <summary>Carried from the quotation when one is converted (document §4.4), so
    /// the bill still adds up: taxable + GST − TDS + adjustment + round off = total.
    /// Zero on an invoice raised directly, which is every invoice raised before
    /// quotations learned about TDS.</summary>
    public string? TdsLabel { get; set; }
    public decimal TdsPercent { get; set; }
    public decimal TdsAmount { get; set; }
    public string? AdjustmentLabel { get; set; }
    public decimal AdjustmentAmount { get; set; }
    public decimal RoundOffAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    /// <summary>Computed persisted column: TotalAmount − PaidAmount.</summary>
    public decimal OutstandingAmount { get; private set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Pending;
    public long? PaymentId { get; set; }
    public DateTime? PaidUtc { get; set; }

    // Snapshotted at issue time
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string? CustomerMobile { get; set; }
    public string? CustomerGstin { get; set; }
    /// <summary>The customer's company (profile CompanyName, or the quotation's), printed
    /// above the contact name under Bill To.</summary>
    public string? CustomerCompanyName { get; set; }
    public string? BillingAddress { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyGstin { get; set; }
    public string? CompanyAddress { get; set; }

    public string? PdfPath { get; set; }
    public string? Notes { get; set; }
    /// <summary>Snapshotted from the quotation at issue time, like every other
    /// customer-facing field on this row.</summary>
    public string? TermsAndConditions { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}
