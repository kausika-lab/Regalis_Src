using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Masters;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services;

/// <summary>Home-screen promotional banners (M23).</summary>
public class BannerService(
    RegalisDbContext db,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IBannerService
{
    public async Task<PagedResult<BannerDto>> GetBannersAsync(PagedRequest request,
        CancellationToken ct = default)
    {
        var query = db.Banners.AsNoTracking();
        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderBy(b => b.DisplayOrder).ThenByDescending(b => b.BannerId)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(b => ToDto(b))
            .ToListAsync(ct);
        return PagedResult<BannerDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<List<BannerDto>> GetActiveBannersAsync(int? locationId,
        CancellationToken ct = default)
    {
        var nowUtc = clock.UtcNow;
        // The carousel shows the caller's portal: customers never see admin banners.
        var portal = currentUser.UserType == UserType.Customer ? PortalType.Customer : PortalType.Admin;
        return await db.Banners.AsNoTracking()
            .Where(b => b.ValidFromUtc <= nowUtc && nowUtc < b.ValidToUtc &&
                        (b.PortalType == portal || b.PortalType == PortalType.Both) &&
                        (locationId == null || b.LocationId == null || b.LocationId == locationId))
            .OrderBy(b => b.DisplayOrder).ThenByDescending(b => b.BannerId)
            .Select(b => ToDto(b))
            .ToListAsync(ct);
    }

    public async Task<BannerDto> CreateBannerAsync(SaveBannerRequest request,
        CancellationToken ct = default)
    {
        await EnsureLocationExistsAsync(request.LocationId, ct);
        var banner = new Banner();
        Apply(banner, request);
        db.Banners.Add(banner);
        await db.SaveChangesAsync(ct);
        return ToDto(banner);
    }

    public async Task<BannerDto> UpdateBannerAsync(int bannerId, SaveBannerRequest request,
        CancellationToken ct = default)
    {
        var banner = await db.Banners
            .FirstOrDefaultAsync(b => b.BannerId == bannerId, ct)
            ?? throw new NotFoundException("Banner", bannerId);
        await EnsureLocationExistsAsync(request.LocationId, ct);
        Apply(banner, request);
        await db.SaveChangesAsync(ct);
        return ToDto(banner);
    }

    public async Task DeleteBannerAsync(int bannerId, CancellationToken ct = default)
    {
        var banner = await db.Banners
            .FirstOrDefaultAsync(b => b.BannerId == bannerId, ct)
            ?? throw new NotFoundException("Banner", bannerId);
        db.Banners.Remove(banner); // interceptor converts to soft delete
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

    private static void Apply(Banner banner, SaveBannerRequest request)
    {
        banner.Title = request.Title.Trim();
        banner.Subtitle = request.Subtitle?.Trim();
        banner.ImagePath = request.ImagePath.Trim();
        banner.LinkUrl = request.LinkUrl?.Trim();
        banner.PromotionalPrice = request.PromotionalPrice;
        banner.DisplayOrder = request.DisplayOrder;
        banner.ValidFromUtc = request.ValidFromUtc;
        banner.ValidToUtc = request.ValidToUtc;
        banner.LocationId = request.LocationId;
        banner.PortalType = (PortalType)request.PortalType;
    }

    private static BannerDto ToDto(Banner b) => new(
        b.BannerId, b.Title, b.Subtitle, b.ImagePath, b.LinkUrl, b.PromotionalPrice,
        b.DisplayOrder, b.ValidFromUtc, b.ValidToUtc, b.LocationId, (byte)b.PortalType, b.IsActive);
}
