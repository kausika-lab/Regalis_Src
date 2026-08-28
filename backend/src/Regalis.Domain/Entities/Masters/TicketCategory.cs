using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_TicketCategory.</summary>
public class TicketCategory : AuditableEntity
{
    public int TicketCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }

    public ICollection<TicketIssueType> IssueTypes { get; set; } = new List<TicketIssueType>();
}
