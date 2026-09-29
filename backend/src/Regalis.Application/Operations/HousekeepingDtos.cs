using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Operations;

public record HousekeepingScheduleDto(
    long HousekeepingScheduleId,
    string ScheduleNumber,
    long StaffUserId,
    string StaffName,
    int LocationId,
    int? WorkspaceId,
    string? WorkspaceName,
    string? AreaName,
    DateOnly ScheduledDate,
    DateTime StartUtc,
    DateTime EndUtc,
    // 1 Housekeeping, 2 Maintenance, 3 Cleaning, 4 Repair, 5 Inspection.
    byte TaskType,
    string TaskDescription,
    string? Notes,
    // 1 Scheduled, 2 InProgress, 3 Completed, 4 Cancelled, 5 Missed.
    byte Status,
    bool BlocksWorkspace,
    long? WorkspaceBlockId,
    DateTime? CheckInUtc,
    DateTime? CheckOutUtc,
    string? CompletionNotes,
    long AssignedByUserId);

public record SaveHousekeepingScheduleRequest(
    long StaffUserId,
    int LocationId,
    int? WorkspaceId,
    string? AreaName,
    DateTime StartUtc,
    DateTime EndUtc,
    byte TaskType,
    string TaskDescription,
    string? Notes,
    bool BlocksWorkspace);

public record CompleteHousekeepingRequest(string? CompletionNotes);

public class HousekeepingListRequest : PagedRequest
{
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public byte? Status { get; set; }
    public long? StaffUserId { get; set; }
    public int? LocationId { get; set; }
    /// <summary>Workspace the schedule blocks or takes place in.</summary>
    public int? WorkspaceId { get; set; }
}

public class SaveHousekeepingScheduleRequestValidator : AbstractValidator<SaveHousekeepingScheduleRequest>
{
    public SaveHousekeepingScheduleRequestValidator()
    {
        RuleFor(x => x.StaffUserId).GreaterThan(0);
        RuleFor(x => x.LocationId).GreaterThan(0);
        RuleFor(x => x.EndUtc).GreaterThan(x => x.StartUtc)
            .WithMessage("The schedule must end after it starts.");
        RuleFor(x => x.TaskType).InclusiveBetween((byte)1, (byte)5);
        RuleFor(x => x.TaskDescription).NotEmpty().MaximumLength(500);
        RuleFor(x => x.AreaName).MaximumLength(150);
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.WorkspaceId).NotNull()
            .When(x => x.BlocksWorkspace)
            .WithMessage("A schedule that blocks the calendar must name the workspace it blocks.");
    }
}

public class CompleteHousekeepingRequestValidator : AbstractValidator<CompleteHousekeepingRequest>
{
    public CompleteHousekeepingRequestValidator()
    {
        RuleFor(x => x.CompletionNotes).MaximumLength(1000);
    }
}

public interface IHousekeepingService
{
    Task<PagedResult<HousekeepingScheduleDto>> GetSchedulesAsync(HousekeepingListRequest request,
        CancellationToken ct = default);

    /// <summary>Own rows for the signed-in staff member (matrix: Housekeeping
    /// "sees only their own schedule").</summary>
    Task<PagedResult<HousekeepingScheduleDto>> GetMySchedulesAsync(PagedRequest request,
        CancellationToken ct = default);

    /// <summary>Creates the schedule and, when BlocksWorkspace, the TRN_WorkspaceBlock
    /// in the same transaction (Invariant 12) plus a staff access token time-boxed to
    /// the schedule window.</summary>
    Task<HousekeepingScheduleDto> CreateAsync(SaveHousekeepingScheduleRequest request,
        CancellationToken ct = default);

    /// <summary>Edits a Scheduled row; the calendar block and the staff token follow
    /// the new window atomically.</summary>
    Task<HousekeepingScheduleDto> UpdateAsync(long scheduleId, SaveHousekeepingScheduleRequest request,
        CancellationToken ct = default);

    /// <summary>Cancels the schedule, releases the calendar block and revokes the
    /// staff token.</summary>
    Task<HousekeepingScheduleDto> CancelAsync(long scheduleId, CancellationToken ct = default);

    Task<HousekeepingScheduleDto> CheckInAsync(long scheduleId, CancellationToken ct = default);

    /// <summary>Completes the task; an early finish releases the calendar block so the
    /// slot returns to customers immediately.</summary>
    Task<HousekeepingScheduleDto> CheckOutAsync(long scheduleId, CompleteHousekeepingRequest request,
        CancellationToken ct = default);
}
