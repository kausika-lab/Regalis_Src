using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Regalis.Application.Abstractions;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Security;

/// <summary>This API process. Registered as a singleton, so its BootId lives exactly as long
/// as the process.</summary>
public class ServerInstance(IConfiguration configuration) : IServerInstance
{
    /// <summary>The access-token claim that carries the issuing process's BootId.</summary>
    public const string ClaimType = "boot";

    public string BootId { get; } = Guid.NewGuid().ToString("N");

    public DateTime StartedUtc { get; } = DateTime.UtcNow;

    public bool EndSessionsOnRestart { get; } =
        configuration.GetValue("Auth:EndSessionsOnRestart", true);

    /// <summary>
    /// Run once at startup: revokes every refresh token still open, so no browser can renew
    /// a session that began before this restart. Access tokens are already refused by their
    /// boot claim; this closes the refresh path. Returns the number of tokens revoked.
    /// </summary>
    public async Task<int> EndEarlierSessionsAsync(RegalisDbContext db, CancellationToken ct = default)
    {
        if (!EndSessionsOnRestart) return 0;
        var nowUtc = DateTime.UtcNow;
        return await db.RefreshTokens
            .Where(t => t.RevokedUtc == null && t.ConsumedUtc == null && t.ExpiresUtc > nowUtc)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedUtc, nowUtc)
                .SetProperty(t => t.RevokedReason, "ServerRestart"), ct);
    }
}
