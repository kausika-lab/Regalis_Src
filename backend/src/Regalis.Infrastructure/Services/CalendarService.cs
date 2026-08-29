using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Bookings;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The single calendar (Design Invariant 1): one data source — bookings + blocks —
/// rendered for both portals. Customers get anonymized titles for other customers'
/// bookings; admin-portal users see numbers and names.
/// </summary>
public class CalendarService(
    RegalisDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : ICalendarService
{
    public async Task<CalendarResponse> GetCalendarAsync(string view, DateOnly date, int? locationId,
        int? workspaceTypeId, int? workspaceId, CancellationToken ct = default)
    {
        var (rangeStartIst, rangeEndIst) = view.ToLowerInvariant() switch
        {
            "day" => (date, date.AddDays(1)),
            "week" => WeekRange(date),
            "month" => (new DateOnly(date.Year, date.Month, 1),
                new DateOnly(date.Year, date.Month, 1).AddMonths(1)),
            _ => throw new BusinessRuleException("view must be day, week or month.")
        };
        var rangeStartUtc = IstClock.ToUtc(rangeStartIst.ToDateTime(TimeOnly.MinValue));
        var rangeEndUtc = IstClock.ToUtc(rangeEndIst.ToDateTime(TimeOnly.MinValue));

        var workspacesQuery = db.Workspaces.AsNoTracking()
            .Include(w => w.WorkspaceType)
            .Where(w => w.IsActive);
        if (locationId is not null)
        {
            workspacesQuery = workspacesQuery.Where(w => w.LocationId == locationId);
        }
        if (workspaceTypeId is not null)
        {
            workspacesQuery = workspacesQuery.Where(w => w.WorkspaceTypeId == workspaceTypeId);
        }
        if (workspaceId is not null)
        {
            workspacesQuery = workspacesQuery.Where(w => w.WorkspaceId == workspaceId);
        }
        var workspaces = await workspacesQuery.OrderBy(w => w.WorkspaceCode).ToListAsync(ct);
        var workspaceIds = workspaces.Select(w => w.WorkspaceId).ToList();

        var isAdminView = currentUser.UserType != UserType.Customer;

        var bookings = await db.Bookings.AsNoTracking()
            .Include(b => b.Customer)
            .Where(b => workspaceIds.Contains(b.WorkspaceId) &&
                        Booking.OccupancyStatuses.Contains(b.Status) &&
                        b.StartUtc < rangeEndUtc && rangeStartUtc < b.EndUtc)
            .ToListAsync(ct);

        var blocks = await db.WorkspaceBlocks.AsNoTracking()
            .Where(k => workspaceIds.Contains(k.WorkspaceId) && k.IsActive &&
                        k.StartUtc < rangeEndUtc && rangeStartUtc < k.EndUtc)
            .ToListAsync(ct);

        var result = workspaces.Select(w => new CalendarWorkspaceDto(
            w.WorkspaceId, w.WorkspaceCode, w.WorkspaceName, w.WorkspaceType.TypeName,
            bookings.Where(b => b.WorkspaceId == w.WorkspaceId)
                .Select(b => new CalendarItemDto(
                    "booking", b.BookingId, b.StartUtc, b.EndUtc,
                    Title: isAdminView
                        ? $"{b.BookingNumber} · {b.Customer.FullName}"
                        : b.CustomerId == currentUser.UserId
                            ? $"Your booking {b.BookingNumber}"
                            : "Reserved",
                    BookingStatus: (byte)b.Status,
                    BlockReason: null,
                    IsOwn: b.CustomerId == currentUser.UserId))
                .Concat(blocks.Where(k => k.WorkspaceId == w.WorkspaceId)
                    .Select(k => new CalendarItemDto(
                        "block", k.WorkspaceBlockId, k.StartUtc, k.EndUtc,
                        Title: isAdminView
                            ? $"Blocked · {k.BlockReason}{(k.ReasonNotes is null ? "" : $" — {k.ReasonNotes}")}"
                            : "Unavailable",
                        BookingStatus: null,
                        BlockReason: (byte)k.BlockReason,
                        IsOwn: false)))
                .OrderBy(i => i.StartUtc)
                .ToList()))
            .ToList();

        return new CalendarResponse(view.ToLowerInvariant(), rangeStartUtc, rangeEndUtc, result);
    }

    public async Task<BlockDto> BlockAsync(CreateBlockRequest request, CancellationToken ct = default)
    {
        var workspace = await db.Workspaces.AsNoTracking()
            .FirstOrDefaultAsync(w => w.WorkspaceId == request.WorkspaceId, ct)
            ?? throw new NotFoundException("Workspace", request.WorkspaceId);

        // A block must not silently swallow paid bookings — they are cancelled or
        // rescheduled first, by a human decision.
        var overlappingBookings = await db.Bookings.AnyAsync(b =>
            b.WorkspaceId == request.WorkspaceId &&
            Booking.OccupancyStatuses.Contains(b.Status) &&
            b.StartUtc < request.EndUtc && request.StartUtc < b.EndUtc, ct);
        if (overlappingBookings)
        {
            throw new ConflictException(
                "Customer bookings exist in this window. Cancel or reschedule them before blocking.");
        }

        var block = new WorkspaceBlock
        {
            WorkspaceId = workspace.WorkspaceId,
            LocationId = workspace.LocationId,
            StartUtc = request.StartUtc,
            EndUtc = request.EndUtc,
            BlockReason = (BlockReason)request.BlockReason,
            ReasonNotes = request.ReasonNotes?.Trim(),
            BlockedByUserId = currentUser.UserId
        };
        db.WorkspaceBlocks.Add(block);
        await db.SaveChangesAsync(ct);
        return ToDto(block, workspace.WorkspaceName);
    }

    public async Task<BlockDto> UnblockAsync(long blockId, CancellationToken ct = default)
    {
        var block = await db.WorkspaceBlocks
            .Include(k => k.Workspace)
            .FirstOrDefaultAsync(k => k.WorkspaceBlockId == blockId, ct)
            ?? throw new NotFoundException("WorkspaceBlock", blockId);
        if (!block.IsActive)
        {
            throw new BusinessRuleException("This block is already released.");
        }
        // Unblock = release, keep the row (DATABASE_DESIGN §5).
        block.IsActive = false;
        block.ReleasedUtc = clock.UtcNow;
        block.ReleasedByUserId = currentUser.UserId;
        await db.SaveChangesAsync(ct);
        return ToDto(block, block.Workspace.WorkspaceName);
    }

    private static (DateOnly Start, DateOnly End) WeekRange(DateOnly date)
    {
        var daysSinceMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var monday = date.AddDays(-daysSinceMonday);
        return (monday, monday.AddDays(7));
    }

    private static BlockDto ToDto(WorkspaceBlock block, string workspaceName) => new(
        block.WorkspaceBlockId, block.WorkspaceId, workspaceName, block.StartUtc, block.EndUtc,
        (byte)block.BlockReason, block.ReasonNotes, block.IsActive);
}
