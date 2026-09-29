using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_PurchaseOrder — anything the company buys (document §4).
/// Tax here is the supplier's GST as printed on their quote, entered by the buyer;
/// it is NOT sourced from Tax.* in MST_AppSetting, which models our OUTPUT GST.</summary>
public class PurchaseOrder : AuditableEntity
{
    public long PurchaseOrderId { get; set; }
    /// <summary>REG/PO/{FY}/{00001} — gap-free, from MST_NumberSequence.</summary>
    public string PoNumber { get; set; } = string.Empty;
    public DateOnly PoDate { get; set; }
    public int SupplierId { get; set; }
    public int LocationId { get; set; }
    public DateOnly? ExpectedDeliveryDate { get; set; }
    /// <summary>Supplier quotation, indent or request number.</summary>
    public string? ReferenceNumber { get; set; }
    /// <summary>One of Purchase.ShipmentPreferences ("Supplier delivery", "Pickup"…).</summary>
    public string? ShipmentPreference { get; set; }
    /// <summary>Reverse charge: the supplier does not charge GST; Regalis pays it to the
    /// government. The GST is still computed and printed, but not added to what the
    /// supplier is paid.</summary>
    public bool IsReverseCharge { get; set; }
    /// <summary>The delivery address as printed, snapshotted from the location (or
    /// typed) so later edits to the location never rewrite an issued PO.</summary>
    public string? DeliveryAddress { get; set; }
    /// <summary>Supplier state ≠ delivery location state: IGST instead of CGST + SGST.</summary>
    public bool IsInterState { get; set; }

    /// <summary>Σ quantity × rate.</summary>
    public decimal SubTotal { get; set; }
    public PurchaseOrderDiscountType? DiscountType { get; set; }
    /// <summary>The percent or rupee figure as entered; DiscountAmount is what it came to.</summary>
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    /// <summary>CGST + SGST + IGST.</summary>
    public decimal TaxAmount { get; set; }
    /// <summary>TDS Regalis deducts from the supplier's payment, on the pre-GST value.</summary>
    public string? TdsCode { get; set; }
    public string? TdsLabel { get; set; }
    public decimal TdsPercent { get; set; }
    public decimal TdsAmount { get; set; }
    public string? AdjustmentLabel { get; set; }
    /// <summary>Round off, freight, other — may be negative.</summary>
    public decimal AdjustmentAmount { get; set; }
    /// <summary>Payable to the supplier: taxable + GST (unless reverse charge) − TDS + adjustment.</summary>
    public decimal TotalAmount { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateTime? SubmittedUtc { get; set; }
    public DateTime? ApprovedUtc { get; set; }
    public long? ApprovedByUserId { get; set; }
    public DateTime? SentUtc { get; set; }
    public DateTime? CancelledUtc { get; set; }
    public string? CancellationReason { get; set; }

    /// <summary>Notes for the supplier, printed on the PO.</summary>
    public string? Remarks { get; set; }
    /// <summary>Copied from Purchase.TermsAndConditions on create, then this PO's own.</summary>
    public string? TermsAndConditions { get; set; }
    /// <summary>Legacy single document path; new uploads go to <see cref="Attachments"/>.</summary>
    public string? AttachmentPath { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
    public ICollection<PurchaseOrderAttachment> Attachments { get; set; } = new List<PurchaseOrderAttachment>();
    public ICollection<PurchaseOrderStatusHistory> StatusHistory { get; set; } = new List<PurchaseOrderStatusHistory>();
}
