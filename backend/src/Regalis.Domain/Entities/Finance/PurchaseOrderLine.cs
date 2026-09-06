namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_PurchaseOrderLine. ReceivedQuantity is what drives the header's
/// PartiallyReceived / Received status; it only ever moves forward, incremented by
/// the goods-received entry that writes the incoming TRN_InventoryTransaction.</summary>
public class PurchaseOrderLine
{
    public long PurchaseOrderLineId { get; set; }
    public long PurchaseOrderId { get; set; }
    public int LineNumber { get; set; }
    /// <summary>Optional link to the stock master; a PO may buy something not stocked.</summary>
    public int? InventoryItemId { get; set; }
    public string ItemName { get; set; } = string.Empty;
    public string? HsnSacCode { get; set; }
    public string? Unit { get; set; }
    public decimal Quantity { get; set; }
    public decimal ReceivedQuantity { get; set; }
    public decimal Rate { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string? Remarks { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
