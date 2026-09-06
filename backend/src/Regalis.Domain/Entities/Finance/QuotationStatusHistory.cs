using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_QuotationStatusHistory — append-only, mirroring
/// TRN_BookingStatusHistory. This is what makes a quotation's approval, rejection or
/// expiry auditable without ever editing the quotation's own row.</summary>
public class QuotationStatusHistory
{
    public long QuotationStatusHistoryId { get; set; }
    public long QuotationId { get; set; }
    public QuotationStatus? FromStatus { get; set; }
    public QuotationStatus ToStatus { get; set; }
    public long ChangedByUserId { get; set; }
    public DateTime ChangedUtc { get; set; }
    public string? Reason { get; set; }

    public Quotation Quotation { get; set; } = null!;
}
