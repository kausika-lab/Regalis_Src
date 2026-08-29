using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Masters;
using Regalis.Domain.Entities.Masters;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

public class LocationService(RegalisDbContext db) : ILocationService
{
    public async Task<PagedResult<LocationDto>> GetLocationsAsync(PagedRequest request,
        CancellationToken ct = default)
    {
        var query = db.Locations.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(l =>
                l.LocationName.Contains(term) || l.LocationCode.Contains(term) ||
                (l.City != null && l.City.Contains(term)));
        }
        query = request.IsDescending
            ? query.OrderByDescending(l => l.LocationId)
            : query.OrderBy(l => l.LocationId);

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(l => ToDtoExpression(l, l.Workspaces.Count(w => !w.IsDeleted)))
            .ToListAsync(ct);
        return PagedResult<LocationDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<LocationDto> GetLocationAsync(int locationId, CancellationToken ct = default)
    {
        var location = await db.Locations.AsNoTracking()
            .Include(l => l.Workspaces)
            .FirstOrDefaultAsync(l => l.LocationId == locationId, ct)
            ?? throw new NotFoundException("Location", locationId);
        return ToDtoExpression(location, location.Workspaces.Count);
    }

    public async Task<LocationDto> CreateLocationAsync(SaveLocationRequest request,
        CancellationToken ct = default)
    {
        var code = request.LocationCode.Trim().ToUpperInvariant();
        if (await db.Locations.AnyAsync(l => l.LocationCode == code, ct))
        {
            throw new BusinessRuleException("A location with this code already exists.",
                ErrorCodes.DuplicateRecord);
        }

        var location = new Location { LocationCode = code };
        Apply(location, request);
        if (location.IsDefault)
        {
            await ClearOtherDefaultsAsync(0, ct);
        }
        db.Locations.Add(location);
        await db.SaveChangesAsync(ct);
        return ToDtoExpression(location, 0);
    }

    public async Task<LocationDto> UpdateLocationAsync(int locationId, SaveLocationRequest request,
        CancellationToken ct = default)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.LocationId == locationId, ct)
                       ?? throw new NotFoundException("Location", locationId);
        Apply(location, request);
        if (location.IsDefault)
        {
            await ClearOtherDefaultsAsync(locationId, ct);
        }
        await db.SaveChangesAsync(ct);
        return await GetLocationAsync(locationId, ct);
    }

    public async Task DeleteLocationAsync(int locationId, CancellationToken ct = default)
    {
        var location = await db.Locations.FirstOrDefaultAsync(l => l.LocationId == locationId, ct)
                       ?? throw new NotFoundException("Location", locationId);
        if (await db.Workspaces.AnyAsync(w => w.LocationId == locationId, ct))
        {
            throw new BusinessRuleException(
                "This location has workspaces. Move or delete them first.");
        }
        if (location.IsDefault)
        {
            throw new BusinessRuleException("The default location cannot be deleted.");
        }
        db.Locations.Remove(location); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Location location, SaveLocationRequest request)
    {
        location.LocationName = request.LocationName.Trim();
        location.AddressLine1 = request.AddressLine1?.Trim();
        location.AddressLine2 = request.AddressLine2?.Trim();
        location.City = request.City?.Trim();
        location.State = request.State?.Trim();
        location.PostalCode = request.PostalCode?.Trim();
        location.ContactNumber = request.ContactNumber?.Trim();
        location.Email = request.Email?.Trim();
        location.WorkingHoursStart = TimeOnly.Parse(request.WorkingHoursStart);
        location.WorkingHoursEnd = TimeOnly.Parse(request.WorkingHoursEnd);
        location.WorkingDays = string.Join(',', request.WorkingDays.Select(d => d.ToUpperInvariant()));
        location.Latitude = request.Latitude;
        location.Longitude = request.Longitude;
        location.Description = request.Description?.Trim();
        location.IsDefault = request.IsDefault;
    }

    private async Task ClearOtherDefaultsAsync(int exceptLocationId, CancellationToken ct)
    {
        var others = await db.Locations
            .Where(l => l.IsDefault && l.LocationId != exceptLocationId)
            .ToListAsync(ct);
        foreach (var other in others)
        {
            other.IsDefault = false;
        }
    }

    private static LocationDto ToDtoExpression(Location l, int workspaceCount) => new(
        l.LocationId, l.LocationCode, l.LocationName, l.AddressLine1, l.AddressLine2, l.City,
        l.State, l.PostalCode, l.ContactNumber, l.Email,
        l.WorkingHoursStart.ToString("HH:mm"), l.WorkingHoursEnd.ToString("HH:mm"),
        l.WorkingDays, l.Latitude, l.Longitude, l.Description, l.IsDefault, l.IsActive,
        workspaceCount);
}
