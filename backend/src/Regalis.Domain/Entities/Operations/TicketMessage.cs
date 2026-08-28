using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>TRN_TicketMessage.</summary>
public class TicketMessage : AuditableEntity
{
    public long TicketMessageId { get; set; }
    public long TicketId { get; set; }
    public long SenderUserId { get; set; }
    public TicketSenderType SenderType { get; set; }
    public string Message { get; set; } = string.Empty;
    /// <summary>Never returned to the customer.</summary>
    public bool IsInternalNote { get; set; }
    public DateTime SentUtc { get; set; }

    public Ticket Ticket { get; set; } = null!;
}
