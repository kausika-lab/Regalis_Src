using Regalis.Domain.Enums;

namespace Regalis.Application.Abstractions;

/// <summary>
/// The identity of the caller, resolved from the JWT by the API layer.
/// Row-level scoping in services goes through this — never through a
/// client-supplied customer id (ROLE_PERMISSION_MATRIX §6).
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    long UserId { get; }
    string? Email { get; }
    string? FullName { get; }
    UserType? UserType { get; }
    int? LocationId { get; }
    IReadOnlySet<string> Roles { get; }
    IReadOnlySet<string> Permissions { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string? CorrelationId { get; }

    bool HasPermission(string permissionCode);
}
