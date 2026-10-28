using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Administration;
using Regalis.Application.Common.Exceptions;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.Infrastructure.Services;

public class UserAdminService(
    RegalisDbContext db,
    IPasswordHasher passwordHasher,
    ICurrentUser currentUser,
    IDateTimeProvider clock) : IUserAdminService
{
    public async Task<PagedResult<UserListItemDto>> GetUsersAsync(UserListRequest request,
        CancellationToken ct = default)
    {
        var query = db.Users.AsNoTracking();

        if (request.UserType is not null)
        {
            query = query.Where(u => (byte)u.UserType == request.UserType);
        }
        if (request.Status is not null)
        {
            query = query.Where(u => (byte)u.Status == request.Status);
        }
        if (request.RoleId is not null)
        {
            query = query.Where(u => u.UserRoles.Any(ur => ur.RoleId == request.RoleId));
        }
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(u =>
                u.FullName.Contains(term) || u.Email.Contains(term) ||
                u.MobileNumber.Contains(term) || (u.CompanyName != null && u.CompanyName.Contains(term)));
        }

        query = (request.SortBy?.ToLowerInvariant(), request.IsDescending) switch
        {
            ("fullname", false) => query.OrderBy(u => u.FullName),
            ("fullname", true) => query.OrderByDescending(u => u.FullName),
            ("email", false) => query.OrderBy(u => u.Email),
            ("email", true) => query.OrderByDescending(u => u.Email),
            ("lastloginutc", false) => query.OrderBy(u => u.LastLoginUtc),
            ("lastloginutc", true) => query.OrderByDescending(u => u.LastLoginUtc),
            (_, false) => query.OrderBy(u => u.CreatedDate),
            _ => query.OrderByDescending(u => u.CreatedDate)
        };

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(u => new UserListItemDto(
                u.UserId, u.FullName, u.Email, u.MobileNumber, (byte)u.UserType, (byte)u.Status,
                u.CompanyName,
                u.UserRoles.Select(ur => ur.Role.RoleName).ToList(),
                u.LastLoginUtc, u.CreatedDate, u.IsActive))
            .ToListAsync(ct);

        return PagedResult<UserListItemDto>.Create(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<UserDetailDto> GetUserAsync(long userId, CancellationToken ct = default)
    {
        var user = await db.Users.AsNoTracking()
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new NotFoundException("User", userId);
        return ToDetail(user);
    }

    public async Task<UserDetailDto> CreateUserAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        var email = request.Email.Trim();
        var mobile = request.MobileNumber.Trim();
        if (await db.Users.AnyAsync(u => u.Email == email || u.MobileNumber == mobile, ct))
        {
            throw new BusinessRuleException("A user with this email or mobile number already exists.",
                ErrorCodes.DuplicateRecord);
        }
        await ValidateRoleAssignmentAsync(request.RoleIds, (UserType)request.UserType, ct);

        var user = new User
        {
            UserType = (UserType)request.UserType,
            FullName = request.FullName.Trim(),
            Email = email,
            MobileNumber = mobile,
            CompanyName = request.CompanyName?.Trim(),
            DefaultLocationId = request.DefaultLocationId,
            PasswordHash = passwordHasher.Hash(request.Password),
            Status = UserStatus.Active,
            IsEmailVerified = true,
            MustChangePassword = true
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        foreach (var roleId in request.RoleIds.Distinct())
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = roleId,
                AssignedDateUtc = clock.UtcNow,
                AssignedBy = currentUser.UserId
            });
        }
        db.PasswordHistories.Add(new PasswordHistory
        {
            UserId = user.UserId,
            PasswordHash = user.PasswordHash,
            ChangedUtc = clock.UtcNow
        });
        if (user.UserType == UserType.Customer)
        {
            db.Wallets.Add(new Wallet { CustomerId = user.UserId });
        }
        await AdminAlerts.AccountAsync(db, user, "New profile created", "was created",
            clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);

        return await GetUserAsync(user.UserId, ct);
    }

    public async Task<UserDetailDto> UpdateUserAsync(long userId, UpdateUserRequest request,
        CancellationToken ct = default)
    {
        var user = await db.Users
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.UserId == userId, ct)
            ?? throw new NotFoundException("User", userId);

        var mobile = request.MobileNumber.Trim();
        if (await db.Users.AnyAsync(u => u.MobileNumber == mobile && u.UserId != userId, ct))
        {
            throw new BusinessRuleException("Another user already uses this mobile number.",
                ErrorCodes.DuplicateRecord);
        }
        await ValidateRoleAssignmentAsync(request.RoleIds, user.UserType, ct);

        user.FullName = request.FullName.Trim();
        user.MobileNumber = mobile;
        user.CompanyName = request.CompanyName?.Trim();
        user.Address = request.Address?.Trim();
        user.DefaultLocationId = request.DefaultLocationId;

        var requested = request.RoleIds.Distinct().ToHashSet();
        var current = user.UserRoles.Select(ur => ur.RoleId).ToHashSet();
        foreach (var stale in user.UserRoles.Where(ur => !requested.Contains(ur.RoleId)).ToList())
        {
            db.UserRoles.Remove(stale);
        }
        foreach (var roleId in requested.Except(current))
        {
            db.UserRoles.Add(new UserRole
            {
                UserId = user.UserId,
                RoleId = roleId,
                AssignedDateUtc = clock.UtcNow,
                AssignedBy = currentUser.UserId
            });
        }

        await AdminAlerts.AccountAsync(db, user, "Profile information changed",
            "was changed by an administrator", clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
        return await GetUserAsync(userId, ct);
    }

    public Task ActivateAsync(long userId, CancellationToken ct = default) =>
        SetStatusAsync(userId, UserStatus.Active, ct);

    public Task DeactivateAsync(long userId, CancellationToken ct = default) =>
        SetStatusAsync(userId, UserStatus.Inactive, ct);

    public Task LockAsync(long userId, CancellationToken ct = default) =>
        SetStatusAsync(userId, UserStatus.Locked, ct);

    public async Task UnlockAsync(long userId, CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct);
        user.Status = UserStatus.Active;
        user.LockoutEndUtc = null;
        user.FailedLoginCount = 0;
        await AdminAlerts.AccountAsync(db, user, "Account unlocked", "was unlocked",
            clock.UtcNow, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task ResetPasswordAsync(long userId, AdminResetPasswordRequest request,
        CancellationToken ct = default)
    {
        var user = await RequireUserAsync(userId, ct);
        var nowUtc = clock.UtcNow;

        user.PasswordHash = passwordHasher.Hash(request.NewPassword);
        user.LastPasswordChangeUtc = nowUtc;
        user.MustChangePassword = true;
        db.PasswordHistories.Add(new PasswordHistory
        {
            UserId = user.UserId,
            PasswordHash = user.PasswordHash,
            ChangedUtc = nowUtc
        });

        var activeTokens = await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedUtc == null && t.ConsumedUtc == null)
            .ToListAsync(ct);
        foreach (var token in activeTokens)
        {
            token.RevokedUtc = nowUtc;
            token.RevokedReason = "AdminPasswordReset";
        }
        await AdminAlerts.AccountAsync(db, user, "Password reset",
            "had its password reset by an administrator", nowUtc, ct,
            NotificationPriority.High);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(long userId, CancellationToken ct = default)
    {
        if (userId == currentUser.UserId)
        {
            throw new BusinessRuleException("You cannot delete your own account.");
        }
        var user = await RequireUserAsync(userId, ct);
        db.Users.Remove(user); // interceptor converts to soft delete
        await db.SaveChangesAsync(ct);
    }

    private async Task SetStatusAsync(long userId, UserStatus status, CancellationToken ct)
    {
        if (userId == currentUser.UserId && status != UserStatus.Active)
        {
            throw new BusinessRuleException("You cannot deactivate or lock your own account.");
        }
        var user = await RequireUserAsync(userId, ct);
        user.Status = status;
        var (title, action) = status switch
        {
            UserStatus.Active => ("Account activated", "was activated"),
            UserStatus.Locked => ("Account locked", "was locked"),
            _ => ("Account deactivated", "was deactivated")
        };
        await AdminAlerts.AccountAsync(db, user, title, action, clock.UtcNow, ct,
            status == UserStatus.Locked ? NotificationPriority.High : NotificationPriority.Normal);
        await db.SaveChangesAsync(ct);
    }

    private async Task<User> RequireUserAsync(long userId, CancellationToken ct) =>
        await db.Users.FirstOrDefaultAsync(u => u.UserId == userId, ct)
        ?? throw new NotFoundException("User", userId);

    /// <summary>Roles must exist and match the user's portal type: a Customer user
    /// cannot receive an admin-portal role and vice versa.</summary>
    private async Task ValidateRoleAssignmentAsync(int[] roleIds, UserType userType, CancellationToken ct)
    {
        var roles = await db.Roles.Where(r => roleIds.Contains(r.RoleId)).ToListAsync(ct);
        if (roles.Count != roleIds.Distinct().Count())
        {
            throw new BusinessRuleException("One or more roles do not exist.");
        }
        var expectedPortal = userType == UserType.Customer ? PortalType.Customer : PortalType.Admin;
        if (roles.Any(r => r.PortalType != expectedPortal && r.PortalType != PortalType.Both))
        {
            throw new BusinessRuleException(
                $"Role portal mismatch: a {userType} user can only hold {expectedPortal}-portal roles.");
        }
    }

    private static UserDetailDto ToDetail(User user) => new(
        user.UserId, user.FullName, user.Email, user.MobileNumber, (byte)user.UserType,
        (byte)user.Status, user.CompanyName, user.Address, user.PhotoPath, user.DefaultLocationId,
        user.IsEmailVerified, user.IsMobileVerified, user.MustChangePassword,
        user.LastLoginUtc, user.LockoutEndUtc,
        user.UserRoles.Select(ur => ur.RoleId).ToList(),
        user.UserRoles.Select(ur => ur.Role.RoleName).ToList(),
        user.CreatedDate, user.IsActive,
        user.Gstin, user.DateOfBirth, (byte?)user.Gender,
        user.AddressLine1, user.AddressLine2, user.City, user.State, user.PostalCode, user.Country,
        user.IsProfileComplete);
}
