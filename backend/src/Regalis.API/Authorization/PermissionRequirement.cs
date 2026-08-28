using Microsoft.AspNetCore.Authorization;

namespace Regalis.API.Authorization;

public class PermissionRequirement(string permissionCode) : IAuthorizationRequirement
{
    public string PermissionCode { get; } = permissionCode;
}
