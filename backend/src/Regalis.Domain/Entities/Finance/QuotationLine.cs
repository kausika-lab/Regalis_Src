using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_QuotationLine. The document's "Service Type" is modelled as a
/// nullable FK to the existing MST_WorkspaceType (DESK / CABIN / MEETING_ROOM) rather
/// than a new enum, with MST_ServiceItem covering "other workspace services if
/// required later" (§6). Quantity and Duration are separate, as the document lists
/// them: 5 desks for 3 months is Quantity 5, Duration 3 Months.
///
/// One quotation mixes services freely — a cabin for 3 months, 5 desks for 1 month and
/// a meeting room for 4 hours are three lines on one document (§1).</summary>
public class QuotationLine
{
    public long QuotationLineId { get; set; }
    public long QuotationId { get; set; }
    public int LineNumber { get; set; }

    /// <summary>Header lines are text-only grouping rows and carry no money (§3.3).</summary>
    public QuotationLineType LineType { get; set; } = QuotationLineType.Service;
    /// <summary>Cabin / Desk / MeetingRoom / Pass / Other — drives the grouped picker
    /// and the Service filter on the list screen (§3.1).</summary>
    public QuotationServiceType ServiceType { get; set; } = QuotationServiceType.Other;

    public int? WorkspaceTypeId { get; set; }
    public int? ServiceItemId { get; set; }
    /// <summary>The specific cabin or meeting room quoted, where one was picked.</summary>
    public int? WorkspaceId { get; set; }
    /// <summary>The pass plan quoted, for a Day Pass / Monthly Pass line.</summary>
    public int? PassPlanId { get; set; }

    public string Description { get; set; } = string.Empty;
    /// <summary>Optional free text under the service name on the PDF (§3.3).</summary>
    public string? LineNotes { get; set; }
    public string? HsnSacCode { get; set; }
    public decimal Quantity { get; set; }
    public decimal? DurationValue { get; set; }
    /// <summary>Hour / Day / Month / One-time — free text so a new unit needs no
    /// migration, and so quotations written before the unit enum still read back.</summary>
    public string? DurationUnit { get; set; }
    public decimal UnitPrice { get; set; }
    /// <summary>The configured price for this service when the line was written. Kept
    /// so the form can flag "changed from ₹27,000 (standard price)" (§3.3).</summary>
    public decimal? StandardRate { get; set; }
    /// <summary>Per-line discount, kept for the lines that carry their own.</summary>
    public decimal DiscountAmount { get; set; }
    /// <summary>This line's share of the whole-quotation discount, split in proportion
    /// to line value, with the last line absorbing the paisa difference (§5.2).</summary>
    public decimal DiscountShare { get; set; }
    public decimal LineAmount { get; set; }
    /// <summary>Line value less both discounts — the base GST is charged on.</summary>
    public decimal TaxableAmount { get; set; }
    public decimal TaxPercent { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }
    public decimal IgstAmount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Optional booking window, where the quotation names actual dates.</summary>
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }

    public Quotation Quotation { get; set; } = null!;
}
