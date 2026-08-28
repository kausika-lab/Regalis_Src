namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_InvoiceLine.</summary>
public class InvoiceLine
{
    public long InvoiceLineId { get; set; }
    public long InvoiceId { get; set; }
    public int LineNumber { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? HsnSacCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public Invoice Invoice { get; set; } = null!;
}
