using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Auth;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services;

/// <summary>Builds the caller's sidebar from MST_Menu + MAP_RoleMenu. Adding a screen
/// is a data insert — no redeploy (ARCHITECTURE §5).</summary>
public class MenuQueryService(RegalisDbContext db, ICurrentUser currentUser) : IMenuQueryService
{
    public async Task<List<MenuNodeDto>> GetMyMenusAsync(CancellationToken ct = default)
    {
        var roleCodes = currentUser.Roles;
        if (roleCodes.Count == 0)
        {
            return [];
        }

        var menus = await db.RoleMenus
            .Where(rm => roleCodes.Contains(rm.Role.RoleCode) &&
                         rm.Menu.IsVisible && rm.Menu.IsActive)
            .Select(rm => rm.Menu)
            .Distinct()
            .OrderBy(m => m.DisplayOrder)
            .ToListAsync(ct);

        var nodes = menus.ToDictionary(
            m => m.MenuId,
            m => new MenuNodeDto(m.MenuId, m.ParentMenuId, m.MenuCode, m.DisplayName,
                m.Route, m.Icon, m.DisplayOrder, (byte)m.MenuLevel, []));

        var roots = new List<MenuNodeDto>();
        foreach (var menu in menus)
        {
            var node = nodes[menu.MenuId];
            if (menu.ParentMenuId is not null && nodes.TryGetValue(menu.ParentMenuId.Value, out var parent))
            {
                parent.Children.Add(node);
            }
            else
            {
                roots.Add(node);
            }
        }
        return roots;
    }
}
