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

    public decimal SubTotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    public DateTime? SentUtc { get; set; }
    public DateTime? CancelledUtc { get; set; }
    public string? CancellationReason { get; set; }

    public string? Remarks { get; set; }
    /// <summary>Supporting document (quote, approval mail) — one path, like TicketAttachment.</summary>
    public string? AttachmentPath { get; set; }

    public Supplier Supplier { get; set; } = null!;
    public ICollection<PurchaseOrderLine> Lines { get; set; } = new List<PurchaseOrderLine>();
}
