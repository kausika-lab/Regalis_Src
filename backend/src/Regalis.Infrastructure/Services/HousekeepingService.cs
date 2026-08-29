using System.Data;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Access;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Security;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Housekeeping / maintenance scheduling (M18). A schedule that blocks a workspace
/// writes the TRN_WorkspaceBlock in the same transaction (Invariant 12) — customer
/// availability drops the slot through the one existing calendar path, and the staff
/// access token is time-boxed to exactly the schedule window.
/// </summary>
public class HousekeepingService(
    RegalisDbContext db,
    INumberSequenceService numberSequence,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IOptions<JwtOptions> jwtOptions) : IHousekeepingService
{
    public async Task<PagedResult<HousekeepingScheduleDto>> GetSchedulesAsync(
        HousekeepingListRequest request, CancellationToken ct = default)
    {
        var query = db.HousekeepingSchedules.AsNoTracking();
        // Matrix: housekeeping staff see only their own schedule, whatever they ask for.
        if (currentUser.UserType == UserType.Staff)
        {
            query = query.Where(h => h.StaffUserId == currentUser.UserId);
        }
        if (request.From is not null)
        {
            query = query.Where(h => h.ScheduledDate >= request.From);
        }
        if (request.To is not null)
        {
            query = query.Where(h => h.ScheduledDate <= request.To);
        }
        if (request.Status is not null)
        {
            query = query.Where(h => (byte)h.Status == request.Status);
        }
        if (request.StaffUserId is not null)
        {
            query = query.Where(h => h.StaffUserId == request.StaffUserId);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(h => h.LocationId == request.LocationId);
        }
        return await PageAsync(query, request, ct);
    }

    public async Task<PagedResult<HousekeepingScheduleDto>> GetMySchedulesAsync(
        PagedRequest request, CancellationToken ct = default) =>
        await PageAsync(db.HousekeepingSchedules.AsNoTracking()
            .Where(h => h.StaffUserId == currentUser.UserId), request, ct);

    public async Task<HousekeepingScheduleDto> CreateAsync(SaveHousekeepingScheduleRequest request,
        CancellationToken ct = default)
    {
        await ValidateAsync(request, excludeScheduleId: null, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        var scheduleId = await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            if (request.BlocksWorkspace)
            {
                await EnsureWorkspaceIsFreeAsync(request, excludeScheduleId: null, ct);
            }

            var schedule = new HousekeepingSchedule
            {
                ScheduleNumber = await numberSequence.NextAsync(SequenceKeys.HousekeepingSchedule, ct),
                StaffUserId = request.StaffUserId,
                LocationId = request.LocationId,
                WorkspaceId = request.WorkspaceId,
                AreaName = request.AreaName?.Trim(),
                ScheduledDate = IstClock.IstDateOf(request.StartUtc),
                StartUtc = request.StartUtc,
                EndUtc = request.EndUtc,
                TaskType = (HousekeepingTaskType)request.TaskType,
                TaskDescription = request.TaskDescription.Trim(),
                Notes = request.Notes?.Trim(),
                BlocksWorkspace = request.BlocksWorkspace,
                AssignedByUserId = currentUser.UserId
            };
            db.HousekeepingSchedules.Add(schedule);
            await db.SaveChangesAsync(ct);

            if (request.BlocksWorkspace)
            {
                var block = new WorkspaceBlock
                {
                    WorkspaceId = request.WorkspaceId!.Value,
                    LocationId = request.LocationId,
                    StartUtc = request.StartUtc,
                    EndUtc = request.EndUtc,
                    BlockReason = BlockReason.Housekeeping,
                    ReasonNotes = $"{schedule.ScheduleNumber}: {schedule.TaskDescription}",
                    HousekeepingScheduleId = schedule.HousekeepingScheduleId,
                    BlockedByUserId = currentUser.UserId
                };
                db.WorkspaceBlocks.Add(block);
                await db.SaveChangesAsync(ct);
                schedule.WorkspaceBlockId = block.WorkspaceBlockId;
            }

            db.AccessTokens.Add(BuildStaffToken(schedule));
            db.Notifications.Add(BuildAssignmentNotification(schedule));
            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
            return schedule.HousekeepingScheduleId;
        });

        return await ToDtoAsync(scheduleId, ct);
    }

    public async Task<HousekeepingScheduleDto> UpdateAsync(long scheduleId,
        SaveHousekeepingScheduleRequest request, CancellationToken ct = default)
    {
        await ValidateAsync(request, scheduleId, ct);

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction =
                await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

            var schedule = await db.HousekeepingSchedules
                .FirstOrDefaultAsync(h => h.HousekeepingScheduleId == scheduleId, ct)
                ?? throw new NotFoundException("HousekeepingSchedule", scheduleId);
            if (schedule.Status != HousekeepingStatus.Scheduled)
            {
                throw new BusinessRuleException(
                    "Only a Scheduled task can be edited. Cancel it or complete it instead.");
            }

            if (request.BlocksWorkspace)
            {
                await EnsureWorkspaceIsFreeAsync(request, scheduleId, ct);
            }

            schedule.StaffUserId = request.StaffUserId;
            schedule.LocationId = request.LocationId;
            schedule.WorkspaceId = request.WorkspaceId;
            schedule.AreaName = request.AreaName?.Trim();
            schedule.ScheduledDate = IstClock.IstDateOf(request.StartUtc);
            schedule.StartUtc = request.StartUtc;
            schedule.EndUtc = request.EndUtc;
            schedule.TaskType = (HousekeepingTaskType)request.TaskType;
            schedule.TaskDescription = request.TaskDescription.Trim();
            schedule.Notes = request.Notes?.Trim();
            schedule.BlocksWorkspace = request.BlocksWorkspace;

            // The calendar block follows the schedule — release-and-rewrite keeps the
            // audit trail (a block row is never edited in place, DATABASE_DESIGN §5).
            await ReleaseBlockAsync(schedule, ct);
            if (request.BlocksWorkspace)
            {
                var block = new WorkspaceBlock
                {
                    WorkspaceId = request.WorkspaceId!.Value,
                    LocationId = request.LocationId,
                    StartUtc = request.StartUtc,
                    EndUtc = request.EndUtc,
                    BlockReason = BlockReason.Housekeeping,
                    ReasonNotes = $"{schedule.ScheduleNumber}: {schedule.TaskDescription}",
                    HousekeepingScheduleId = schedule.HousekeepingScheduleId,
                    BlockedByUserId = currentUser.UserId
                };
                db.WorkspaceBlocks.Add(block);
                await db.SaveChangesAsync(ct);
                schedule.WorkspaceBlockId = block.WorkspaceBlockId;
            }
            else
            {
                schedule.WorkspaceBlockId = null;
            }

            // The staff token is reissued so it is time-boxed to the new window
            // (and the new assignee, if the task changed hands).
            await RevokeTokensAsync(schedule.HousekeepingScheduleId, "Schedule changed", ct);
            db.AccessTokens.Add(BuildStaffToken(schedule));
            db.Notifications.Add(BuildAssignmentNotification(schedule, updated: true));
            await db.SaveChangesAsync(ct);

            await transaction.CommitAsync(ct);
        });

        return await ToDtoAsync(scheduleId, ct);
    }

    public async Task<HousekeepingScheduleDto> CancelAsync(long scheduleId,
        CancellationToken ct = default)
    {
        var schedule = await db.HousekeepingSchedules
            .FirstOrDefaultAsync(h => h.HousekeepingScheduleId == scheduleId, ct)
            ?? throw new NotFoundException("HousekeepingSchedule", scheduleId);
        if (schedule.Status is not (HousekeepingStatus.Scheduled or HousekeepingStatus.InProgress))
        {
            throw new BusinessRuleException("Only a Scheduled or InProgress task can be cancelled.");
        }

        schedule.Status = HousekeepingStatus.Cancelled;
        await ReleaseBlockAsync(schedule, ct);
        await RevokeTokensAsync(scheduleId, "Schedule cancelled", ct);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(scheduleId, ct);
    }

    public async Task<HousekeepingScheduleDto> CheckInAsync(long scheduleId,
        CancellationToken ct = default)
    {
        var schedule = await db.HousekeepingSchedules
            .FirstOrDefaultAsync(h => h.HousekeepingScheduleId == scheduleId, ct)
            ?? throw new NotFoundException("HousekeepingSchedule", scheduleId);
        if (schedule.Status != HousekeepingStatus.Scheduled)
        {
            throw new BusinessRuleException("Only a Scheduled task can be started.");
        }
        schedule.Status = HousekeepingStatus.InProgress;
        schedule.CheckInUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(scheduleId, ct);
    }

    public async Task<HousekeepingScheduleDto> CheckOutAsync(long scheduleId,
        CompleteHousekeepingRequest request, CancellationToken ct = default)
    {
        var schedule = await db.HousekeepingSchedules
            .FirstOrDefaultAsync(h => h.HousekeepingScheduleId == scheduleId, ct)
            ?? throw new NotFoundException("HousekeepingSchedule", scheduleId);
        if (schedule.Status != HousekeepingStatus.InProgress)
        {
            throw new BusinessRuleException("Check in before checking out.");
        }
        schedule.Status = HousekeepingStatus.Completed;
        schedule.CheckOutUtc = clock.UtcNow;
        schedule.CompletionNotes = request.CompletionNotes?.Trim();
        // An early finish releases the block — the slot returns to customers now.
        await ReleaseBlockAsync(schedule, ct);
        await db.SaveChangesAsync(ct);
        return await ToDtoAsync(scheduleId, ct);
    }

    // ── Rules ───────────────────────────────────────────────────────────────

    private async Task ValidateAsync(SaveHousekeepingScheduleRequest request,
        long? excludeScheduleId, CancellationToken ct)
    {
        var staff = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == request.StaffUserId, ct)
            ?? throw new NotFoundException("User", request.StaffUserId);
        if (staff.UserType != UserType.Staff)
        {
            throw new BusinessRuleException("Schedules are assigned to staff users only.");
        }
        if (!await db.Locations.AnyAsync(l => l.LocationId == request.LocationId, ct))
        {
            throw new NotFoundException("Location", request.LocationId);
        }
        if (request.WorkspaceId is not null)
        {
            var workspace = await db.Workspaces.AsNoTracking()
                .FirstOrDefaultAsync(w => w.WorkspaceId == request.WorkspaceId, ct)
                ?? throw new NotFoundException("Workspace", request.WorkspaceId);
            if (workspace.LocationId != request.LocationId)
            {
                throw new BusinessRuleException("The workspace is not at the selected location.");
            }
        }
        if (request.EndUtc <= clock.UtcNow)
        {
            throw new BusinessRuleException("The schedule window is entirely in the past.");
        }

        // One person, one place at a time.
        var staffBusy = await db.HousekeepingSchedules.AnyAsync(h =>
            h.StaffUserId == request.StaffUserId &&
            (excludeScheduleId == null || h.HousekeepingScheduleId != excludeScheduleId) &&
            (h.Status == HousekeepingStatus.Scheduled || h.Status == HousekeepingStatus.InProgress) &&
            h.StartUtc < request.EndUtc && request.StartUtc < h.EndUtc, ct);
        if (staffBusy)
        {
            throw new ConflictException("This staff member already has a schedule in that window.");
        }
    }

    /// <summary>A block must not swallow paid bookings (same rule as the manual
    /// calendar block) — they are cancelled or rescheduled first, by a human.</summary>
    private async Task EnsureWorkspaceIsFreeAsync(SaveHousekeepingScheduleRequest request,
        long? excludeScheduleId, CancellationToken ct)
    {
        var workspaceId = request.WorkspaceId!.Value;
        var overlappingBooking = await db.Bookings.AnyAsync(b =>
            b.WorkspaceId == workspaceId &&
            Booking.OccupancyStatuses.Contains(b.Status) &&
            b.StartUtc < request.EndUtc && request.StartUtc < b.EndUtc, ct);
        if (overlappingBooking)
        {
            throw new ConflictException(
                "Customer bookings exist in this window. Cancel or reschedule them before blocking.");
        }
        var overlappingBlock = await db.WorkspaceBlocks.AnyAsync(k =>
            k.WorkspaceId == workspaceId && k.IsActive &&
            (excludeScheduleId == null || k.HousekeepingScheduleId != excludeScheduleId) &&
            k.StartUtc < request.EndUtc && request.StartUtc < k.EndUtc, ct);
        if (overlappingBlock)
        {
            throw new ConflictException("The workspace is already blocked in that window.");
        }
    }

    private async Task ReleaseBlockAsync(HousekeepingSchedule schedule, CancellationToken ct)
    {
        var blocks = await db.WorkspaceBlocks
            .Where(k => k.HousekeepingScheduleId == schedule.HousekeepingScheduleId && k.IsActive)
            .ToListAsync(ct);
        foreach (var block in blocks)
        {
            block.IsActive = false;
            block.ReleasedUtc = clock.UtcNow;
            block.ReleasedByUserId = currentUser.UserId;
        }
    }

    private async Task RevokeTokensAsync(long scheduleId, string reason, CancellationToken ct)
    {
        var tokens = await db.AccessTokens
            .Where(t => t.HousekeepingScheduleId == scheduleId && t.Status == AccessTokenStatus.Active)
            .ToListAsync(ct);
        foreach (var token in tokens)
        {
            token.Status = AccessTokenStatus.Revoked;
            token.RevokedReason = reason;
        }
    }

    /// <summary>The staff token opens the door only inside the schedule window —
    /// time-boxed access, never a standing pass (Day 8 senior scope).</summary>
    private AccessToken BuildStaffToken(HousekeepingSchedule schedule)
    {
        var tokenValue = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        return new AccessToken
        {
            TokenValue = tokenValue,
            QrPayload = QrPayloadSigner.Sign(tokenValue, jwtOptions.Value.SigningKey),
            UserId = schedule.StaffUserId,
            HousekeepingScheduleId = schedule.HousekeepingScheduleId,
            WorkspaceId = schedule.WorkspaceId,
            LocationId = schedule.LocationId,
            TokenPurpose = AccessTokenPurpose.Housekeeping,
            ValidFromUtc = schedule.StartUtc,
            ValidToUtc = schedule.EndUtc,
            Status = AccessTokenStatus.Active
        };
    }

    private Notification BuildAssignmentNotification(HousekeepingSchedule schedule,
        bool updated = false)
    {
        var startIst = IstClock.ToIst(schedule.StartUtc);
        var endIst = IstClock.ToIst(schedule.EndUtc);
        return new Notification
        {
            UserId = schedule.StaffUserId,
            Audience = NotificationAudience.SingleUser,
            Title = updated ? "Schedule updated" : "New schedule assigned",
            Message = $"{schedule.ScheduleNumber}: {schedule.TaskDescription} on " +
                      $"{startIst:dd MMM yyyy}, {startIst:HH:mm}–{endIst:HH:mm} IST. " +
                      "Your access QR is valid for this window.",
            Category = NotificationCategory.Housekeeping,
            ReferenceType = "HousekeepingSchedule",
            ReferenceId = schedule.HousekeepingScheduleId,
            SentUtc = clock.UtcNow
        };
    }

    // ── Shaping ─────────────────────────────────────────────────────────────

    private async Task<PagedResult<HousekeepingScheduleDto>> PageAsync(
        IQueryable<HousekeepingSchedule> query, PagedRequest request, CancellationToken ct)
    {
        var totalCount = await query.CountAsync(ct);
        var items = await Shape(query
                .OrderByDescending(h => h.StartUtc)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize))
            .ToListAsync(ct);
        return PagedResult<HousekeepingScheduleDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    private async Task<HousekeepingScheduleDto> ToDtoAsync(long scheduleId, CancellationToken ct) =>
        await Shape(db.HousekeepingSchedules.AsNoTracking()
            .Where(h => h.HousekeepingScheduleId == scheduleId))
            .FirstAsync(ct);

    private IQueryable<HousekeepingScheduleDto> Shape(IQueryable<HousekeepingSchedule> query) =>
        query
            .Join(db.Users, h => h.StaffUserId, u => u.UserId, (h, u) => new { h, StaffName = u.FullName })
            .GroupJoin(db.Workspaces, x => x.h.WorkspaceId, w => (int?)w.WorkspaceId,
                (x, workspaces) => new { x.h, x.StaffName, workspaces })
            .SelectMany(x => x.workspaces.DefaultIfEmpty(), (x, w) => new HousekeepingScheduleDto(
                x.h.HousekeepingScheduleId, x.h.ScheduleNumber, x.h.StaffUserId, x.StaffName,
                x.h.LocationId, x.h.WorkspaceId, w == null ? null : w.WorkspaceName, x.h.AreaName,
                x.h.ScheduledDate, x.h.StartUtc, x.h.EndUtc, (byte)x.h.TaskType, x.h.TaskDescription,
                x.h.Notes, (byte)x.h.Status, x.h.BlocksWorkspace, x.h.WorkspaceBlockId,
                x.h.CheckInUtc, x.h.CheckOutUtc, x.h.CompletionNotes, x.h.AssignedByUserId));
}
