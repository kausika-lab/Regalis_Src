using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Operations;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// Maintenance &amp; housekeeping checklist (Maintenance Feature Specification v1.0).
///
/// Roles are decided here, not by the permission codes alone: the HOUSEKEEPING role
/// carries HOUSEKEEPING.CREATE/EDIT/APPROVE, so a caller whose housekeeping role is only
/// HOUSEKEEPING is treated as a housekeeper — own tasks, start and mark done, nothing
/// else. Supervisors are callers with HOUSEKEEPING.APPROVE who are not housekeepers
/// (today: Admin and Operations Manager). Nobody verifies work they completed.
/// Housekeepers and deleting work are Admin-only.
///
/// Progress (confirmed with the business): Done and Verified both count as complete.
/// </summary>
public class MaintenanceService(
    RegalisDbContext db,
    INumberSequenceService sequences,
    ISettingService settings,
    IHousekeepingService housekeeping,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    IOptions<StorageOptions> storageOptions,
    ILogger<MaintenanceService> logger) : IMaintenanceService
{
    private const long MaxPhotoBytes = 10 * 1024 * 1024;
    private static readonly Dictionary<string, string> PhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".png"] = "image/png", [".webp"] = "image/webp"
    };
    private static readonly MaintenanceWorkStatus[] OpenStatuses =
        [MaintenanceWorkStatus.Pending, MaintenanceWorkStatus.InProgress, MaintenanceWorkStatus.Overdue, MaintenanceWorkStatus.Rework];

    // ── Who is calling ───────────────────────────────────────────────────────

    private bool IsHousekeeper => currentUser.Roles.Contains(RoleCodes.Housekeeping)
                                  && !currentUser.Roles.Contains(RoleCodes.Admin)
                                  && !currentUser.Roles.Contains(RoleCodes.Manager);
    private bool IsAdmin => currentUser.Roles.Contains(RoleCodes.Admin);
    private bool CanManage => !IsHousekeeper && (currentUser.HasPermission(PermissionCodes.Housekeeping.Create)
                                                 || currentUser.HasPermission(PermissionCodes.Housekeeping.Edit));
    private bool CanVerifyAny => !IsHousekeeper && currentUser.HasPermission(PermissionCodes.Housekeeping.Approve);

    private void Require(bool allowed, string message)
    {
        if (!allowed)
        {
            throw new ForbiddenException(message);
        }
    }

    public async Task<MaintenanceMeDto> GetMeAsync(CancellationToken ct = default)
    {
        var me = await db.Users.AsNoTracking().Where(u => u.UserId == currentUser.UserId)
            .Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "";
        var profile = await FindHousekeeperAsync(currentUser.UserId, ct);
        return new MaintenanceMeDto(currentUser.UserId, me, IsHousekeeper, CanManage, IsAdmin, CanVerifyAny,
            IsAdmin, profile);
    }

    public async Task<MaintenanceOptionsDto> GetOptionsAsync(CancellationToken ct = default)
    {
        var categories = Csv(await settings.GetStringAsync(AppSettingKeys.Maintenance.Categories,
            "Cleaning,Plumbing,Electrical,Laundry,Other", ct));
        var common = Csv(await settings.GetStringAsync(AppSettingKeys.Maintenance.Areas,
            "Lobby,Reception,Pantry,Washroom 1F,Washroom 2F,Floor 1,Floor 2,Corridor,Terrace", ct));
        var workspaces = await db.Workspaces.AsNoTracking().Where(w => w.IsActive)
            .OrderBy(w => w.WorkspaceName)
            .Select(w => new MaintenanceAreaOptionDto(w.WorkspaceName, w.WorkspaceId, w.LocationId))
            .ToListAsync(ct);
        var areas = workspaces.Concat(common.Where(c => workspaces.All(w => !w.Name.Equals(c, StringComparison.OrdinalIgnoreCase)))
            .Select(c => new MaintenanceAreaOptionDto(c, null, null))).ToList();
        var housekeepers = await (
                from h in db.HousekeeperProfiles.AsNoTracking()
                join u in db.Users.AsNoTracking() on h.UserId equals u.UserId
                where u.Status == UserStatus.Active
                orderby u.FullName
                select new MaintenanceAssigneeOptionDto(u.UserId, u.FullName, h.HousekeeperCode, (byte)h.Shift, h.AssignedAreas))
            .ToListAsync(ct);
        return new MaintenanceOptionsDto(categories, areas, housekeepers, (await CurrentShiftAsync(ct)).ToString());
    }

    // ── Checklist ────────────────────────────────────────────────────────────

    public async Task<MaintenanceWorkPageDto> GetWorkAsync(MaintenanceWorkListRequest request, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var date = request.Date ?? IstClock.IstDateOf(now);
        var scoped = Scoped();
        var query = scoped;
        if (!request.All)
        {
            var (from, to) = DayUtc(date);
            query = query.Where(w => w.DueUtc >= from && w.DueUtc < to);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(w => w.WorkNumber.Contains(term) || w.Title.Contains(term) || w.AreaName.Contains(term));
        }
        if (request.Status is not null)
        {
            var status = (MaintenanceWorkStatus)request.Status.Value;
            query = status switch
            {
                MaintenanceWorkStatus.Overdue => query.Where(w => w.Status == MaintenanceWorkStatus.Overdue
                    || ((w.Status == MaintenanceWorkStatus.Pending || w.Status == MaintenanceWorkStatus.InProgress) && w.DueUtc < now)),
                MaintenanceWorkStatus.Pending or MaintenanceWorkStatus.InProgress =>
                    query.Where(w => w.Status == status && w.DueUtc >= now),
                _ => query.Where(w => w.Status == status)
            };
        }
        if (!string.IsNullOrWhiteSpace(request.Area)) query = query.Where(w => w.AreaName == request.Area);
        if (!string.IsNullOrWhiteSpace(request.Category)) query = query.Where(w => w.Category == request.Category);
        if (request.AssigneeId is not null) query = query.Where(w => w.AssignedToUserId == request.AssigneeId);

        var total = await query.CountAsync(ct);
        // Open work first, soonest due first; finished and cancelled work sinks.
        var ids = await query
            .OrderBy(w => w.Status == MaintenanceWorkStatus.Verified || w.Status == MaintenanceWorkStatus.Cancelled ? 1 : 0)
            .ThenBy(w => w.DueUtc).ThenByDescending(w => w.MaintenanceWorkItemId)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize)
            .Select(w => w.MaintenanceWorkItemId).ToListAsync(ct);
        var items = await LoadAsync(ids, ct);
        return new MaintenanceWorkPageDto(
            PagedResult<MaintenanceWorkDto>.Create(items, request.Page, request.PageSize, total),
            await SummaryAsync(scoped, date, ct));
    }

    public async Task<MaintenanceWorkDto> GetWorkItemAsync(long id, CancellationToken ct = default)
    {
        if (!await Scoped().AnyAsync(w => w.MaintenanceWorkItemId == id, ct))
        {
            throw new NotFoundException("MaintenanceWorkItem", id);
        }
        return (await LoadAsync([id], ct))[0];
    }

    public async Task<byte[]> ExportWorkAsync(MaintenanceWorkListRequest request, CancellationToken ct = default)
    {
        request.Page = 1;
        request.PageSize = PagedRequest.MaxPageSize;
        var rows = new List<MaintenanceWorkDto>();
        while (true)
        {
            var page = await GetWorkAsync(request, ct);
            rows.AddRange(page.Work.Items);
            if (!page.Work.HasNext || rows.Count >= 10_000) break;
            request.Page++;
        }
        var csv = new StringBuilder();
        csv.AppendLine("Work ID,Title,Area,Category,Priority,Assigned to,Due (IST),Status,Started (IST),Completed (IST),Verified by,Rework count,Completion note");
        foreach (var w in rows)
        {
            csv.AppendLine(string.Join(",", w.WorkNumber, Cell(w.Title), Cell(w.AreaName), Cell(w.Category),
                ((MaintenancePriority)w.Priority).ToString(), Cell(w.AssignedToName), Ist(w.DueUtc),
                StatusLabel(w.Status), Ist(w.StartedUtc), Ist(w.CompletedUtc), Cell(w.VerifiedByName),
                w.ReworkCount.ToString(CultureInfo.InvariantCulture), Cell(w.CompletionNote)));
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }

    public async Task<MaintenanceWorkDto> CreateAsync(SaveMaintenanceWorkRequest request, CancellationToken ct = default)
    {
        Require(CanManage, "Only an admin or supervisor can add work.");
        var now = clock.UtcNow;
        if (request.DueUtc < now.AddMinutes(-1))
        {
            throw Invalid("Due time cannot be in the past.", "dueUtc");
        }
        await EnsureCategoryAsync(request.Category, ct);
        await EnsureActiveHousekeeperAsync(request.AssignedToUserId, ct);
        var locationId = await LocationForAsync(request.WorkspaceId, ct);

        long? scheduleId = null;
        if (request.BlockWorkspace)
        {
            var start = request.BlockFromUtc ?? request.DueUtc.AddHours(-1);
            if (start < now) start = now;
            if (start >= request.DueUtc)
            {
                throw Invalid("The block must start before the due time.", "blockFromUtc");
            }
            var schedule = await housekeeping.CreateAsync(new SaveHousekeepingScheduleRequest(
                request.AssignedToUserId, locationId, request.WorkspaceId, request.AreaName.Trim(), start,
                request.DueUtc, (byte)TaskTypeFor(request.Category), request.Title.Trim(),
                request.Instructions?.Trim(), BlocksWorkspace: true), ct);
            scheduleId = schedule.HousekeepingScheduleId;
        }

        MaintenanceWorkItem work;
        try
        {
            work = new MaintenanceWorkItem
            {
                WorkNumber = await sequences.NextAsync(SequenceKeys.MaintenanceWork, ct),
                Title = request.Title.Trim(),
                LocationId = locationId,
                WorkspaceId = request.WorkspaceId,
                AreaName = request.AreaName.Trim(),
                Category = request.Category.Trim(),
                Priority = (MaintenancePriority)request.Priority,
                AssignedToUserId = request.AssignedToUserId,
                AssignedUtc = now,
                DueUtc = request.DueUtc,
                Instructions = NullIfBlank(request.Instructions),
                LinkedScheduleId = scheduleId
            };
            db.MaintenanceWorkItems.Add(work);
            await db.SaveChangesAsync(ct);
            Audit("MAINT_CREATED", work, new { work.WorkNumber, work.Title, assignedTo = work.AssignedToUserId, due = work.DueUtc, scheduleId });
            if (request.NotifyHousekeeper)
            {
                NotifyAssignee(work, "New task assigned", $"{work.WorkNumber}: {work.Title} · {work.AreaName} · due {IstLabel(work.DueUtc)}.");
            }
            await db.SaveChangesAsync(ct);
        }
        catch when (scheduleId is not null)
        {
            // Never leave a workspace blocked by a schedule whose task failed to save.
            await TryAsync(() => housekeeping.CancelAsync(scheduleId.Value, ct), "cancel orphan schedule");
            throw;
        }
        return await GetWorkItemAsync(work.MaintenanceWorkItemId, ct);
    }

    public async Task<MaintenanceWorkDto> UpdateAsync(long id, SaveMaintenanceWorkRequest request, CancellationToken ct = default)
    {
        Require(CanManage, "Only an admin or supervisor can edit work.");
        var work = await LoadForWriteAsync(id, ct);
        if (work.Status is MaintenanceWorkStatus.Verified or MaintenanceWorkStatus.Cancelled)
        {
            throw new BusinessRuleException("Verified or cancelled work can no longer be edited.");
        }
        if (request.DueUtc != work.DueUtc && request.DueUtc < clock.UtcNow.AddMinutes(-1))
        {
            throw Invalid("Due time cannot be in the past.", "dueUtc");
        }
        await EnsureCategoryAsync(request.Category, ct);
        if (request.AssignedToUserId != work.AssignedToUserId)
        {
            await EnsureActiveHousekeeperAsync(request.AssignedToUserId, ct);
        }
        var reassigned = request.AssignedToUserId != work.AssignedToUserId;
        work.Title = request.Title.Trim();
        work.AreaName = request.AreaName.Trim();
        work.WorkspaceId = request.WorkspaceId;
        work.LocationId = await LocationForAsync(request.WorkspaceId, ct);
        work.Category = request.Category.Trim();
        work.Priority = (MaintenancePriority)request.Priority;
        work.Instructions = NullIfBlank(request.Instructions);
        if (work.DueUtc != request.DueUtc)
        {
            work.DueUtc = request.DueUtc;
            if (work.Status == MaintenanceWorkStatus.Overdue && request.DueUtc > clock.UtcNow)
            {
                work.Status = work.StartedUtc is null ? MaintenanceWorkStatus.Pending : MaintenanceWorkStatus.InProgress;
            }
        }
        if (reassigned)
        {
            work.AssignedToUserId = request.AssignedToUserId;
            work.AssignedUtc = clock.UtcNow;
            NotifyAssignee(work, "Task assigned to you", $"{work.WorkNumber}: {work.Title} · {work.AreaName} · due {IstLabel(work.DueUtc)}.");
        }
        Audit("MAINT_EDITED", work, new { work.Title, work.DueUtc, work.AssignedToUserId });
        await db.SaveChangesAsync(ct);
        await SyncScheduleAsync(work, ct);
        return await GetWorkItemAsync(id, ct);
    }

    public async Task<MaintenanceWorkDto> AssignAsync(long id, long assignedToUserId, CancellationToken ct = default)
    {
        Require(CanManage, "Only an admin or supervisor can assign work.");
        var work = await LoadForWriteAsync(id, ct);
        if (work.Status is MaintenanceWorkStatus.Verified or MaintenanceWorkStatus.Cancelled or MaintenanceWorkStatus.Done)
        {
            throw new BusinessRuleException("Only open work can be reassigned.");
        }
        await EnsureActiveHousekeeperAsync(assignedToUserId, ct);
        var from = work.AssignedToUserId;
        work.AssignedToUserId = assignedToUserId;
        work.AssignedUtc = clock.UtcNow;
        NotifyAssignee(work, "Task assigned to you", $"{work.WorkNumber}: {work.Title} · {work.AreaName} · due {IstLabel(work.DueUtc)}.");
        Audit("MAINT_ASSIGNED", work, new { from, to = assignedToUserId });
        await db.SaveChangesAsync(ct);
        await SyncScheduleAsync(work, ct);
        return await GetWorkItemAsync(id, ct);
    }

    public async Task DeleteAsync(long id, CancellationToken ct = default)
    {
        Require(IsAdmin, "Only an admin can delete work.");
        var work = await LoadForWriteAsync(id, ct);
        if (work.StartedUtc is not null || work.Status is not (MaintenanceWorkStatus.Pending or MaintenanceWorkStatus.Overdue))
        {
            throw new BusinessRuleException("Only work that has not started can be deleted. Cancel it with a reason instead.");
        }
        Audit("MAINT_DELETED", work, new { work.WorkNumber, work.Title });
        db.MaintenanceWorkItems.Remove(work); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
        await CancelLinkedScheduleAsync(work, ct);
    }

    public async Task<MaintenanceWorkDto> CancelAsync(long id, string? reason, CancellationToken ct = default)
    {
        Require(CanManage, "Only an admin or supervisor can cancel work.");
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Invalid("Give a reason for cancelling.", "reason");
        }
        var work = await LoadForWriteAsync(id, ct);
        if (work.Status is MaintenanceWorkStatus.Verified or MaintenanceWorkStatus.Cancelled)
        {
            throw new BusinessRuleException("This work is already closed.");
        }
        work.Status = MaintenanceWorkStatus.Cancelled;
        work.CancelledUtc = clock.UtcNow;
        work.CancelReason = reason.Trim();
        Audit("MAINT_CANCELLED", work, new { reason = work.CancelReason });
        await db.SaveChangesAsync(ct);
        await CancelLinkedScheduleAsync(work, ct);
        return await GetWorkItemAsync(id, ct);
    }

    public async Task<MaintenanceWorkDto> StartAsync(long id, CancellationToken ct = default)
    {
        var work = await LoadForWriteAsync(id, ct);
        RequireWorker(work);
        if (work.Status is not (MaintenanceWorkStatus.Pending or MaintenanceWorkStatus.Overdue or MaintenanceWorkStatus.Rework))
        {
            throw new BusinessRuleException("This task is not waiting to be started.");
        }
        var wasRework = work.Status == MaintenanceWorkStatus.Rework;
        work.Status = MaintenanceWorkStatus.InProgress;
        work.StartedUtc ??= clock.UtcNow;
        Audit(wasRework ? "MAINT_RESTARTED" : "MAINT_STARTED", work, null);
        await db.SaveChangesAsync(ct);
        if (work.LinkedScheduleId is not null)
        {
            var linked = await db.HousekeepingSchedules.AsNoTracking()
                .FirstOrDefaultAsync(s => s.HousekeepingScheduleId == work.LinkedScheduleId, ct);
            if (linked?.Status == HousekeepingStatus.Scheduled)
            {
                await TryAsync(() => housekeeping.CheckInAsync(linked.HousekeepingScheduleId, ct), "start linked schedule");
            }
        }
        return await GetWorkItemAsync(id, ct);
    }

    public async Task<MaintenanceWorkDto> MarkDoneAsync(long id, string? note, AttachmentUpload? photo, CancellationToken ct = default)
    {
        var work = await LoadForWriteAsync(id, ct);
        RequireWorker(work);
        if (work.StartedUtc is null || work.Status is not (MaintenanceWorkStatus.InProgress or MaintenanceWorkStatus.Overdue))
        {
            throw new BusinessRuleException("Start the task before marking it done.");
        }
        if (string.IsNullOrWhiteSpace(note))
        {
            throw Invalid("Add a short note about what you did.", "note");
        }
        if (photo is not null)
        {
            await SavePhotoAsync(work, photo, ct);
        }
        work.Status = MaintenanceWorkStatus.Done;
        work.CompletedUtc = clock.UtcNow;
        work.CompletedByUserId = currentUser.UserId;
        work.CompletionNote = note.Trim()[..Math.Min(note.Trim().Length, 1000)];
        Audit("MAINT_DONE", work, new { note = work.CompletionNote, photo = work.PhotoFileName });
        await NotifySupervisorsAsync("Work waiting for verification",
            $"{work.WorkNumber}: {work.Title} · {work.AreaName} was marked done.", work, ct);
        await db.SaveChangesAsync(ct);
        return await GetWorkItemAsync(id, ct);
    }

    public async Task<MaintenanceWorkDto> VerifyAsync(long id, CancellationToken ct = default)
    {
        Require(CanVerifyAny, "Only a supervisor or admin can verify work.");
        var work = await LoadForWriteAsync(id, ct);
        if (work.Status != MaintenanceWorkStatus.Done)
        {
            throw new BusinessRuleException("Only work marked done can be verified.");
        }
        if (work.CompletedByUserId == currentUser.UserId)
        {
            throw new BusinessRuleException("You cannot verify work you completed yourself; another supervisor or the admin must check it.");
        }
        work.Status = MaintenanceWorkStatus.Verified;
        work.VerifiedUtc = clock.UtcNow;
        work.VerifiedByUserId = currentUser.UserId;
        Audit("MAINT_VERIFIED", work, null);
        await db.SaveChangesAsync(ct);
        if (work.LinkedScheduleId is not null)
        {
            var linked = await db.HousekeepingSchedules.AsNoTracking()
                .FirstOrDefaultAsync(s => s.HousekeepingScheduleId == work.LinkedScheduleId, ct);
            if (linked?.Status == HousekeepingStatus.InProgress)
            {
                await TryAsync(() => housekeeping.CheckOutAsync(linked.HousekeepingScheduleId,
                    new CompleteHousekeepingRequest($"Verified with {work.WorkNumber}"), ct), "complete linked schedule");
            }
        }
        return await GetWorkItemAsync(id, ct);
    }

    public async Task<MaintenanceWorkDto> RejectAsync(long id, string? reason, CancellationToken ct = default)
    {
        Require(CanVerifyAny, "Only a supervisor or admin can send work back.");
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw Invalid("Tell the housekeeper what needs redoing.", "reason");
        }
        var work = await LoadForWriteAsync(id, ct);
        if (work.Status != MaintenanceWorkStatus.Done)
        {
            throw new BusinessRuleException("Only work marked done can be sent back.");
        }
        if (work.CompletedByUserId == currentUser.UserId)
        {
            throw new BusinessRuleException("Another supervisor or the admin must review work you completed.");
        }
        work.Status = MaintenanceWorkStatus.Rework;
        work.ReworkReason = reason.Trim();
        work.ReworkCount++;
        work.CompletedUtc = null;
        work.CompletedByUserId = null;
        Audit("MAINT_REJECTED", work, new { reason = work.ReworkReason });
        NotifyAssignee(work, "Task sent back for rework", $"{work.WorkNumber}: {work.Title} — {work.ReworkReason}");
        await db.SaveChangesAsync(ct);
        return await GetWorkItemAsync(id, ct);
    }

    public async Task<MaintenancePhotoContent> GetPhotoAsync(long id, CancellationToken ct = default)
    {
        var work = await Scoped().FirstOrDefaultAsync(w => w.MaintenanceWorkItemId == id, ct)
                   ?? throw new NotFoundException("MaintenanceWorkItem", id);
        if (work.PhotoPath is null || !File.Exists(work.PhotoPath))
        {
            throw new NotFoundException("MaintenancePhoto", id);
        }
        return new MaintenancePhotoContent(await File.ReadAllBytesAsync(work.PhotoPath, ct),
            work.PhotoContentType ?? "application/octet-stream", work.PhotoFileName ?? "photo");
    }

    // ── My tasks ─────────────────────────────────────────────────────────────

    public async Task<MaintenanceMyTasksDto> GetMyTasksAsync(string? tab, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var mine = db.MaintenanceWorkItems.AsNoTracking().Where(w => w.AssignedToUserId == currentUser.UserId
                                                                     && w.Status != MaintenanceWorkStatus.Cancelled);
        var (from, to) = DayUtc(IstClock.IstDateOf(now));
        var today = mine.Where(w => w.DueUtc >= from && w.DueUtc < to);
        var todayTotal = await today.CountAsync(ct);
        var todayDone = await today.CountAsync(w => w.Status == MaintenanceWorkStatus.Done || w.Status == MaintenanceWorkStatus.Verified, ct);
        var weekAgo = now.AddDays(-7);
        var todo = mine.Where(w => w.Status == MaintenanceWorkStatus.Pending || w.Status == MaintenanceWorkStatus.InProgress
                                   || w.Status == MaintenanceWorkStatus.Overdue);
        var done = mine.Where(w => (w.Status == MaintenanceWorkStatus.Done || w.Status == MaintenanceWorkStatus.Verified)
                                   && (w.CompletedUtc ?? w.VerifiedUtc ?? w.DueUtc) >= weekAgo);
        var rework = mine.Where(w => w.Status == MaintenanceWorkStatus.Rework);
        var selected = (tab ?? "todo").ToLowerInvariant() switch { "done" => done, "rework" => rework, _ => todo };
        var ids = await selected.OrderBy(w => w.DueUtc).Take(100).Select(w => w.MaintenanceWorkItemId).ToListAsync(ct);
        var profile = await FindHousekeeperAsync(currentUser.UserId, ct);
        return new MaintenanceMyTasksDto(profile, todayTotal, todayDone,
            todayTotal == 0 ? 0 : (int)Math.Round(todayDone * 100m / todayTotal),
            await todo.CountAsync(ct), await done.CountAsync(ct), await rework.CountAsync(ct),
            await LoadAsync(ids, ct));
    }

    // ── Housekeepers ─────────────────────────────────────────────────────────

    public async Task<PagedResult<MaintenanceHousekeeperDto>> GetHousekeepersAsync(HousekeeperListRequest request, CancellationToken ct = default)
    {
        Require(!IsHousekeeper, "Housekeepers see their own tasks only.");
        var rows = HousekeeperRows();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(h => h.FullName.Contains(term) || (h.MobileNumber != null && h.MobileNumber.Contains(term))
                                   || (h.HousekeeperCode != null && h.HousekeeperCode.Contains(term)));
        }
        if (request.Shift is not null) rows = rows.Where(h => h.Shift == request.Shift);
        rows = (request.Status ?? "active").ToLowerInvariant() switch
        {
            "inactive" => rows.Where(h => !h.IsActive),
            "all" => rows,
            _ => rows.Where(h => h.IsActive)
        };
        var total = await rows.CountAsync(ct);
        var items = await rows.OrderByDescending(h => h.IsActive).ThenBy(h => h.FullName)
            .Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);
        return PagedResult<MaintenanceHousekeeperDto>.Create(items.Select(r => r.ToDto()).ToList(), request.Page,
            request.PageSize, total);
    }

    public async Task<HousekeeperCreatedDto> CreateHousekeeperAsync(SaveHousekeeperRequest request, CancellationToken ct = default)
    {
        Require(IsAdmin, "Only an admin can add housekeepers.");
        var mobile = request.MobileNumber.Trim();
        var email = request.Email!.Trim().ToLowerInvariant();
        await EnsureUniqueContactAsync(mobile, email, exceptUserId: null, ct);
        var role = await db.Roles.FirstOrDefaultAsync(r => r.RoleCode == RoleCodes.Housekeeping, ct)
                   ?? throw new BusinessRuleException("The Housekeeping Staff role is missing.");
        var tempPassword = TemporaryPassword();
        var now = clock.UtcNow;

        var strategy = db.Database.CreateExecutionStrategy();
        var userId = await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var user = new User
            {
                UserType = UserType.Staff,
                FullName = request.FullName.Trim(),
                Email = email,
                MobileNumber = mobile,
                PasswordHash = passwordHasher.Hash(tempPassword),
                Status = request.IsActive ? UserStatus.Active : UserStatus.Inactive,
                IsEmailVerified = true,
                MustChangePassword = true,
                LastPasswordChangeUtc = now
            };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            db.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = role.RoleId, AssignedDateUtc = now, AssignedBy = currentUser.UserId });
            db.PasswordHistories.Add(new PasswordHistory { UserId = user.UserId, PasswordHash = user.PasswordHash, ChangedUtc = now });
            db.HousekeeperProfiles.Add(new HousekeeperProfile
            {
                UserId = user.UserId,
                HousekeeperCode = await sequences.NextAsync(SequenceKeys.Housekeeper, ct),
                Shift = (HousekeeperShift)request.Shift,
                AssignedAreas = Areas(request.AssignedAreas),
                JoiningDate = request.JoiningDate
            });
            AuditUser("HOUSEKEEPER_CREATED", user.UserId, new { user.FullName, mobile, shift = request.Shift });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return user.UserId;
        });
        var created = (await FindHousekeeperAsync(userId, ct))!;
        return new HousekeeperCreatedDto(created, tempPassword);
    }

    public async Task<MaintenanceHousekeeperDto> UpdateHousekeeperAsync(long userId, SaveHousekeeperRequest request, CancellationToken ct = default)
    {
        Require(IsAdmin, "Only an admin can edit housekeepers.");
        var (user, profile) = await LoadHousekeeperAsync(userId, ct);
        var mobile = request.MobileNumber.Trim();
        var email = request.Email!.Trim().ToLowerInvariant();
        await EnsureUniqueContactAsync(mobile, email, exceptUserId: userId, ct);
        user.FullName = request.FullName.Trim();
        user.MobileNumber = mobile;
        user.Email = email;
        profile.Shift = (HousekeeperShift)request.Shift;
        profile.AssignedAreas = Areas(request.AssignedAreas);
        profile.JoiningDate = request.JoiningDate;
        AuditUser("HOUSEKEEPER_EDITED", userId, new { user.FullName, mobile, shift = request.Shift });
        await db.SaveChangesAsync(ct);
        return (await FindHousekeeperAsync(userId, ct))!;
    }

    public async Task<MaintenanceHousekeeperDto> DeactivateHousekeeperAsync(long userId, long? reassignToUserId, CancellationToken ct = default)
    {
        Require(IsAdmin, "Only an admin can deactivate housekeepers.");
        var (user, _) = await LoadHousekeeperAsync(userId, ct);
        if (userId == currentUser.UserId)
        {
            throw new BusinessRuleException("You cannot deactivate your own account.");
        }
        var open = await db.MaintenanceWorkItems.Where(w => w.AssignedToUserId == userId && OpenStatuses.Contains(w.Status)).ToListAsync(ct);
        if (open.Count > 0)
        {
            if (reassignToUserId is null)
            {
                throw new BusinessRuleException($"{user.FullName} has {open.Count} open task{(open.Count == 1 ? "" : "s")}. Reassign them before deactivating.");
            }
            if (reassignToUserId == userId)
            {
                throw Invalid("Choose a different housekeeper to take the open tasks.", "reassignToUserId");
            }
            await EnsureActiveHousekeeperAsync(reassignToUserId.Value, ct);
            foreach (var w in open)
            {
                w.AssignedToUserId = reassignToUserId.Value;
                w.AssignedUtc = clock.UtcNow;
                NotifyAssignee(w, "Task assigned to you", $"{w.WorkNumber}: {w.Title} · {w.AreaName} · due {IstLabel(w.DueUtc)}.");
            }
        }
        user.Status = UserStatus.Inactive;
        AuditUser("HOUSEKEEPER_DEACTIVATED", userId, new { reassigned = open.Count, to = reassignToUserId });
        await db.SaveChangesAsync(ct);
        return (await FindHousekeeperAsync(userId, ct))!;
    }

    public async Task<MaintenanceHousekeeperDto> ActivateHousekeeperAsync(long userId, CancellationToken ct = default)
    {
        Require(IsAdmin, "Only an admin can reactivate housekeepers.");
        var (user, _) = await LoadHousekeeperAsync(userId, ct);
        user.Status = UserStatus.Active;
        AuditUser("HOUSEKEEPER_ACTIVATED", userId, null);
        await db.SaveChangesAsync(ct);
        return (await FindHousekeeperAsync(userId, ct))!;
    }

    public async Task<HousekeeperWorkLogDto> GetWorkLogAsync(long userId, CancellationToken ct = default)
    {
        Require(!IsHousekeeper || userId == currentUser.UserId, "Housekeepers see their own record only.");
        var row = await FindHousekeeperAsync(userId, ct)
                  ?? throw new NotFoundException("Housekeeper", userId);
        var now = clock.UtcNow;
        var theirs = db.MaintenanceWorkItems.AsNoTracking().Where(w => w.AssignedToUserId == userId && w.Status != MaintenanceWorkStatus.Cancelled);
        var verifiedDurations = await theirs
            .Where(w => w.Status == MaintenanceWorkStatus.Verified && w.StartedUtc != null && w.CompletedUtc != null)
            .Select(w => new { w.StartedUtc, w.CompletedUtc }).ToListAsync(ct);
        int? avg = verifiedDurations.Count == 0 ? null
            : (int)Math.Round(verifiedDurations.Average(d => (d.CompletedUtc!.Value - d.StartedUtc!.Value).TotalMinutes));
        var recentIds = await theirs.OrderByDescending(w => w.DueUtc).Take(25).Select(w => w.MaintenanceWorkItemId).ToListAsync(ct);
        return new HousekeeperWorkLogDto(row,
            await theirs.CountAsync(ct),
            await theirs.CountAsync(w => w.Status == MaintenanceWorkStatus.Verified, ct),
            await theirs.CountAsync(w => w.Status == MaintenanceWorkStatus.Pending || w.Status == MaintenanceWorkStatus.InProgress
                                        || w.Status == MaintenanceWorkStatus.Overdue, ct),
            await theirs.CountAsync(w => w.Status == MaintenanceWorkStatus.Overdue
                                        || ((w.Status == MaintenanceWorkStatus.Pending || w.Status == MaintenanceWorkStatus.InProgress) && w.DueUtc < now), ct),
            await theirs.SumAsync(w => w.ReworkCount, ct),
            avg,
            await LoadAsync(recentIds, ct));
    }

    // ── Schedules ↔ work ─────────────────────────────────────────────────────

    public async Task CreateForScheduleAsync(long scheduleId, CancellationToken ct = default)
    {
        var schedule = await db.HousekeepingSchedules.AsNoTracking()
            .FirstOrDefaultAsync(s => s.HousekeepingScheduleId == scheduleId, ct);
        if (schedule is null || schedule.TaskType is not (HousekeepingTaskType.Housekeeping or HousekeepingTaskType.Cleaning)
            || await db.MaintenanceWorkItems.AnyAsync(w => w.LinkedScheduleId == scheduleId, ct))
        {
            return;
        }
        var area = schedule.WorkspaceId is null ? null
            : await db.Workspaces.AsNoTracking().Where(w => w.WorkspaceId == schedule.WorkspaceId)
                .Select(w => w.WorkspaceName).FirstOrDefaultAsync(ct);
        var work = new MaintenanceWorkItem
        {
            WorkNumber = await sequences.NextAsync(SequenceKeys.MaintenanceWork, ct),
            Title = schedule.TaskDescription,
            LocationId = schedule.LocationId,
            WorkspaceId = schedule.WorkspaceId,
            AreaName = area ?? schedule.AreaName ?? "Unspecified",
            Category = "Cleaning",
            Priority = MaintenancePriority.Medium,
            AssignedToUserId = schedule.StaffUserId,
            AssignedUtc = clock.UtcNow,
            DueUtc = schedule.EndUtc,
            Instructions = schedule.Notes,
            LinkedScheduleId = scheduleId
        };
        db.MaintenanceWorkItems.Add(work);
        await db.SaveChangesAsync(ct);
        Audit("MAINT_CREATED", work, new { fromSchedule = schedule.ScheduleNumber });
        await db.SaveChangesAsync(ct);
    }

    public async Task CancelForScheduleAsync(long scheduleId, CancellationToken ct = default)
    {
        var open = await db.MaintenanceWorkItems
            .Where(w => w.LinkedScheduleId == scheduleId && OpenStatuses.Contains(w.Status)).ToListAsync(ct);
        foreach (var work in open)
        {
            work.Status = MaintenanceWorkStatus.Cancelled;
            work.CancelledUtc = clock.UtcNow;
            work.CancelReason = "Schedule cancelled.";
            Audit("MAINT_CANCELLED", work, new { scheduleId });
        }
        if (open.Count > 0)
        {
            await db.SaveChangesAsync(ct);
        }
    }

    // ── Overdue sweep ────────────────────────────────────────────────────────

    public async Task<int> SweepOverdueAsync(CancellationToken ct = default)
    {
        var grace = await settings.GetIntAsync(AppSettingKeys.Maintenance.OverdueGraceMinutes, 0, ct);
        var cutoff = clock.UtcNow.AddMinutes(-Math.Max(0, grace));
        var late = await db.MaintenanceWorkItems
            .Where(w => (w.Status == MaintenanceWorkStatus.Pending || w.Status == MaintenanceWorkStatus.InProgress) && w.DueUtc < cutoff)
            .ToListAsync(ct);
        if (late.Count == 0)
        {
            return 0;
        }
        var now = clock.UtcNow;
        foreach (var w in late)
        {
            w.Status = MaintenanceWorkStatus.Overdue;
            w.OverdueSinceUtc ??= now;
            db.AuditLogs.Add(new AuditLog
            {
                Action = "MAINT_OVERDUE", ModuleName = "Operations", EntityName = nameof(MaintenanceWorkItem),
                EntityId = w.MaintenanceWorkItemId.ToString(CultureInfo.InvariantCulture),
                NewValue = JsonSerializer.Serialize(new { w.WorkNumber, w.DueUtc }), UserName = "system",
                TimestampUtc = now
            });
        }
        // One alert per sweep, not one per task, so a bad morning is not twenty notifications.
        var sample = string.Join(", ", late.Take(5).Select(w => w.WorkNumber)) + (late.Count > 5 ? $" and {late.Count - 5} more" : "");
        foreach (var userId in await SupervisorIdsAsync(ct))
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId, Audience = NotificationAudience.SingleUser,
                Title = late.Count == 1 ? "Task overdue" : $"{late.Count} tasks overdue",
                Message = $"Past their due time: {sample}.",
                Category = NotificationCategory.Housekeeping, Priority = NotificationPriority.High,
                ReferenceType = "MaintenanceWork", ActionLabel = "Open checklist", ActionRoute = "/admin/housekeeping",
                SentUtc = now
            });
        }
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Maintenance sweep marked {Count} task(s) overdue", late.Count);
        return late.Count;
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>What the caller may see: housekeepers only their own tasks.</summary>
    private IQueryable<MaintenanceWorkItem> Scoped()
    {
        var q = db.MaintenanceWorkItems.AsNoTracking();
        return IsHousekeeper ? q.Where(w => w.AssignedToUserId == currentUser.UserId) : q;
    }

    private async Task<MaintenanceWorkItem> LoadForWriteAsync(long id, CancellationToken ct)
    {
        var work = await db.MaintenanceWorkItems.FirstOrDefaultAsync(w => w.MaintenanceWorkItemId == id, ct)
                   ?? throw new NotFoundException("MaintenanceWorkItem", id);
        if (IsHousekeeper && work.AssignedToUserId != currentUser.UserId)
        {
            throw new NotFoundException("MaintenanceWorkItem", id);
        }
        return work;
    }

    /// <summary>The assignee works their own task; a manager may act for them.</summary>
    private void RequireWorker(MaintenanceWorkItem work) =>
        Require(work.AssignedToUserId == currentUser.UserId || CanManage, "Only the assigned housekeeper or a supervisor can update this task.");

    private async Task<List<MaintenanceWorkDto>> LoadAsync(List<long> ids, CancellationToken ct)
    {
        if (ids.Count == 0) return [];
        var now = clock.UtcNow;
        var rows = await db.MaintenanceWorkItems.AsNoTracking()
            .Where(w => ids.Contains(w.MaintenanceWorkItemId))
            .Select(w => new
            {
                W = w,
                Assignee = db.Users.Where(u => u.UserId == w.AssignedToUserId).Select(u => u.FullName).FirstOrDefault(),
                CompletedBy = db.Users.Where(u => u.UserId == w.CompletedByUserId).Select(u => u.FullName).FirstOrDefault(),
                VerifiedBy = db.Users.Where(u => u.UserId == w.VerifiedByUserId).Select(u => u.FullName).FirstOrDefault(),
                Schedule = db.HousekeepingSchedules.Where(s => s.HousekeepingScheduleId == w.LinkedScheduleId).Select(s => s.ScheduleNumber).FirstOrDefault()
            })
            .ToListAsync(ct);
        var canManage = CanManage;
        var canVerify = CanVerifyAny;
        var isAdmin = IsAdmin;
        return ids.Select(id => rows.First(r => r.W.MaintenanceWorkItemId == id)).Select(r =>
        {
            var w = r.W;
            var status = (w.Status is MaintenanceWorkStatus.Pending or MaintenanceWorkStatus.InProgress) && w.DueUtc < now
                ? MaintenanceWorkStatus.Overdue
                : w.Status;
            var worker = w.AssignedToUserId == currentUser.UserId || canManage;
            return new MaintenanceWorkDto(
                w.MaintenanceWorkItemId, w.WorkNumber, w.Title, w.AreaName, w.WorkspaceId, w.Category, (byte)w.Priority,
                w.AssignedToUserId, r.Assignee ?? "—", w.DueUtc, (byte)status, w.Instructions,
                w.StartedUtc, w.CompletedUtc, r.CompletedBy, w.CompletionNote, w.PhotoPath is not null,
                w.VerifiedUtc, r.VerifiedBy, w.ReworkReason, w.ReworkCount, w.LinkedScheduleId, r.Schedule, w.CancelReason,
                CanStart: worker && (w.Status is MaintenanceWorkStatus.Pending or MaintenanceWorkStatus.Rework
                                     || (w.Status == MaintenanceWorkStatus.Overdue && w.StartedUtc is null)),
                CanMarkDone: worker && w.StartedUtc is not null
                             && w.Status is MaintenanceWorkStatus.InProgress or MaintenanceWorkStatus.Overdue,
                CanVerify: canVerify && w.Status == MaintenanceWorkStatus.Done && w.CompletedByUserId != currentUser.UserId,
                CanEdit: canManage && w.Status is not (MaintenanceWorkStatus.Verified or MaintenanceWorkStatus.Cancelled),
                CanDelete: isAdmin && w.StartedUtc is null && w.Status is MaintenanceWorkStatus.Pending or MaintenanceWorkStatus.Overdue);
        }).ToList();
    }

    private async Task<MaintenanceSummaryDto> SummaryAsync(IQueryable<MaintenanceWorkItem> scoped, DateOnly date, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var (from, to) = DayUtc(date);
        var day = scoped.Where(w => w.DueUtc >= from && w.DueUtc < to && w.Status != MaintenanceWorkStatus.Cancelled);
        var counts = await day.GroupBy(w => w.Status).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
        int Count(MaintenanceWorkStatus s) => counts.FirstOrDefault(c => c.Key == s)?.N ?? 0;
        var total = counts.Sum(c => c.N);
        var verified = Count(MaintenanceWorkStatus.Verified);
        var waiting = Count(MaintenanceWorkStatus.Done);
        var overdue = await day.CountAsync(w => w.Status == MaintenanceWorkStatus.Overdue
            || ((w.Status == MaintenanceWorkStatus.Pending || w.Status == MaintenanceWorkStatus.InProgress) && w.DueUtc < now), ct);
        var categories = await day.Select(w => w.Category).Distinct().CountAsync(ct);
        var shift = await CurrentShiftAsync(ct);
        var active = HousekeeperRows().Where(h => h.IsActive);
        return new MaintenanceSummaryDto(date, total, categories, verified + waiting, verified, waiting, overdue,
            total == 0 ? 0 : (int)Math.Round((verified + waiting) * 100m / total),
            await active.CountAsync(h => h.Shift == (byte)shift, ct), await active.CountAsync(ct), shift.ToString());
    }

    /// <summary>Housekeepers with today's counts, as a filterable server-side query. A
    /// member-init row (not the DTO record) so callers can still Where/OrderBy on it.</summary>
    private IQueryable<HousekeeperRow> HousekeeperRows()
    {
        var (dayStart, dayEnd) = DayUtc(IstClock.IstDateOf(clock.UtcNow));
        var work = db.MaintenanceWorkItems.Where(w => w.Status != MaintenanceWorkStatus.Cancelled);
        return
            from h in db.HousekeeperProfiles.AsNoTracking()
            join u in db.Users.AsNoTracking() on h.UserId equals u.UserId
            select new HousekeeperRow
            {
                UserId = u.UserId,
                HousekeeperCode = h.HousekeeperCode,
                FullName = u.FullName,
                Email = u.Email,
                MobileNumber = u.MobileNumber,
                Shift = (byte)h.Shift,
                AssignedAreas = h.AssignedAreas,
                JoiningDate = h.JoiningDate,
                IsActive = u.Status == UserStatus.Active,
                TodayDone = work.Count(w => w.AssignedToUserId == h.UserId && w.DueUtc >= dayStart && w.DueUtc < dayEnd
                                            && (w.Status == MaintenanceWorkStatus.Done || w.Status == MaintenanceWorkStatus.Verified)),
                TodayPending = work.Count(w => w.AssignedToUserId == h.UserId && w.DueUtc >= dayStart && w.DueUtc < dayEnd
                                               && (w.Status == MaintenanceWorkStatus.Pending || w.Status == MaintenanceWorkStatus.InProgress
                                                   || w.Status == MaintenanceWorkStatus.Overdue)),
                TodayRework = work.Count(w => w.AssignedToUserId == h.UserId && w.DueUtc >= dayStart && w.DueUtc < dayEnd
                                              && w.Status == MaintenanceWorkStatus.Rework),
                VerifiedCount = work.Count(w => w.AssignedToUserId == h.UserId && w.Status == MaintenanceWorkStatus.Verified),
                OnTimeCount = work.Count(w => w.AssignedToUserId == h.UserId && w.Status == MaintenanceWorkStatus.Verified
                                              && w.VerifiedUtc <= w.DueUtc),
                OpenTasks = work.Count(w => w.AssignedToUserId == h.UserId
                                            && (w.Status == MaintenanceWorkStatus.Pending || w.Status == MaintenanceWorkStatus.InProgress
                                                || w.Status == MaintenanceWorkStatus.Overdue || w.Status == MaintenanceWorkStatus.Rework))
            };
    }

    private async Task<MaintenanceHousekeeperDto?> FindHousekeeperAsync(long userId, CancellationToken ct) =>
        (await HousekeeperRows().FirstOrDefaultAsync(h => h.UserId == userId, ct))?.ToDto();

    private sealed class HousekeeperRow
    {
        public long UserId { get; init; }
        public string HousekeeperCode { get; init; } = "";
        public string FullName { get; init; } = "";
        public string? Email { get; init; }
        public string? MobileNumber { get; init; }
        public byte Shift { get; init; }
        public string? AssignedAreas { get; init; }
        public DateOnly JoiningDate { get; init; }
        public bool IsActive { get; init; }
        public int TodayDone { get; init; }
        public int TodayPending { get; init; }
        public int TodayRework { get; init; }
        public int VerifiedCount { get; init; }
        public int OnTimeCount { get; init; }
        public int OpenTasks { get; init; }

        public MaintenanceHousekeeperDto ToDto() => new(UserId, HousekeeperCode, FullName, Email, MobileNumber, Shift,
            AssignedAreas, JoiningDate, IsActive, TodayDone, TodayPending, TodayRework, VerifiedCount, OnTimeCount,
            VerifiedCount == 0 ? null : OnTimeCount * 100 / VerifiedCount, OpenTasks);
    }

    private async Task<(User User, HousekeeperProfile Profile)> LoadHousekeeperAsync(long userId, CancellationToken ct)
    {
        var profile = await db.HousekeeperProfiles.FirstOrDefaultAsync(h => h.UserId == userId, ct)
                      ?? throw new NotFoundException("Housekeeper", userId);
        var user = await db.Users.FirstAsync(u => u.UserId == userId, ct);
        return (user, profile);
    }

    private async Task EnsureActiveHousekeeperAsync(long userId, CancellationToken ct)
    {
        var ok = await (from h in db.HousekeeperProfiles
                        join u in db.Users on h.UserId equals u.UserId
                        where h.UserId == userId && u.Status == UserStatus.Active
                        select h.HousekeeperProfileId).AnyAsync(ct);
        if (!ok)
        {
            throw Invalid("Select an active housekeeper.", "assignedToUserId");
        }
    }

    private async Task EnsureCategoryAsync(string category, CancellationToken ct)
    {
        var allowed = Csv(await settings.GetStringAsync(AppSettingKeys.Maintenance.Categories,
            "Cleaning,Plumbing,Electrical,Laundry,Other", ct));
        if (!allowed.Contains(category.Trim(), StringComparer.OrdinalIgnoreCase))
        {
            throw Invalid("Select an area, a category and a priority.", "category");
        }
    }

    private async Task EnsureUniqueContactAsync(string mobile, string email, long? exceptUserId, CancellationToken ct)
    {
        if (await db.Users.AnyAsync(u => u.MobileNumber == mobile && u.UserId != exceptUserId, ct))
        {
            throw Invalid("This phone number is already registered.", "mobileNumber");
        }
        if (await db.Users.AnyAsync(u => u.Email == email && u.UserId != exceptUserId, ct))
        {
            throw Invalid("This email is already registered.", "email");
        }
    }

    private async Task<int> LocationForAsync(int? workspaceId, CancellationToken ct)
    {
        if (workspaceId is not null)
        {
            return await db.Workspaces.AsNoTracking().Where(w => w.WorkspaceId == workspaceId)
                       .Select(w => (int?)w.LocationId).FirstOrDefaultAsync(ct)
                   ?? throw Invalid("That workspace no longer exists.", "workspaceId");
        }
        return await db.Locations.AsNoTracking().OrderByDescending(l => l.IsDefault).Select(l => l.LocationId).FirstAsync(ct);
    }

    private async Task SavePhotoAsync(MaintenanceWorkItem work, AttachmentUpload photo, CancellationToken ct)
    {
        var ext = Path.GetExtension(photo.FileName);
        if (string.IsNullOrEmpty(ext) || !PhotoTypes.TryGetValue(ext, out var contentType))
        {
            throw new BusinessRuleException("Upload a photo (.jpg, .png or .webp).");
        }
        using var buffer = new MemoryStream();
        await photo.Content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        if (bytes.LongLength is 0 or > MaxPhotoBytes)
        {
            throw new BusinessRuleException("The photo must be under 10 MB.");
        }
        if (!FileTypeSniffer.Matches(ext, bytes))
        {
            throw new BusinessRuleException("The file content does not match its extension.");
        }
        var dir = Path.Combine(storageOptions.Value.UploadRoot, "maintenance", work.MaintenanceWorkItemId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}{ext.ToLowerInvariant()}");
        await File.WriteAllBytesAsync(path, bytes, ct);
        work.PhotoPath = path;
        work.PhotoFileName = Path.GetFileName(photo.FileName);
        work.PhotoContentType = contentType;
    }

    /// <summary>Keeps a linked schedule's window and assignee in step with the task.</summary>
    private async Task SyncScheduleAsync(MaintenanceWorkItem work, CancellationToken ct)
    {
        if (work.LinkedScheduleId is null) return;
        var s = await db.HousekeepingSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.HousekeepingScheduleId == work.LinkedScheduleId, ct);
        if (s is null || s.Status != HousekeepingStatus.Scheduled) return;
        if (s.StaffUserId == work.AssignedToUserId && s.EndUtc == work.DueUtc) return;
        var start = s.StartUtc < work.DueUtc ? s.StartUtc : work.DueUtc.AddHours(-1);
        await TryAsync(() => housekeeping.UpdateAsync(s.HousekeepingScheduleId, new SaveHousekeepingScheduleRequest(
            work.AssignedToUserId, s.LocationId, s.WorkspaceId, s.AreaName, start, work.DueUtc, (byte)s.TaskType,
            s.TaskDescription, s.Notes, s.BlocksWorkspace), ct), "sync linked schedule");
    }

    private async Task CancelLinkedScheduleAsync(MaintenanceWorkItem work, CancellationToken ct)
    {
        if (work.LinkedScheduleId is null) return;
        var s = await db.HousekeepingSchedules.AsNoTracking().FirstOrDefaultAsync(x => x.HousekeepingScheduleId == work.LinkedScheduleId, ct);
        if (s?.Status is HousekeepingStatus.Scheduled or HousekeepingStatus.InProgress)
        {
            await TryAsync(() => housekeeping.CancelAsync(s.HousekeepingScheduleId, ct), "cancel linked schedule");
        }
    }

    private async Task TryAsync(Func<Task> action, string what)
    {
        try
        {
            await action();
        }
        catch (Exception ex) when (ex is BusinessRuleException or NotFoundException or AppValidationException)
        {
            logger.LogWarning(ex, "Maintenance could not {What}", what);
        }
    }

    private async Task<List<long>> SupervisorIdsAsync(CancellationToken ct) =>
        await (from ur in db.UserRoles
               join r in db.Roles on ur.RoleId equals r.RoleId
               join u in db.Users on ur.UserId equals u.UserId
               where (r.RoleCode == RoleCodes.Admin || r.RoleCode == RoleCodes.Manager)
                     && u.Status == UserStatus.Active && u.UserType != UserType.Customer
               select u.UserId).Distinct().ToListAsync(ct);

    private async Task NotifySupervisorsAsync(string title, string message, MaintenanceWorkItem work, CancellationToken ct)
    {
        foreach (var userId in (await SupervisorIdsAsync(ct)).Where(id => id != currentUser.UserId))
        {
            db.Notifications.Add(new Notification
            {
                UserId = userId, Audience = NotificationAudience.SingleUser, Title = title, Message = message,
                Category = NotificationCategory.Housekeeping, ReferenceType = "MaintenanceWork",
                ReferenceId = work.MaintenanceWorkItemId, ActionLabel = "Verify", ActionRoute = "/admin/housekeeping",
                SentUtc = clock.UtcNow
            });
        }
    }

    private void NotifyAssignee(MaintenanceWorkItem work, string title, string message) =>
        db.Notifications.Add(new Notification
        {
            UserId = work.AssignedToUserId, Audience = NotificationAudience.SingleUser, Title = title, Message = message,
            Category = NotificationCategory.Housekeeping,
            Priority = work.Priority == MaintenancePriority.High ? NotificationPriority.High : NotificationPriority.Normal,
            ReferenceType = "MaintenanceWork", ReferenceId = work.MaintenanceWorkItemId == 0 ? null : work.MaintenanceWorkItemId,
            ActionLabel = "My tasks", ActionRoute = "/admin/housekeeping", SentUtc = clock.UtcNow
        });

    private void Audit(string action, MaintenanceWorkItem work, object? details) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.IsAuthenticated ? currentUser.UserId : null,
            UserName = currentUser.FullName ?? currentUser.Email ?? "system",
            Action = action, ModuleName = "Operations", EntityName = nameof(MaintenanceWorkItem),
            EntityId = work.MaintenanceWorkItemId.ToString(CultureInfo.InvariantCulture),
            NewValue = JsonSerializer.Serialize(new { work.WorkNumber, status = work.Status.ToString(), details }),
            IpAddress = currentUser.IpAddress, UserAgent = currentUser.UserAgent, CorrelationId = currentUser.CorrelationId,
            TimestampUtc = clock.UtcNow
        });

    private void AuditUser(string action, long userId, object? details) =>
        db.AuditLogs.Add(new AuditLog
        {
            UserId = currentUser.UserId, UserName = currentUser.FullName ?? currentUser.Email,
            Action = action, ModuleName = "Operations", EntityName = nameof(HousekeeperProfile),
            EntityId = userId.ToString(CultureInfo.InvariantCulture), NewValue = JsonSerializer.Serialize(details),
            IpAddress = currentUser.IpAddress, UserAgent = currentUser.UserAgent, CorrelationId = currentUser.CorrelationId,
            TimestampUtc = clock.UtcNow
        });

    private async Task<HousekeeperShift> CurrentShiftAsync(CancellationToken ct)
    {
        var raw = await settings.GetStringAsync(AppSettingKeys.Maintenance.ShiftHours,
            "Morning=06:00-14:00;Evening=14:00-22:00;Night=22:00-06:00", ct);
        var nowIst = TimeOnly.FromDateTime(IstClock.ToIst(clock.UtcNow));
        foreach (var part in raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var kv = part.Split('=', 2);
            var span = kv.Length == 2 ? kv[1].Split('-') : [];
            if (span.Length == 2 && Enum.TryParse<HousekeeperShift>(kv[0], true, out var shift)
                && TimeOnly.TryParse(span[0], CultureInfo.InvariantCulture, out var a)
                && TimeOnly.TryParse(span[1], CultureInfo.InvariantCulture, out var b)
                && nowIst.IsBetween(a, b))
            {
                return shift;
            }
        }
        return HousekeeperShift.Morning;
    }

    private static HousekeepingTaskType TaskTypeFor(string category) => category.Trim().ToLowerInvariant() switch
    {
        "cleaning" or "laundry" => HousekeepingTaskType.Cleaning,
        "plumbing" or "electrical" => HousekeepingTaskType.Repair,
        _ => HousekeepingTaskType.Housekeeping
    };

    private static (DateTime From, DateTime To) DayUtc(DateOnly date) =>
        (IstClock.ToUtc(date.ToDateTime(TimeOnly.MinValue)), IstClock.ToUtc(date.AddDays(1).ToDateTime(TimeOnly.MinValue)));

    private static string TemporaryPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnopqrstuvwxyz", digits = "23456789", marks = "#@$%";
        var chars = new List<char>
        {
            upper[RandomNumberGenerator.GetInt32(upper.Length)], lower[RandomNumberGenerator.GetInt32(lower.Length)],
            digits[RandomNumberGenerator.GetInt32(digits.Length)], marks[RandomNumberGenerator.GetInt32(marks.Length)]
        };
        var all = upper + lower + digits;
        while (chars.Count < 12) chars.Add(all[RandomNumberGenerator.GetInt32(all.Length)]);
        return new string(chars.OrderBy(_ => RandomNumberGenerator.GetInt32(1000)).ToArray());
    }

    private static string? Areas(IReadOnlyList<string>? areas) =>
        areas is null ? null : NullIfBlank(string.Join(", ", areas.Select(a => a.Trim()).Where(a => a.Length > 0).Distinct()));

    private static List<string> Csv(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    private static string? NullIfBlank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();

    private static AppValidationException Invalid(string message, string field) =>
        new([new ApiError(ErrorCodes.ValidationError, message, field)]);

    private static string IstLabel(DateTime utc) => IstClock.ToIst(utc).ToString("dd MMM, hh:mm tt", CultureInfo.InvariantCulture);
    private static string Ist(DateTime? utc) => utc is null ? "" : IstClock.ToIst(utc.Value).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

    private static string StatusLabel(byte status) => (MaintenanceWorkStatus)status switch
    {
        MaintenanceWorkStatus.InProgress => "In progress",
        MaintenanceWorkStatus.Done => "Done (waiting)",
        var s => s.ToString()
    };

    private static string Cell(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && "=+-@".Contains(text[0])) text = "'" + text;
        return text.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{text.Replace("\"", "\"\"")}\"" : text;
    }
}
