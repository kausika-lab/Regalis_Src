namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_QuotationLine. The document's "Service Type" is modelled as a
/// nullable FK to the existing MST_WorkspaceType (DESK / CABIN / MEETING_ROOM) rather
/// than a new enum, with MST_ServiceItem covering "other workspace services if
/// required later" (§6). Quantity and Duration are separate, as the document lists
/// them: 5 desks for 3 months is Quantity 5, Duration 3 Months.</summary>
public class QuotationLine
{
    public long QuotationLineId { get; set; }
    public long QuotationId { get; set; }
    public int LineNumber { get; set; }
    public int? WorkspaceTypeId { get; set; }
    public int? ServiceItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? HsnSacCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal? DurationValue { get; set; }
    /// <summary>Hour / Day / Month — free text so a new unit needs no migration.</summary>
    public string? DurationUnit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public Quotation Quotation { get; set; } = null!;
}
