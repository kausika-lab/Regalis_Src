using Regalis.Domain.Common;
using Regalis.Domain.Enums;

namespace Regalis.Domain.Entities.Access;

/// <summary>TRN_Attendance — for customers and staff both (A04). Corrections keep the
/// original values so the payroll hand-off is auditable.</summary>
public class Attendance : AuditableEntity
{
    public long AttendanceId { get; set; }
    public long UserId { get; set; }
    public AttendanceSubjectType SubjectType { get; set; }
    public int LocationId { get; set; }
    public int? WorkspaceId { get; set; }
    public long? BookingId { get; set; }
    public long? HousekeepingScheduleId { get; set; }
    public DateOnly AttendanceDate { get; set; }
    public DateTime CheckInUtc { get; set; }
    public DateTime? CheckOutUtc { get; set; }
    public int? TotalMinutes { get; set; }
    public AttendanceStatus Status { get; set; } = AttendanceStatus.CheckedIn;
    public AccessMethod CheckInMethod { get; set; }
    public AccessMethod? CheckOutMethod { get; set; }
    public long? CheckInAccessLogId { get; set; }
    public long? CheckOutAccessLogId { get; set; }
    public bool IsException { get; set; }
    public string? ExceptionNotes { get; set; }
    public bool IsCorrected { get; set; }
    public long? CorrectedByUserId { get; set; }
    public string? CorrectionReason { get; set; }
    public DateTime? OriginalCheckInUtc { get; set; }
    public DateTime? OriginalCheckOutUtc { get; set; }
}
