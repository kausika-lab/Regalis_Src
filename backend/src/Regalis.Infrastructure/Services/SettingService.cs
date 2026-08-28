using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Regalis.Application.Abstractions;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services;

/// <summary>Reads MST_AppSetting with a short memory cache so hot paths (booking
/// rules, security policy) don't hit the database on every request.</summary>
public class SettingService(RegalisDbContext dbContext, IMemoryCache cache) : ISettingService
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromSeconds(60);

    public async Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default)
    {
        var value = await GetRawAsync(key, ct);
        return value ?? fallback;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var value = await GetRawAsync(key, ct);
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public async Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default)
    {
        var value = await GetRawAsync(key, ct);
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
    {
        var value = await GetRawAsync(key, ct);
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private async Task<string?> GetRawAsync(string key, CancellationToken ct)
    {
        return await cache.GetOrCreateAsync($"appsetting:{key}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CacheTtl;
            return await dbContext.AppSettings
                .Where(s => s.SettingKey == key)
                .Select(s => s.SettingValue)
                .FirstOrDefaultAsync(ct);
        });
    }
}
