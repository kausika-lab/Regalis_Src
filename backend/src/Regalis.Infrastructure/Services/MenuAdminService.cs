using Microsoft.EntityFrameworkCore;
using Regalis.Application.Administration;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

public class MenuAdminService(RegalisDbContext db) : IMenuAdminService
{
    public async Task<List<MenuDto>> GetMenusAsync(byte? portalType, CancellationToken ct = default)
    {
        var query = db.Menus.AsNoTracking();
        if (portalType is not null)
        {
            query = query.Where(m => (byte)m.PortalType == portalType);
        }
        return await query
            .OrderBy(m => m.PortalType).ThenBy(m => m.DisplayOrder)
            .Select(m => new MenuDto(
                m.MenuId, m.ParentMenuId, m.MenuCode, m.MenuName, m.DisplayName, m.Route,
                m.Icon, m.DisplayOrder, m.PermissionId, (byte)m.PortalType, (byte)m.MenuLevel,
                m.IsVisible, m.IsActive))
            .ToListAsync(ct);
    }

    public async Task<MenuDto> CreateMenuAsync(CreateMenuRequest request, CancellationToken ct = default)
    {
        var code = request.MenuCode.Trim().ToUpperInvariant();
        if (await db.Menus.AnyAsync(m => m.MenuCode == code, ct))
        {
            throw new BusinessRuleException("A menu with this code already exists.",
                ErrorCodes.DuplicateRecord);
        }

        var level = MenuLevel.Module;
        if (request.ParentMenuId is not null)
        {
            var parent = await db.Menus.FirstOrDefaultAsync(m => m.MenuId == request.ParentMenuId, ct)
                         ?? throw new NotFoundException("Menu", request.ParentMenuId);
            if (parent.MenuLevel == MenuLevel.Screen)
            {
                throw new BusinessRuleException("A screen-level menu cannot have children.");
            }
            level = (MenuLevel)((byte)parent.MenuLevel + 1);
        }

        var menu = new Menu
        {
            ParentMenuId = request.ParentMenuId,
            MenuCode = code,
            MenuName = request.MenuName.Trim(),
            DisplayName = request.DisplayName.Trim(),
            Route = NormalizeRoute(request.Route),
            Icon = request.Icon?.Trim(),
            DisplayOrder = request.DisplayOrder,
            PermissionId = request.PermissionId,
            PortalType = (PortalType)request.PortalType,
            MenuLevel = level,
            IsVisible = request.IsVisible
        };
        db.Menus.Add(menu);
        await db.SaveChangesAsync(ct);
        return ToDto(menu);
    }

    public async Task<MenuDto> UpdateMenuAsync(int menuId, UpdateMenuRequest request,
        CancellationToken ct = default)
    {
        var menu = await db.Menus.FirstOrDefaultAsync(m => m.MenuId == menuId, ct)
                   ?? throw new NotFoundException("Menu", menuId);

        if (request.ParentMenuId is not null)
        {
            if (request.ParentMenuId == menuId)
            {
                throw new BusinessRuleException("A menu cannot be its own parent.");
            }
            // The self-FK is NO ACTION; cycles are prevented here in code.
            var ancestorId = request.ParentMenuId;
            while (ancestorId is not null)
            {
                if (ancestorId == menuId)
                {
                    throw new BusinessRuleException("This parent assignment would create a cycle.");
                }
                ancestorId = await db.Menus
                    .Where(m => m.MenuId == ancestorId)
                    .Select(m => m.ParentMenuId)
                    .FirstOrDefaultAsync(ct);
            }
        }

        menu.ParentMenuId = request.ParentMenuId;
        menu.MenuName = request.MenuName.Trim();
        menu.DisplayName = request.DisplayName.Trim();
        menu.Route = NormalizeRoute(request.Route);
        menu.Icon = request.Icon?.Trim();
        menu.DisplayOrder = request.DisplayOrder;
        menu.PermissionId = request.PermissionId;
        menu.PortalType = (PortalType)request.PortalType;
        menu.IsVisible = request.IsVisible;
        await db.SaveChangesAsync(ct);
        return ToDto(menu);
    }

    public async Task DeleteMenuAsync(int menuId, CancellationToken ct = default)
    {
        var menu = await db.Menus.FirstOrDefaultAsync(m => m.MenuId == menuId, ct)
                   ?? throw new NotFoundException("Menu", menuId);
        if (await db.Menus.AnyAsync(m => m.ParentMenuId == menuId, ct))
        {
            throw new BusinessRuleException("Delete or move the child menus first.");
        }
        db.Menus.Remove(menu); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    private static string? NormalizeRoute(string? route)
    {
        var trimmed = route?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }
        return trimmed.StartsWith('/') ? trimmed : $"/{trimmed}";
    }

    private static MenuDto ToDto(Menu m) => new(
        m.MenuId, m.ParentMenuId, m.MenuCode, m.MenuName, m.DisplayName, m.Route, m.Icon,
        m.DisplayOrder, m.PermissionId, (byte)m.PortalType, (byte)m.MenuLevel, m.IsVisible, m.IsActive);
}
