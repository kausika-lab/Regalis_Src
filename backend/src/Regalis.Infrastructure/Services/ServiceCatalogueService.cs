using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Masters;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>Services &amp; Coffee catalogue (M22). Customers browse available items;
/// the admin grid manages the full list.</summary>
public class ServiceCatalogueService(RegalisDbContext db) : IServiceCatalogueService
{
    public async Task<PagedResult<ServiceItemDto>> GetItemsAsync(ServiceItemListRequest request,
        CancellationToken ct = default)
    {
        var query = db.ServiceItems.AsNoTracking();
        if (request.IncludeUnavailable != true)
        {
            query = query.Where(s => s.IsAvailable);
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(s => s.ItemName.Contains(term) || s.ItemCode.Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(request.Category))
        {
            query = query.Where(s => s.Category == request.Category);
        }
        if (request.LocationId is not null)
        {
            query = query.Where(s => s.LocationId == null || s.LocationId == request.LocationId);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(s => s.Category).ThenBy(s => s.DisplayOrder).ThenBy(s => s.ItemName)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(s => ToDto(s))
            .ToListAsync(ct);
        return PagedResult<ServiceItemDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default) =>
        await db.ServiceItems.AsNoTracking()
            .Where(s => s.IsAvailable)
            .Select(s => s.Category)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

    public async Task<ServiceItemDto> GetItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await db.ServiceItems.AsNoTracking()
            .FirstOrDefaultAsync(s => s.ServiceItemId == itemId, ct)
            ?? throw new NotFoundException("ServiceItem", itemId);
        return ToDto(item);
    }

    public async Task<ServiceItemDto> CreateItemAsync(SaveServiceItemRequest request,
        CancellationToken ct = default)
    {
        var code = request.ItemCode.Trim().ToUpperInvariant();
        if (await db.ServiceItems.AnyAsync(s => s.ItemCode == code, ct))
        {
            throw new ConflictException($"Item code {code} is already in use.");
        }
        await EnsureLocationExistsAsync(request.LocationId, ct);
        var item = new Domain.Entities.Masters.ServiceItem { ItemCode = code };
        Apply(item, request);
        db.ServiceItems.Add(item);
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task<ServiceItemDto> UpdateItemAsync(int itemId, SaveServiceItemRequest request,
        CancellationToken ct = default)
    {
        var item = await db.ServiceItems
            .FirstOrDefaultAsync(s => s.ServiceItemId == itemId, ct)
            ?? throw new NotFoundException("ServiceItem", itemId);
        await EnsureLocationExistsAsync(request.LocationId, ct);
        Apply(item, request);
        await db.SaveChangesAsync(ct);
        return ToDto(item);
    }

    public async Task DeleteItemAsync(int itemId, CancellationToken ct = default)
    {
        var item = await db.ServiceItems
            .FirstOrDefaultAsync(s => s.ServiceItemId == itemId, ct)
            ?? throw new NotFoundException("ServiceItem", itemId);
        if (await db.ServiceOrderLines.AnyAsync(l => l.ServiceItemId == itemId, ct))
        {
            throw new BusinessRuleException(
                "This item appears on orders and cannot be deleted — mark it unavailable instead.");
        }
        db.ServiceItems.Remove(item); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureLocationExistsAsync(int? locationId, CancellationToken ct)
    {
        if (locationId is not null &&
            !await db.Locations.AnyAsync(l => l.LocationId == locationId, ct))
        {
            throw new NotFoundException("Location", locationId);
        }
    }

    private static void Apply(Domain.Entities.Masters.ServiceItem item, SaveServiceItemRequest request)
    {
        item.ItemName = request.ItemName.Trim();
        item.Category = request.Category.Trim();
        item.Price = request.Price;
        item.Unit = request.Unit?.Trim();
        item.ImagePath = request.ImagePath?.Trim();
        item.Description = request.Description?.Trim();
        item.IsAvailable = request.IsAvailable;
        item.DisplayOrder = request.DisplayOrder;
        item.LocationId = request.LocationId;
    }

    private static ServiceItemDto ToDto(Domain.Entities.Masters.ServiceItem s) => new(
        s.ServiceItemId, s.ItemCode, s.ItemName, s.Category, s.Price, s.Unit, s.ImagePath,
        s.Description, s.IsAvailable, s.DisplayOrder, s.LocationId, s.IsActive);
}
