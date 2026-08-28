using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Masters;
using Regalis.Domain.Enums;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Persistence.Seed;

/// <summary>
/// Idempotent seeding of the state defined by ROLE_PERMISSION_MATRIX.md and
/// DATABASE_DESIGN.md: safe to run on every startup — existing rows are never
/// overwritten, only missing rows are inserted. The DBA route produces the same
/// state via Database_Seed.sql.
/// </summary>
public class DatabaseSeeder(
    RegalisDbContext db,
    IPasswordHasher passwordHasher,
    IConfiguration configuration,
    IDateTimeProvider clock,
    ILogger<DatabaseSeeder> logger)
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedPermissionsAsync(ct);
        await SeedRolesAsync(ct);
        await SeedRolePermissionsAsync(ct);
        await SeedMenusAsync(ct);
        await SeedRoleMenusAsync(ct);
        await SeedAppSettingsAsync(ct);
        await SeedWorkspaceTypesAsync(ct);
        await SeedTicketCategoriesAsync(ct);
        await SeedNumberSequencesAsync(ct);
        await SeedDefaultLocationAsync(ct);
        await SeedAdminUserAsync(ct);
        logger.LogInformation("Database seed verified");
    }

    private async Task SeedPermissionsAsync(CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.PermissionCode).ToHashSetAsync(ct);
        var order = 0;
        foreach (var (module, actions) in SeedCatalogue.Permissions)
        {
            foreach (var action in actions)
            {
                order++;
                var code = $"{module}.{action}";
                if (existing.Contains(code))
                {
                    continue;
                }
                db.Permissions.Add(new Permission
                {
                    PermissionCode = code,
                    ModuleName = module,
                    ActionName = action,
                    Description = $"{action} on {module}",
                    DisplayOrder = order
                });
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedRolesAsync(CancellationToken ct)
    {
        var existing = await db.Roles.Select(r => r.RoleCode).ToHashSetAsync(ct);
        foreach (var (code, name, description, portal) in SeedCatalogue.Roles)
        {
            if (existing.Contains(code))
            {
                continue;
            }
            db.Roles.Add(new Role
            {
                RoleCode = code,
                RoleName = name,
                Description = description,
                PortalType = portal,
                IsSystemRole = true
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedRolePermissionsAsync(CancellationToken ct)
    {
        var roles = await db.Roles.ToDictionaryAsync(r => r.RoleCode, r => r.RoleId, ct);
        var permissions = await db.Permissions
            .ToDictionaryAsync(p => p.PermissionCode, p => p.PermissionId, ct);
        var permissionsByModule = (await db.Permissions.ToListAsync(ct))
            .GroupBy(p => p.ModuleName)
            .ToDictionary(g => g.Key, g => g.Select(p => p.PermissionId).ToList());
        var existingPairs = await db.RolePermissions
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct);
        var existing = existingPairs.Select(p => (p.RoleId, p.PermissionId)).ToHashSet();

        foreach (var (roleCode, grants) in SeedCatalogue.RolePermissions)
        {
            var roleId = roles[roleCode];
            foreach (var (module, actions) in grants)
            {
                var permissionIds = actions is ["*"]
                    ? permissionsByModule[module]
                    : actions.Select(a => permissions[$"{module}.{a}"]).ToList();
                foreach (var permissionId in permissionIds.Where(id => !existing.Contains((roleId, id))))
                {
                    db.RolePermissions.Add(new RolePermission { RoleId = roleId, PermissionId = permissionId });
                    existing.Add((roleId, permissionId));
                }
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedMenusAsync(CancellationToken ct)
    {
        var existing = await db.Menus.Select(m => m.MenuCode).ToHashSetAsync(ct);
        var permissions = await db.Permissions
            .ToDictionaryAsync(p => p.PermissionCode, p => p.PermissionId, ct);
        var menuIdsByCode = await db.Menus.ToDictionaryAsync(m => m.MenuCode, m => m.MenuId, ct);

        var order = 0;
        foreach (var seed in SeedCatalogue.Menus)
        {
            order += 10;
            if (existing.Contains(seed.Code))
            {
                continue;
            }
            var menu = new Menu
            {
                MenuCode = seed.Code,
                MenuName = seed.DisplayName,
                DisplayName = seed.DisplayName,
                Route = seed.Route,
                Icon = seed.Icon,
                DisplayOrder = order,
                PermissionId = seed.PermissionCode is null ? null : permissions[seed.PermissionCode],
                PortalType = seed.Portal,
                MenuLevel = seed.ParentCode is null ? MenuLevel.Module : MenuLevel.Menu,
                ParentMenuId = seed.ParentCode is null ? null : menuIdsByCode[seed.ParentCode],
                IsVisible = true
            };
            db.Menus.Add(menu);
            // Parents are declared before children in the catalogue; save so the
            // child lookup finds the id.
            await db.SaveChangesAsync(ct);
            menuIdsByCode[seed.Code] = menu.MenuId;
        }
    }

    private async Task SeedRoleMenusAsync(CancellationToken ct)
    {
        var roles = await db.Roles.ToDictionaryAsync(r => r.RoleCode, r => r.RoleId, ct);
        var menus = await db.Menus.ToDictionaryAsync(m => m.MenuCode, m => m.MenuId, ct);
        var existingPairs = await db.RoleMenus
            .Select(rm => new { rm.RoleId, rm.MenuId })
            .ToListAsync(ct);
        var existing = existingPairs.Select(p => (p.RoleId, p.MenuId)).ToHashSet();

        foreach (var seed in SeedCatalogue.Menus)
        {
            var menuId = menus[seed.Code];
            foreach (var roleId in seed.Roles.Select(code => roles[code])
                         .Where(roleId => !existing.Contains((roleId, menuId))))
            {
                db.RoleMenus.Add(new RoleMenu { RoleId = roleId, MenuId = menuId });
                existing.Add((roleId, menuId));
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedAppSettingsAsync(CancellationToken ct)
    {
        var existing = await db.AppSettings.Select(s => s.SettingKey).ToHashSetAsync(ct);
        foreach (var (key, value, group, dataType, description) in SeedCatalogue.AppSettings)
        {
            if (existing.Contains(key))
            {
                continue;
            }
            db.AppSettings.Add(new AppSetting
            {
                SettingKey = key,
                SettingValue = value,
                SettingGroup = group,
                DataType = dataType,
                Description = description,
                IsEditableByAdmin = true
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedWorkspaceTypesAsync(CancellationToken ct)
    {
        var existing = await db.WorkspaceTypes.Select(t => t.TypeCode).ToHashSetAsync(ct);
        foreach (var (code, name, icon, displayOrder, requiresCapacity) in SeedCatalogue.WorkspaceTypes)
        {
            if (existing.Contains(code))
            {
                continue;
            }
            db.WorkspaceTypes.Add(new WorkspaceType
            {
                TypeCode = code,
                TypeName = name,
                IconName = icon,
                DisplayOrder = displayOrder,
                RequiresCapacity = requiresCapacity
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedTicketCategoriesAsync(CancellationToken ct)
    {
        var existingCategories = await db.TicketCategories
            .Include(c => c.IssueTypes)
            .ToDictionaryAsync(c => c.CategoryName, ct);

        var categoryOrder = 0;
        foreach (var (categoryName, issueTypes) in SeedCatalogue.TicketCategories)
        {
            categoryOrder += 10;
            if (!existingCategories.TryGetValue(categoryName, out var category))
            {
                category = new TicketCategory { CategoryName = categoryName, DisplayOrder = categoryOrder };
                db.TicketCategories.Add(category);
            }

            var existingTypes = category.IssueTypes.Select(t => t.IssueTypeName).ToHashSet();
            var typeOrder = 0;
            foreach (var issueType in issueTypes)
            {
                typeOrder += 10;
                if (!existingTypes.Contains(issueType))
                {
                    category.IssueTypes.Add(new TicketIssueType
                    {
                        IssueTypeName = issueType,
                        DisplayOrder = typeOrder
                    });
                }
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedNumberSequencesAsync(CancellationToken ct)
    {
        // Indian financial year: April–March, e.g. "2026-27".
        var nowIst = clock.UtcNow.AddHours(5.5);
        var fyStart = nowIst.Month >= 4 ? nowIst.Year : nowIst.Year - 1;
        var financialYear = $"{fyStart}-{(fyStart + 1) % 100:D2}";

        var existingRows = await db.NumberSequences
            .Select(s => new { s.SequenceKey, s.FinancialYear })
            .ToListAsync(ct);
        var existing = existingRows.Select(s => (s.SequenceKey, s.FinancialYear)).ToHashSet();

        foreach (var (key, prefix, fy) in SeedCatalogue.NumberSequences)
        {
            var resolvedFy = fy == "FY" ? financialYear : fy;
            if (existing.Contains((key, resolvedFy)))
            {
                continue;
            }
            db.NumberSequences.Add(new NumberSequence
            {
                SequenceKey = key,
                Prefix = prefix,
                FinancialYear = resolvedFy,
                CurrentValue = 0
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedDefaultLocationAsync(CancellationToken ct)
    {
        if (await db.Locations.AnyAsync(ct))
        {
            return;
        }
        // Phase 1 runs one live location (out-of-scope register). Details are
        // editable data — Regalis Ops confirms address and hours (A-register).
        db.Locations.Add(new Location
        {
            LocationCode = "REG-HQ",
            LocationName = "Regalis WorkSpace",
            WorkingHoursStart = new TimeOnly(8, 0),
            WorkingHoursEnd = new TimeOnly(21, 0),
            WorkingDays = "MON,TUE,WED,THU,FRI,SAT",
            IsDefault = true
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedAdminUserAsync(CancellationToken ct)
    {
        const string adminEmail = "enquiry@amstag.in"; // ROLE_PERMISSION_MATRIX §3
        var admin = await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail, ct);

        if (admin is null)
        {
            var password = configuration["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(password))
            {
                // No password, no admin — never seed a known default credential.
                logger.LogError(
                    "Seed:AdminPassword is not configured; the admin user was NOT seeded. " +
                    "Set it via user-secrets or environment variables and restart.");
                return;
            }

            admin = new User
            {
                UserType = UserType.Admin,
                FullName = "Regalis Administrator",
                Email = adminEmail,
                MobileNumber = "0000000000",
                PasswordHash = passwordHasher.Hash(password),
                Status = UserStatus.Active,
                IsEmailVerified = true,
                MustChangePassword = true,
                DefaultLocationId = await db.Locations
                    .Where(l => l.IsDefault)
                    .Select(l => (int?)l.LocationId)
                    .FirstOrDefaultAsync(ct)
            };
            db.Users.Add(admin);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Seeded administrator {Email} (MustChangePassword = true)", adminEmail);
        }

        // Heal role + history even when the user row already exists — a previous
        // seed run may have been interrupted between the two saves.
        var adminRoleId = await db.Roles
            .Where(r => r.RoleCode == RoleCodes.Admin)
            .Select(r => r.RoleId)
            .FirstAsync(ct);
        if (!await db.UserRoles.AnyAsync(ur => ur.UserId == admin.UserId && ur.RoleId == adminRoleId, ct))
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = admin.UserId,
                RoleId = adminRoleId,
                AssignedDateUtc = clock.UtcNow
            });
        }
        if (!await db.PasswordHistories.AnyAsync(h => h.UserId == admin.UserId, ct))
        {
            db.PasswordHistories.Add(new PasswordHistory
            {
                UserId = admin.UserId,
                PasswordHash = admin.PasswordHash,
                ChangedUtc = clock.UtcNow
            });
        }
        await db.SaveChangesAsync(ct);
    }
}
