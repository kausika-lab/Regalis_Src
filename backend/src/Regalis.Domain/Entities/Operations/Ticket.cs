using Regalis.Domain.Common;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>TRN_Ticket — support tickets.</summary>
public class Ticket : AuditableEntity
{
    public long TicketId { get; set; }
    public string TicketNumber { get; set; } = string.Empty;
    public long CustomerId { get; set; }
    public int TicketCategoryId { get; set; }
    public int TicketIssueTypeId { get; set; }
    public int LocationId { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;
    public TicketStatus Status { get; set; } = TicketStatus.Open;
    public long? AssignedToUserId { get; set; }
    public DateTime? AssignedUtc { get; set; }
    public DateTime? FirstResponseUtc { get; set; }
    public DateTime? ResolvedUtc { get; set; }
    public DateTime? ClosedUtc { get; set; }
    public string? ResolutionNotes { get; set; }
    public int ReopenCount { get; set; }

    public TicketCategory TicketCategory { get; set; } = null!;
    public TicketIssueType TicketIssueType { get; set; } = null!;
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();
    public ICollection<TicketAttachment> Attachments { get; set; } = new List<TicketAttachment>();
}
