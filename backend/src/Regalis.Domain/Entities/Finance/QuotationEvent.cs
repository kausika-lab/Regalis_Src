using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Finance;

/// <summary>TRN_QuotationEvent — the activity timeline (document §3.6, §5.1).
/// Append-only and wider than TRN_QuotationStatusHistory, which it sits beside: a send,
/// a download or an internal note is not a status change but still has to be
/// answerable for. Every row records who, when and through which channel.</summary>
public class QuotationEvent
{
    public long QuotationEventId { get; set; }
    public long QuotationId { get; set; }
    public QuotationEventType EventType { get; set; }
    public QuotationActorType ActorType { get; set; } = QuotationActorType.Admin;
    public long? ActorUserId { get; set; }
    /// <summary>Human-readable line for the timeline, already composed.</summary>
    public string Summary { get; set; } = string.Empty;
    /// <summary>Where a send went: the email address or the +91 number.</summary>
    public string? Channel { get; set; }
    public string? Destination { get; set; }
    public QuotationDeliveryStatus? DeliveryStatus { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedUtc { get; set; }

    public Quotation Quotation { get; set; } = null!;
}
