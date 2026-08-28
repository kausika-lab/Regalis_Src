using Microsoft.EntityFrameworkCore;
using Regalis.Application.Administration;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

public class RoleAdminService(RegalisDbContext db) : IRoleAdminService
{
    public async Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default) =>
        await db.Roles.AsNoTracking()
            .OrderBy(r => r.RoleId)
            .Select(r => new RoleDto(
                r.RoleId, r.RoleCode, r.RoleName, r.Description, (byte)r.PortalType,
                r.IsSystemRole, r.IsActive,
                r.UserRoles.Count,
                r.RolePermissions.Count))
            .ToListAsync(ct);

    public async Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        var code = request.RoleCode.Trim().ToUpperInvariant();
        if (await db.Roles.AnyAsync(r => r.RoleCode == code, ct))
        {
            throw new BusinessRuleException("A role with this code already exists.",
                ErrorCodes.DuplicateRecord);
        }
        var role = new Role
        {
            RoleCode = code,
            RoleName = request.RoleName.Trim(),
            Description = request.Description?.Trim(),
            PortalType = (PortalType)request.PortalType,
            IsSystemRole = false
        };
        db.Roles.Add(role);
        await db.SaveChangesAsync(ct);
        return new RoleDto(role.RoleId, role.RoleCode, role.RoleName, role.Description,
            (byte)role.PortalType, role.IsSystemRole, role.IsActive, 0, 0);
    }

    public async Task<RoleDto> UpdateRoleAsync(int roleId, UpdateRoleRequest request,
        CancellationToken ct = default)
    {
        var role = await RequireRoleAsync(roleId, ct);
        role.RoleName = request.RoleName.Trim();
        role.Description = request.Description?.Trim();
        role.PortalType = (PortalType)request.PortalType;
        if (role.IsSystemRole && !request.IsActive &&
            role.RoleCode is RoleCodes.Admin or RoleCodes.Customer)
        {
            throw new BusinessRuleException("The ADMIN and CUSTOMER roles cannot be deactivated.");
        }
        role.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        return new RoleDto(role.RoleId, role.RoleCode, role.RoleName, role.Description,
            (byte)role.PortalType, role.IsSystemRole, role.IsActive,
            await db.UserRoles.CountAsync(ur => ur.RoleId == roleId, ct),
            await db.RolePermissions.CountAsync(rp => rp.RoleId == roleId, ct));
    }

    public async Task DeleteRoleAsync(int roleId, CancellationToken ct = default)
    {
        var role = await RequireRoleAsync(roleId, ct);
        if (role.IsSystemRole)
        {
            throw new BusinessRuleException("System roles cannot be deleted.");
        }
        if (await db.UserRoles.AnyAsync(ur => ur.RoleId == roleId, ct))
        {
            throw new BusinessRuleException(
                "This role is assigned to users. Remove the assignments first.");
        }
        db.Roles.Remove(role); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<RolePermissionRowDto>> GetRolePermissionsAsync(int roleId,
        CancellationToken ct = default)
    {
        await RequireRoleAsync(roleId, ct);
        var granted = await db.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Select(rp => rp.PermissionId)
            .ToHashSetAsync(ct);
        var catalogue = await db.Permissions.AsNoTracking()
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);
        return catalogue
            .Select(p => new RolePermissionRowDto(
                p.PermissionId, p.PermissionCode, p.ModuleName, p.ActionName,
                granted.Contains(p.PermissionId)))
            .ToList();
    }

    public async Task UpdateRolePermissionsAsync(int roleId, UpdateRolePermissionsRequest request,
        CancellationToken ct = default)
    {
        await RequireRoleAsync(roleId, ct);
        var requested = request.PermissionIds.Distinct().ToHashSet();

        var validIds = await db.Permissions
            .Where(p => requested.Contains(p.PermissionId))
            .Select(p => p.PermissionId)
            .ToHashSetAsync(ct);
        if (validIds.Count != requested.Count)
        {
            throw new BusinessRuleException("One or more permissions do not exist.");
        }

        var current = await db.RolePermissions.Where(rp => rp.RoleId == roleId).ToListAsync(ct);
        foreach (var stale in current.Where(rp => !requested.Contains(rp.PermissionId)))
        {
            db.RolePermissions.Remove(stale);
        }
        var currentIds = current.Select(rp => rp.PermissionId).ToHashSet();
        foreach (var permissionId in requested.Except(currentIds))
        {
            db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
        }
        // Takes effect within one access-token lifetime (15 min) or on refresh —
        // permissions are stamped into the JWT at issue time.
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<RoleMenuRowDto>> GetRoleMenusAsync(int roleId, CancellationToken ct = default)
    {
        var role = await RequireRoleAsync(roleId, ct);
        var granted = await db.RoleMenus
            .Where(rm => rm.RoleId == roleId)
            .Select(rm => rm.MenuId)
            .ToHashSetAsync(ct);
        return await db.Menus.AsNoTracking()
            .Where(m => role.PortalType == PortalType.Both || m.PortalType == role.PortalType)
            .OrderBy(m => m.DisplayOrder)
            .Select(m => new RoleMenuRowDto(
                m.MenuId, m.MenuCode, m.DisplayName, (byte)m.PortalType, granted.Contains(m.MenuId)))
            .ToListAsync(ct);
    }

    public async Task UpdateRoleMenusAsync(int roleId, UpdateRoleMenusRequest request,
        CancellationToken ct = default)
    {
        await RequireRoleAsync(roleId, ct);
        var requested = request.MenuIds.Distinct().ToHashSet();

        var validIds = await db.Menus
            .Where(m => requested.Contains(m.MenuId))
            .Select(m => m.MenuId)
            .ToHashSetAsync(ct);
        if (validIds.Count != requested.Count)
        {
            throw new BusinessRuleException("One or more menus do not exist.");
        }

        var current = await db.RoleMenus.Where(rm => rm.RoleId == roleId).ToListAsync(ct);
        foreach (var stale in current.Where(rm => !requested.Contains(rm.MenuId)))
        {
            db.RoleMenus.Remove(stale);
        }
        var currentIds = current.Select(rm => rm.MenuId).ToHashSet();
        foreach (var menuId in requested.Except(currentIds))
        {
            db.RoleMenus.Add(new RoleMenu { RoleId = roleId, MenuId = menuId });
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<PermissionDto>> GetPermissionCatalogueAsync(CancellationToken ct = default) =>
        await db.Permissions.AsNoTracking()
            .OrderBy(p => p.DisplayOrder)
            .Select(p => new PermissionDto(p.PermissionId, p.PermissionCode, p.ModuleName, p.ActionName))
            .ToListAsync(ct);

    private async Task<Role> RequireRoleAsync(int roleId, CancellationToken ct) =>
        await db.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId, ct)
        ?? throw new NotFoundException("Role", roleId);
}
