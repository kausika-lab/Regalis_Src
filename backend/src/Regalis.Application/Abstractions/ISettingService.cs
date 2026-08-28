namespace Regalis.Application.Abstractions;

/// <summary>
/// Typed access to MST_AppSetting — the "no hard-coding" table (Invariant 6).
/// Values are cached briefly; an admin edit takes effect within the cache window.
/// </summary>
public interface ISettingService
{
    Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default);
    Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default);
    Task<decimal> GetDecimalAsync(string key, decimal fallback, CancellationToken ct = default);
    Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default);
}
