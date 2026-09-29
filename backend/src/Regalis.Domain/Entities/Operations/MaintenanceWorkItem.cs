using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Operations;

/// <summary>TRN_MaintenanceWorkItem — one checklist task (MW-000142). A work item is the
/// job a housekeeper must finish; a HousekeepingSchedule is a time window that blocks a
/// workspace. When "block this workspace" is ticked the two are linked
/// (LinkedScheduleId) and kept in step.</summary>
public class MaintenanceWorkItem : AuditableEntity
{
    public long MaintenanceWorkItemId { get; set; }
    public string WorkNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int LocationId { get; set; }
    /// <summary>Set when the area is a bookable workspace (lets the task block it).</summary>
    public int? WorkspaceId { get; set; }
    public string AreaName { get; set; } = string.Empty;
    /// <summary>One of Maintenance.Categories (Cleaning, Plumbing, Electrical, Laundry, Other).</summary>
    public string Category { get; set; } = string.Empty;
    public MaintenancePriority Priority { get; set; } = MaintenancePriority.Medium;
    public long AssignedToUserId { get; set; }
    public DateTime AssignedUtc { get; set; }
    public DateTime DueUtc { get; set; }
    public MaintenanceWorkStatus Status { get; set; } = MaintenanceWorkStatus.Pending;
    public string? Instructions { get; set; }

    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }
    public long? CompletedByUserId { get; set; }
    public string? CompletionNote { get; set; }
    public string? PhotoPath { get; set; }
    public string? PhotoFileName { get; set; }
    public string? PhotoContentType { get; set; }

    public DateTime? VerifiedUtc { get; set; }
    public long? VerifiedByUserId { get; set; }
    public string? ReworkReason { get; set; }
    public int ReworkCount { get; set; }
    /// <summary>Set once, the first time the sweeper finds the task past due.</summary>
    public DateTime? OverdueSinceUtc { get; set; }

    public DateTime? CancelledUtc { get; set; }
    public string? CancelReason { get; set; }

    public long? LinkedScheduleId { get; set; }
}
