using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Masters;

/// <summary>MST_WorkspaceType — DESK, CABIN, MEETING_ROOM.
/// Extensible: adding CONFERENCE_ROOM or EVENT_SPACE is a data insert.</summary>
public class WorkspaceType : AuditableEntity
{
    public int WorkspaceTypeId { get; set; }
    public string TypeCode { get; set; } = string.Empty;
    public string TypeName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public int DisplayOrder { get; set; }
    public bool RequiresCapacity { get; set; }

    public ICollection<Workspace> Workspaces { get; set; } = new List<Workspace>();
}
