using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Bookings;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

/// <summary>
/// The single availability source (Design Invariant 1): both portals read the same
/// bookings + blocks with the same half-open overlap predicate
/// s1 &lt; e2 AND s2 &lt; e1. These reads are advisory — the booking engine
/// re-verifies under range locks inside its transaction.
/// </summary>
public class AvailabilityService(RegalisDbContext db, ISettingService settings) : IAvailabilityService
{
    public async Task<List<SlotDto>> GetSlotsAsync(int locationId, int workspaceTypeId, DateOnly date,
        CancellationToken ct = default)
    {
        var location = await db.Locations.AsNoTracking()
            .FirstOrDefaultAsync(l => l.LocationId == locationId, ct)
            ?? throw new NotFoundException("Location", locationId);

        var dayCode = date.DayOfWeek.ToString()[..3].ToUpperInvariant();
        if (!location.WorkingDays.Split(',').Contains(dayCode))
        {
            return []; // closed that day
        }

        var granularity = await settings.GetIntAsync(AppSettingKeys.Booking.SlotGranularityMinutes, 30, ct);

        // Working window of the IST date, expressed in UTC.
        var dayStartUtc = IstClock.ToUtc(date.ToDateTime(location.WorkingHoursStart));
        var dayEndUtc = IstClock.ToUtc(date.ToDateTime(location.WorkingHoursEnd));

        var workspaceIds = await BookableWorkspacesQuery(locationId, workspaceTypeId)
            .Select(w => w.WorkspaceId)
            .ToListAsync(ct);
        if (workspaceIds.Count == 0)
        {
            return [];
        }

        var occupancy = await LoadOccupancyAsync(workspaceIds, dayStartUtc, dayEndUtc, ct);
        // Group once instead of re-scanning the whole occupancy list per slot per
        // workspace (Day 9 performance pass: the loop was O(slots × spaces × rows)).
        var occupancyByWorkspace = occupancy
            .GroupBy(o => o.WorkspaceId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var slots = new List<SlotDto>();
        for (var slotStart = dayStartUtc; slotStart < dayEndUtc; slotStart = slotStart.AddMinutes(granularity))
        {
            var slotEnd = slotStart.AddMinutes(granularity);
            var isAvailable = workspaceIds.Any(id =>
                !occupancyByWorkspace.TryGetValue(id, out var spans) ||
                !spans.Any(o => o.StartUtc < slotEnd && slotStart < o.EndUtc));
            slots.Add(new SlotDto(slotStart, slotEnd, isAvailable));
        }
        return slots;
    }

    public async Task<List<AvailableWorkspaceDto>> GetAvailableWorkspacesAsync(int locationId,
        int workspaceTypeId, DateTime startUtc, int durationMinutes, CancellationToken ct = default)
    {
        var endUtc = startUtc.AddMinutes(durationMinutes);
        var workspaces = await BookableWorkspacesQuery(locationId, workspaceTypeId)
            .Include(w => w.WorkspaceType)
            .Include(w => w.WorkspaceAmenities).ThenInclude(wa => wa.Amenity)
            // Two collection includes multiply rows in a single JOIN; split queries
            // keep the payload linear (Day 9 performance pass).
            .AsSplitQuery()
            .ToListAsync(ct);
        if (workspaces.Count == 0)
        {
            return [];
        }

        var occupancy = await LoadOccupancyAsync(
            workspaces.Select(w => w.WorkspaceId).ToList(), startUtc, endUtc, ct);
        var busyIds = occupancy.Select(o => o.WorkspaceId).ToHashSet();

        return workspaces
            .Where(w => !busyIds.Contains(w.WorkspaceId))
            .OrderBy(w => w.WorkspaceCode)
            .Select(w => new AvailableWorkspaceDto(
                w.WorkspaceId, w.WorkspaceCode, w.WorkspaceName, w.WorkspaceType.TypeName,
                w.FloorNumber, w.Capacity, w.PricePerHour, w.Equipment,
                w.WorkspaceAmenities.Select(wa => wa.Amenity.AmenityName).ToList()))
            .ToList();
    }

    public async Task<AvailabilityCheckResponse> CheckAsync(AvailabilityCheckRequest request,
        CancellationToken ct = default)
    {
        var workspace = await db.Workspaces.AsNoTracking()
            .FirstOrDefaultAsync(w => w.WorkspaceId == request.WorkspaceId, ct)
            ?? throw new NotFoundException("Workspace", request.WorkspaceId);

        if (!workspace.IsBookable || workspace.Status != WorkspaceStatus.Available || !workspace.IsActive)
        {
            return new AvailabilityCheckResponse(false, "NotBookable");
        }

        var hasBooking = await db.Bookings.AnyAsync(b =>
            b.WorkspaceId == request.WorkspaceId &&
            Booking.OccupancyStatuses.Contains(b.Status) &&
            b.StartUtc < request.EndUtc && request.StartUtc < b.EndUtc, ct);
        if (hasBooking)
        {
            return new AvailabilityCheckResponse(false, "Booked");
        }

        var hasBlock = await db.WorkspaceBlocks.AnyAsync(k =>
            k.WorkspaceId == request.WorkspaceId && k.IsActive &&
            k.StartUtc < request.EndUtc && request.StartUtc < k.EndUtc, ct);
        return hasBlock
            ? new AvailabilityCheckResponse(false, "Blocked")
            : new AvailabilityCheckResponse(true, null);
    }

    private IQueryable<Domain.Entities.Masters.Workspace> BookableWorkspacesQuery(
        int locationId, int workspaceTypeId) =>
        db.Workspaces.AsNoTracking().Where(w =>
            w.LocationId == locationId &&
            w.WorkspaceTypeId == workspaceTypeId &&
            w.IsBookable &&
            w.IsActive &&
            w.Status == WorkspaceStatus.Available);

    private sealed record Occupancy(int WorkspaceId, DateTime StartUtc, DateTime EndUtc);

    /// <summary>Bookings (Pending/Confirmed/CheckedIn) + active blocks overlapping
    /// the window — the two documented occupancy sources, nothing else.</summary>
    private async Task<List<Occupancy>> LoadOccupancyAsync(List<int> workspaceIds,
        DateTime windowStartUtc, DateTime windowEndUtc, CancellationToken ct)
    {
        var bookings = await db.Bookings.AsNoTracking()
            .Where(b => workspaceIds.Contains(b.WorkspaceId) &&
                        Booking.OccupancyStatuses.Contains(b.Status) &&
                        b.StartUtc < windowEndUtc && windowStartUtc < b.EndUtc)
            .Select(b => new Occupancy(b.WorkspaceId, b.StartUtc, b.EndUtc))
            .ToListAsync(ct);

        var blocks = await db.WorkspaceBlocks.AsNoTracking()
            .Where(k => workspaceIds.Contains(k.WorkspaceId) && k.IsActive &&
                        k.StartUtc < windowEndUtc && windowStartUtc < k.EndUtc)
            .Select(k => new Occupancy(k.WorkspaceId, k.StartUtc, k.EndUtc))
            .ToListAsync(ct);

        bookings.AddRange(blocks);
        return bookings;
    }
}
