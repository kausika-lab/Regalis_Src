using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>TRN_HousekeepingSchedule. When BlocksWorkspace is true, creating the schedule
/// writes a TRN_WorkspaceBlock in the same transaction (Invariant 12), so the customer
/// calendar drops the slot automatically.</summary>
public class HousekeepingSchedule : AuditableEntity
{
    public long HousekeepingScheduleId { get; set; }
    public string ScheduleNumber { get; set; } = string.Empty;
    /// <summary>FK → MST_User where UserType = Staff.</summary>
    public long StaffUserId { get; set; }
    public int LocationId { get; set; }
    public int? WorkspaceId { get; set; }
    public string? AreaName { get; set; }
    public DateOnly ScheduledDate { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public HousekeepingTaskType TaskType { get; set; }
    public string TaskDescription { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public HousekeepingStatus Status { get; set; } = HousekeepingStatus.Scheduled;
    public bool BlocksWorkspace { get; set; }
    public long? WorkspaceBlockId { get; set; }
    public DateTime? CheckInUtc { get; set; }
    public DateTime? CheckOutUtc { get; set; }
    public string? CompletionNotes { get; set; }
    public long AssignedByUserId { get; set; }
}
