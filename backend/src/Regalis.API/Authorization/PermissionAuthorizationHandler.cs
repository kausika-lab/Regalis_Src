using Microsoft.AspNetCore.Authorization;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Identity;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared.Constants;

namespace Regalis.API.Authorization;

/// <summary>
/// Grants when the JWT carries the required "perm" claim (stamped at login from the
/// database). A valid token without the permission gets 403 and an
/// AUTHORIZATION_FAILED audit row (ROLE_PERMISSION_MATRIX §6).
/// </summary>
public class PermissionAuthorizationHandler(
    RegalisDbContext dbContext,
    ICurrentUser currentUser,
    IDateTimeProvider clock,
    ILogger<PermissionAuthorizationHandler> logger)
    : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.User.HasClaim("perm", requirement.PermissionCode))
        {
            context.Succeed(requirement);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            logger.LogWarning(
                "Authorization failed: user {UserId} lacks permission {Permission}",
                currentUser.UserId, requirement.PermissionCode);

            dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = currentUser.UserId,
                UserName = currentUser.FullName,
                Action = AuditActions.AuthorizationFailed,
                ModuleName = requirement.PermissionCode.Split('.')[0],
                EntityName = "Permission",
                EntityId = requirement.PermissionCode,
                IpAddress = currentUser.IpAddress,
                UserAgent = currentUser.UserAgent,
                CorrelationId = currentUser.CorrelationId,
                TimestampUtc = clock.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }
    }
}
