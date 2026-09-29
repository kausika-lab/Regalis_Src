using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_PurchaseOrderStatusHistory — append-only, mirroring
/// TRN_QuotationStatusHistory. Every workflow step (submit, approve, reject, send,
/// receive, cancel) writes one row, which is also the approval record: who approved
/// or rejected, when, and the comment they gave.</summary>
public class PurchaseOrderStatusHistory
{
    public long PurchaseOrderStatusHistoryId { get; set; }
    public long PurchaseOrderId { get; set; }
    public PurchaseOrderStatus? FromStatus { get; set; }
    public PurchaseOrderStatus ToStatus { get; set; }
    /// <summary>"Created", "Submitted", "Approved", "Rejected", "Sent", "Received"…</summary>
    public string Action { get; set; } = string.Empty;
    public long ChangedByUserId { get; set; }
    public DateTime ChangedUtc { get; set; }
    public string? Reason { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
