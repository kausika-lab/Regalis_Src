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
    public string? BillingAddress { get; set; }
    public string CompanyName { get; set; } = string.Empty;
    public string? CompanyGstin { get; set; }
    public string? CompanyAddress { get; set; }

    public string? PdfPath { get; set; }
    public string? Notes { get; set; }
    public string? CancellationReason { get; set; }

    public ICollection<InvoiceLine> Lines { get; set; } = new List<InvoiceLine>();
}
