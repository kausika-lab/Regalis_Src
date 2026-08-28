using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_TicketIssueType.</summary>
public class TicketIssueType : AuditableEntity
{
    public int TicketIssueTypeId { get; set; }
    public int TicketCategoryId { get; set; }
    public string IssueTypeName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public TicketCategory TicketCategory { get; set; } = null!;
}
