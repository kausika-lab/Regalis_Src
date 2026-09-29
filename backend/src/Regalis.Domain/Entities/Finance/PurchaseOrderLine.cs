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
    /// <summary>The line's own discount plus its share of the whole-order discount.</summary>
    public decimal DiscountAmount { get; set; }
    /// <summary>Taxable value: quantity × rate − DiscountAmount.</summary>
    public decimal LineAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalAmount { get; set; }
    /// <summary>Accounting head, e.g. "Pantry &amp; supplies" (Purchase.AccountCategories).</summary>
    public string? AccountCategory { get; set; }
    /// <summary>Printed under the item name, e.g. "For the 2nd floor desk zone".</summary>
    public string? Remarks { get; set; }

    public PurchaseOrder PurchaseOrder { get; set; } = null!;
}
