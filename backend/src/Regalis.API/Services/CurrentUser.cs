using Regalis.API.Middleware;
using Regalis.Application.Abstractions;
using Regalis.Domain.Enums;

namespace Regalis.API.Services;

/// <summary>Resolves the caller's identity from the JWT claims on the current request.
/// Claims are raw (MapInboundClaims is off): sub, email, name, user_type, loc, role, perm.</summary>
public class CurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private HttpContext? Context => httpContextAccessor.HttpContext;

    public bool IsAuthenticated => Context?.User.Identity?.IsAuthenticated == true;

    public long UserId =>
        long.TryParse(Context?.User.FindFirst("sub")?.Value, out var id) ? id : 0;

    public string? Email => Context?.User.FindFirst("email")?.Value;

    public string? FullName => Context?.User.FindFirst("name")?.Value;

    public UserType? UserType =>
        byte.TryParse(Context?.User.FindFirst("user_type")?.Value, out var t)
            ? (UserType)t
            : null;

    public int? LocationId =>
        int.TryParse(Context?.User.FindFirst("loc")?.Value, out var loc) ? loc : null;

    public IReadOnlySet<string> Roles =>
        Context?.User.FindAll("role").Select(c => c.Value).ToHashSet() ?? [];

    public IReadOnlySet<string> Permissions =>
        Context?.User.FindAll("perm").Select(c => c.Value).ToHashSet() ?? [];

    public string? IpAddress => Context?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent => Context?.Request.Headers.UserAgent.ToString();

    public string? CorrelationId => Context?.Items[CorrelationIdMiddleware.ItemKey]?.ToString();

    public bool HasPermission(string permissionCode) =>
        Context?.User.HasClaim("perm", permissionCode) == true;
}
