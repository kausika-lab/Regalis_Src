using Microsoft.AspNetCore.Authorization;

namespace Regalis.API.Authorization;

/// <summary>
/// Guards a controller action with a permission code, e.g.
/// <c>[HasPermission(PermissionCodes.Booking.Create)]</c>. The policy is created on
/// demand by <see cref="PermissionPolicyProvider"/> — no policy is registered by hand.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class HasPermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "PERMISSION:";

    public HasPermissionAttribute(string permissionCode)
        : base($"{PolicyPrefix}{permissionCode}")
    {
        PermissionCode = permissionCode;
    }

    public string PermissionCode { get; }
}
