using Microsoft.AspNetCore.Authorization;
using Regalis.API.Auditing;
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

            var reason = $"Missing permission: {requirement.PermissionCode}";
            // Audit Trail §4.4: the attempted action (e.g. DELETE on Quotation #11), Denied,
            // with the permission that was missing. Refused reads keep the
            // AUTHORIZATION_FAILED action on the permission itself (EXPORT for exports).
            var row = context.Resource is HttpContext http && AuditOutcome.IsMutation(http.Request.Method)
                ? AuditOutcome.Build(http, currentUser, clock.UtcNow, AuditResults.Denied, reason)
                : new AuditLog
                {
                    UserId = currentUser.UserId,
                    UserName = currentUser.FullName,
                    Action = context.Resource is HttpContext get
                             && get.Request.Path.Value?.Contains("export", StringComparison.OrdinalIgnoreCase) == true
                        ? AuditActions.Export
                        : AuditActions.AuthorizationFailed,
                    ModuleName = requirement.PermissionCode.Split('.')[0],
                    EntityName = "Permission",
                    EntityId = requirement.PermissionCode,
                    IpAddress = currentUser.IpAddress,
                    UserAgent = currentUser.UserAgent is { Length: > 300 } ua ? ua[..300] : currentUser.UserAgent,
                    CorrelationId = currentUser.CorrelationId,
                    TimestampUtc = clock.UtcNow,
                    Result = AuditResults.Denied,
                    FailureReason = reason
                };
            dbContext.AuditLogs.Add(row);
            await dbContext.SaveChangesAsync();
        }
    }
}
