using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Masters;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

public class WorkspaceService(RegalisDbContext db) : IWorkspaceService
{
    public async Task<List<WorkspaceTypeDto>> GetWorkspaceTypesAsync(CancellationToken ct = default) =>
        await db.WorkspaceTypes.AsNoTracking()
            .OrderBy(t => t.DisplayOrder)
            .Select(t => new WorkspaceTypeDto(
                t.WorkspaceTypeId, t.TypeCode, t.TypeName, t.IconName, t.DisplayOrder,
                t.RequiresCapacity,
                t.Workspaces.Count(w => !w.IsDeleted)))
            .ToListAsync(ct);

    public async Task<List<AmenityDto>> GetAmenitiesAsync(CancellationToken ct = default) =>
        await db.Amenities.AsNoTracking()
            .OrderBy(a => a.DisplayOrder)
            .Select(a => new AmenityDto(a.AmenityId, a.AmenityName, a.IconName, a.DisplayOrder))
            .ToListAsync(ct);

    public async Task<AmenityDto> CreateAmenityAsync(SaveAmenityRequest request,
        CancellationToken ct = default)
    {
        var name = request.AmenityName.Trim();
        if (await db.Amenities.AnyAsync(a => a.AmenityName == name, ct))
        {
            throw new BusinessRuleException("An amenity with this name already exists.",
                ErrorCodes.DuplicateRecord);
        }
        var amenity = new Amenity
        {
            AmenityName = name,
            IconName = request.IconName?.Trim(),
            DisplayOrder = request.DisplayOrder
        };
        db.Amenities.Add(amenity);
        await db.SaveChangesAsync(ct);
        return new AmenityDto(amenity.AmenityId, amenity.AmenityName, amenity.IconName, amenity.DisplayOrder);
    }

    public async Task<PagedResult<WorkspaceDto>> GetWorkspacesAsync(WorkspaceListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Workspaces.AsNoTracking();
        if (request.LocationId is not null)
        {
            query = query.Where(w => w.LocationId == request.LocationId);
        }
        if (request.WorkspaceTypeId is not null)
        {
            query = query.Where(w => w.WorkspaceTypeId == request.WorkspaceTypeId);
        }
        if (request.Status is not null)
        {
            query = query.Where(w => (byte)w.Status == request.Status);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(w => w.WorkspaceName.Contains(term) || w.WorkspaceCode.Contains(term));
        }
        query = request.IsDescending
            ? query.OrderByDescending(w => w.WorkspaceId)
            : query.OrderBy(w => w.WorkspaceCode);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Include(w => w.Location)
            .Include(w => w.WorkspaceType)
            .Include(w => w.WorkspaceAmenities).ThenInclude(wa => wa.Amenity)
            .Select(w => ToDto(w))
            .ToListAsync(ct);
        return PagedResult<WorkspaceDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<WorkspaceDto> GetWorkspaceAsync(int workspaceId, CancellationToken ct = default)
    {
        var workspace = await LoadAsync(workspaceId, tracking: false, ct);
        return ToDto(workspace);
    }

    public async Task<WorkspaceDto> CreateWorkspaceAsync(SaveWorkspaceRequest request,
        CancellationToken ct = default)
    {
        await ValidateReferencesAsync(request, ct);
        var code = request.WorkspaceCode.Trim().ToUpperInvariant();
        if (await db.Workspaces.AnyAsync(
                w => w.LocationId == request.LocationId && w.WorkspaceCode == code, ct))
        {
            throw new BusinessRuleException(
                "A workspace with this code already exists at the location.",
                ErrorCodes.DuplicateRecord);
        }

        var workspace = new Workspace { WorkspaceCode = code };
        Apply(workspace, request);
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync(ct);
        await SyncAmenitiesAsync(workspace, request.AmenityIds, ct);
        await db.SaveChangesAsync(ct);
        return await GetWorkspaceAsync(workspace.WorkspaceId, ct);
    }

    public async Task<WorkspaceDto> UpdateWorkspaceAsync(int workspaceId, SaveWorkspaceRequest request,
        CancellationToken ct = default)
    {
        await ValidateReferencesAsync(request, ct);
        var workspace = await LoadAsync(workspaceId, tracking: true, ct);

        var code = request.WorkspaceCode.Trim().ToUpperInvariant();
        if (await db.Workspaces.AnyAsync(w =>
                w.LocationId == request.LocationId && w.WorkspaceCode == code &&
                w.WorkspaceId != workspaceId, ct))
        {
            throw new BusinessRuleException(
                "A workspace with this code already exists at the location.",
                ErrorCodes.DuplicateRecord);
        }

        workspace.WorkspaceCode = code;
        Apply(workspace, request);
        await SyncAmenitiesAsync(workspace, request.AmenityIds, ct);
        await db.SaveChangesAsync(ct);
        return await GetWorkspaceAsync(workspaceId, ct);
    }

    public async Task<WorkspaceDto> UpdateWorkspaceStatusAsync(int workspaceId,
        UpdateWorkspaceStatusRequest request, CancellationToken ct = default)
    {
        var workspace = await LoadAsync(workspaceId, tracking: true, ct);
        workspace.Status = (WorkspaceStatus)request.Status;
        await db.SaveChangesAsync(ct);
        return await GetWorkspaceAsync(workspaceId, ct);
    }

    public async Task DeleteWorkspaceAsync(int workspaceId, CancellationToken ct = default)
    {
        var workspace = await LoadAsync(workspaceId, tracking: true, ct);
        var nowUtc = DateTime.UtcNow;
        // Bookings are never deleted (Invariant 3); a workspace with future
        // occupancy cannot silently disappear from under them.
        var hasFutureOccupancy = await db.Bookings.AnyAsync(b =>
            b.WorkspaceId == workspaceId &&
            b.EndUtc > nowUtc &&
            (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed ||
             b.Status == BookingStatus.CheckedIn), ct);
        if (hasFutureOccupancy)
        {
            throw new BusinessRuleException(
                "This workspace has upcoming bookings. Cancel or reschedule them first.");
        }
        db.Workspaces.Remove(workspace); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    private async Task ValidateReferencesAsync(SaveWorkspaceRequest request, CancellationToken ct)
    {
        if (!await db.Locations.AnyAsync(l => l.LocationId == request.LocationId, ct))
        {
            throw new NotFoundException("Location", request.LocationId);
        }
        if (!await db.WorkspaceTypes.AnyAsync(t => t.WorkspaceTypeId == request.WorkspaceTypeId, ct))
        {
            throw new NotFoundException("WorkspaceType", request.WorkspaceTypeId);
        }
        var amenityIds = request.AmenityIds.Distinct().ToArray();
        var found = await db.Amenities.CountAsync(a => amenityIds.Contains(a.AmenityId), ct);
        if (found != amenityIds.Length)
        {
            throw new BusinessRuleException("One or more amenities do not exist.");
        }
    }

    private static void Apply(Workspace workspace, SaveWorkspaceRequest request)
    {
        workspace.LocationId = request.LocationId;
        workspace.WorkspaceTypeId = request.WorkspaceTypeId;
        workspace.WorkspaceName = request.WorkspaceName.Trim();
        workspace.FloorNumber = request.FloorNumber?.Trim();
        workspace.Capacity = request.Capacity;
        workspace.PricePerHour = request.PricePerHour;
        workspace.PricePerDay = request.PricePerDay;
        workspace.PricePerMonth = request.PricePerMonth;
        workspace.Equipment = request.Equipment?.Trim();
        workspace.Description = request.Description?.Trim();
        workspace.IsBookable = request.IsBookable;
    }

    private async Task SyncAmenitiesAsync(Workspace workspace, int[] amenityIds, CancellationToken ct)
    {
        var requested = amenityIds.Distinct().ToHashSet();
        var current = await db.WorkspaceAmenities
            .Where(wa => wa.WorkspaceId == workspace.WorkspaceId)
            .ToListAsync(ct);
        foreach (var stale in current.Where(wa => !requested.Contains(wa.AmenityId)))
        {
            db.WorkspaceAmenities.Remove(stale);
        }
        var currentIds = current.Select(wa => wa.AmenityId).ToHashSet();
        foreach (var amenityId in requested.Except(currentIds))
        {
            db.WorkspaceAmenities.Add(new WorkspaceAmenity
            {
                WorkspaceId = workspace.WorkspaceId,
                AmenityId = amenityId
            });
        }
    }

    private async Task<Workspace> LoadAsync(int workspaceId, bool tracking, CancellationToken ct)
    {
        var query = tracking ? db.Workspaces : db.Workspaces.AsNoTracking();
        return await query
            .Include(w => w.Location)
            .Include(w => w.WorkspaceType)
            .Include(w => w.WorkspaceAmenities).ThenInclude(wa => wa.Amenity)
            .FirstOrDefaultAsync(w => w.WorkspaceId == workspaceId, ct)
            ?? throw new NotFoundException("Workspace", workspaceId);
    }

    private static WorkspaceDto ToDto(Workspace w) => new(
        w.WorkspaceId, w.LocationId, w.Location.LocationName, w.WorkspaceTypeId,
        w.WorkspaceType.TypeName, w.WorkspaceCode, w.WorkspaceName, w.FloorNumber, w.Capacity,
        w.PricePerHour, w.PricePerDay, w.PricePerMonth, w.Equipment, w.Description,
        (byte)w.Status, w.IsBookable,
        w.IsActive,
        w.WorkspaceAmenities
            .Select(wa => new AmenityDto(wa.Amenity.AmenityId, wa.Amenity.AmenityName,
                wa.Amenity.IconName, wa.Amenity.DisplayOrder))
            .ToList());
}
