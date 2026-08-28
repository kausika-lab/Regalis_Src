using Regalis.Domain.Common;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Booking;

/// <summary>TRN_WorkspaceBlock — everything that occupies a workspace but is not a
/// customer booking. Housekeeping schedules write these rows so customer availability
/// drops automatically (Invariant 12) — there is no second code path.</summary>
public class WorkspaceBlock : AuditableEntity
{
    public long WorkspaceBlockId { get; set; }
    public int WorkspaceId { get; set; }
    public int LocationId { get; set; }
    /// <summary>Inclusive.</summary>
    public DateTime StartUtc { get; set; }
    /// <summary>Exclusive.</summary>
    public DateTime EndUtc { get; set; }
    public BlockReason BlockReason { get; set; }
    public string? ReasonNotes { get; set; }
    /// <summary>Set when the block came from a housekeeping schedule.</summary>
    public long? HousekeepingScheduleId { get; set; }
    public long BlockedByUserId { get; set; }
    /// <summary>Unblock = set this + IsActive false; the row is kept.</summary>
    public DateTime? ReleasedUtc { get; set; }
    public long? ReleasedByUserId { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Location Location { get; set; } = null!;
}
