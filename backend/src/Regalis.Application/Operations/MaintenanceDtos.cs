using FluentValidation;
using Regalis.Shared.Api;

namespace Regalis.Application.Operations;

// ── What the caller may do (drives which tabs and buttons the page shows) ────

/// <summary>Capabilities are decided on the server. A caller whose only housekeeping role
/// is HOUSEKEEPING is a housekeeper: own tasks only, no add/assign/verify, whatever
/// permissions that role carries.</summary>
public record MaintenanceMeDto(
    long UserId,
    string FullName,
    bool IsHousekeeper,
    bool CanManageWork,
    bool CanDeleteWork,
    bool CanVerify,
    bool CanManageHousekeepers,
    MaintenanceHousekeeperDto? Housekeeper);

public record MaintenanceOptionsDto(
    IReadOnlyList<string> Categories,
    IReadOnlyList<MaintenanceAreaOptionDto> Areas,
    IReadOnlyList<MaintenanceAssigneeOptionDto> Housekeepers,
    string CurrentShift);

/// <summary>An area is either a bookable workspace (can be blocked) or a common area.</summary>
public record MaintenanceAreaOptionDto(string Name, int? WorkspaceId, int? LocationId);

public record MaintenanceAssigneeOptionDto(long UserId, string FullName, string? HousekeeperCode,
    byte? Shift, string? AssignedAreas);

// ── Checklist ────────────────────────────────────────────────────────────────

public class MaintenanceWorkListRequest : PagedRequest
{
    /// <summary>1 Pending … 7 Cancelled. "Overdue" (6) also matches tasks past due that
    /// the sweeper has not reached yet.</summary>
    public byte? Status { get; set; }
    public string? Area { get; set; }
    public string? Category { get; set; }
    public long? AssigneeId { get; set; }
    /// <summary>IST day the task is due; null = today. Use All = true for every date.</summary>
    public DateOnly? Date { get; set; }
    public bool All { get; set; }
}

public record MaintenanceWorkDto(
    long WorkItemId,
    string WorkNumber,
    string Title,
    string AreaName,
    int? WorkspaceId,
    string Category,
    byte Priority,
    long AssignedToUserId,
    string AssignedToName,
    DateTime DueUtc,
    // Effective status: a Pending/InProgress task already past due reads as Overdue (6).
    byte Status,
    string? Instructions,
    DateTime? StartedUtc,
    DateTime? CompletedUtc,
    string? CompletedByName,
    string? CompletionNote,
    bool HasPhoto,
    DateTime? VerifiedUtc,
    string? VerifiedByName,
    string? ReworkReason,
    int ReworkCount,
    long? LinkedScheduleId,
    string? LinkedScheduleNumber,
    string? CancelReason,
    // For the current caller.
    bool CanStart,
    bool CanMarkDone,
    bool CanVerify,
    bool CanEdit,
    bool CanDelete);

public record MaintenanceSummaryDto(
    DateOnly Date,
    int Total,
    int Categories,
    int Completed,
    int Verified,
    int WaitingForVerification,
    int Overdue,
    int Percent,
    int OnDutyNow,
    int ActiveHousekeepers,
    string CurrentShift);

public record MaintenanceWorkPageDto(PagedResult<MaintenanceWorkDto> Work, MaintenanceSummaryDto Summary);

public record SaveMaintenanceWorkRequest(
    string Title,
    string AreaName,
    int? WorkspaceId,
    string Category,
    byte Priority,
    long AssignedToUserId,
    DateTime DueUtc,
    string? Instructions,
    bool BlockWorkspace,
    // When blocking: the window start; defaults to one hour before the due time.
    DateTime? BlockFromUtc,
    bool NotifyHousekeeper);

public record MaintenanceAssignRequest(long AssignedToUserId);
public record MaintenanceReasonRequest(string? Reason);

public record MaintenancePhotoContent(byte[] Content, string ContentType, string FileName);

// ── My tasks (housekeeper phone view) ────────────────────────────────────────

public record MaintenanceMyTasksDto(
    MaintenanceHousekeeperDto? Housekeeper,
    int TodayTotal,
    int TodayCompleted,
    int Percent,
    int ToDoCount,
    int DoneCount,
    int ReworkCount,
    IReadOnlyList<MaintenanceWorkDto> Tasks);

// ── Housekeepers ─────────────────────────────────────────────────────────────

public class HousekeeperListRequest : PagedRequest
{
    public byte? Shift { get; set; }
    /// <summary>"active" (default), "inactive" or "all".</summary>
    public string? Status { get; set; }
}

public record MaintenanceHousekeeperDto(
    long UserId,
    string? HousekeeperCode,
    string FullName,
    string? Email,
    string? MobileNumber,
    byte Shift,
    string? AssignedAreas,
    DateOnly? JoiningDate,
    bool IsActive,
    int TodayDone,
    int TodayPending,
    int TodayRework,
    int VerifiedCount,
    int OnTimeCount,
    // Tasks verified before their due time ÷ tasks verified, 0–100; null when none verified.
    int? OnTimeRate,
    int OpenTasks);

public record SaveHousekeeperRequest(
    string FullName,
    string MobileNumber,
    string? Email,
    byte Shift,
    DateOnly JoiningDate,
    IReadOnlyList<string>? AssignedAreas,
    bool IsActive = true);

/// <summary>Returned once when a housekeeper is created with a login: the temporary
/// password is shown to the admin to hand over, and must be changed on first sign-in.</summary>
public record HousekeeperCreatedDto(MaintenanceHousekeeperDto Housekeeper, string? TemporaryPassword);

public record DeactivateHousekeeperRequest(long? ReassignToUserId);

public record HousekeeperWorkLogDto(
    MaintenanceHousekeeperDto Housekeeper,
    int Assigned,
    int Completed,
    int Pending,
    int Overdue,
    int Rework,
    int? AverageMinutes,
    IReadOnlyList<MaintenanceWorkDto> Recent);

// ── Validation (messages from the spec, §5.5) ────────────────────────────────

public class SaveMaintenanceWorkRequestValidator : AbstractValidator<SaveMaintenanceWorkRequest>
{
    public SaveMaintenanceWorkRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Enter a title for the work.").MaximumLength(200);
        RuleFor(x => x.AreaName).NotEmpty().WithMessage("Select an area, a category and a priority.").MaximumLength(100);
        RuleFor(x => x.Category).NotEmpty().WithMessage("Select an area, a category and a priority.").MaximumLength(60);
        RuleFor(x => x.Priority).InclusiveBetween((byte)1, (byte)3).WithMessage("Select an area, a category and a priority.");
        RuleFor(x => x.AssignedToUserId).GreaterThan(0).WithMessage("Select an active housekeeper.");
        RuleFor(x => x.Instructions).MaximumLength(1000);
        RuleFor(x => x.WorkspaceId).NotNull().When(x => x.BlockWorkspace)
            .WithMessage("Pick a workspace as the area to block it.");
    }
}

public class SaveHousekeeperRequestValidator : AbstractValidator<SaveHousekeeperRequest>
{
    public SaveHousekeeperRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
        RuleFor(x => x.MobileNumber).Matches("^[6-9][0-9]{9}$").WithMessage("Enter a 10-digit mobile number.");
        RuleFor(x => x.Email).NotEmpty().WithMessage("Enter an email — it is the housekeeper's sign-in for My tasks.")
            .EmailAddress().MaximumLength(256);
        RuleFor(x => x.Shift).InclusiveBetween((byte)1, (byte)3);
        RuleFor(x => x.AssignedAreas).Must(a => a is null || string.Join(",", a).Length <= 300)
            .WithMessage("Too many areas.");
    }
}

public interface IMaintenanceService
{
    Task<MaintenanceMeDto> GetMeAsync(CancellationToken ct = default);
    Task<MaintenanceOptionsDto> GetOptionsAsync(CancellationToken ct = default);

    Task<MaintenanceWorkPageDto> GetWorkAsync(MaintenanceWorkListRequest request, CancellationToken ct = default);
    Task<MaintenanceWorkDto> GetWorkItemAsync(long id, CancellationToken ct = default);
    Task<byte[]> ExportWorkAsync(MaintenanceWorkListRequest request, CancellationToken ct = default);
    Task<MaintenanceWorkDto> CreateAsync(SaveMaintenanceWorkRequest request, CancellationToken ct = default);
    Task<MaintenanceWorkDto> UpdateAsync(long id, SaveMaintenanceWorkRequest request, CancellationToken ct = default);
    Task<MaintenanceWorkDto> AssignAsync(long id, long assignedToUserId, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task<MaintenanceWorkDto> CancelAsync(long id, string? reason, CancellationToken ct = default);
    Task<MaintenanceWorkDto> StartAsync(long id, CancellationToken ct = default);
    Task<MaintenanceWorkDto> MarkDoneAsync(long id, string? note, AttachmentUpload? photo, CancellationToken ct = default);
    Task<MaintenanceWorkDto> VerifyAsync(long id, CancellationToken ct = default);
    Task<MaintenanceWorkDto> RejectAsync(long id, string? reason, CancellationToken ct = default);
    Task<MaintenancePhotoContent> GetPhotoAsync(long id, CancellationToken ct = default);

    Task<MaintenanceMyTasksDto> GetMyTasksAsync(string? tab, CancellationToken ct = default);

    Task<PagedResult<MaintenanceHousekeeperDto>> GetHousekeepersAsync(HousekeeperListRequest request, CancellationToken ct = default);
    Task<HousekeeperCreatedDto> CreateHousekeeperAsync(SaveHousekeeperRequest request, CancellationToken ct = default);
    Task<MaintenanceHousekeeperDto> UpdateHousekeeperAsync(long userId, SaveHousekeeperRequest request, CancellationToken ct = default);
    Task<MaintenanceHousekeeperDto> DeactivateHousekeeperAsync(long userId, long? reassignToUserId, CancellationToken ct = default);
    Task<MaintenanceHousekeeperDto> ActivateHousekeeperAsync(long userId, CancellationToken ct = default);
    Task<HousekeeperWorkLogDto> GetWorkLogAsync(long userId, CancellationToken ct = default);

    /// <summary>A housekeeping/cleaning schedule created on the Schedules tab gets its
    /// matching checklist task.</summary>
    Task CreateForScheduleAsync(long scheduleId, CancellationToken ct = default);

    /// <summary>Cancelling a schedule on the Schedules tab cancels its open checklist task.</summary>
    Task CancelForScheduleAsync(long scheduleId, CancellationToken ct = default);

    /// <summary>Background sweep: Pending/InProgress past due (plus grace) → Overdue,
    /// with alerts to admins and supervisors. Returns how many tasks turned overdue.</summary>
    Task<int> SweepOverdueAsync(CancellationToken ct = default);
}
