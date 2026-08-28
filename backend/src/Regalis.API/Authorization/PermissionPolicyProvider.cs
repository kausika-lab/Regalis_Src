using System.Collections.Concurrent;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Regalis.API.Authorization;

/// <summary>
/// Creates an AuthorizationPolicy on demand for any [HasPermission("MODULE.ACTION")]
/// attribute (ARCHITECTURE §5). Non-permission policies fall back to the default provider.
/// </summary>
public class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _cache = new();

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (!policyName.StartsWith(HasPermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            return _fallback.GetPolicyAsync(policyName);
        }

        var policy = _cache.GetOrAdd(policyName, name =>
        {
            var permissionCode = name[HasPermissionAttribute.PolicyPrefix.Length..];
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permissionCode))
                .Build();
        });
        return Task.FromResult<AuthorizationPolicy?>(policy);
    }
}
