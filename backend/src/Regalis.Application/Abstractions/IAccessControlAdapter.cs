namespace Regalis.Application.Abstractions;

/// <summary>What the physical device reported when asked to open.</summary>
public record DeviceCommandResult(bool IsDeviceOnline, string DeviceStatus);

/// <summary>
/// The access-control hardware seam (A01, Invariant 5). The vendor API is not yet
/// known, so ALL access rules are evaluated by our own AccessService against the
/// database — the adapter only carries the door command and reports device health.
/// Swapping in the real vendor is an adapter implementation, nothing more.
/// </summary>
public interface IAccessControlAdapter
{
    string ProviderName { get; }

    /// <summary>Asks the device to release the door. A failure here never becomes a
    /// silent grant (Invariant 9): the caller logs DeviceError and denies.</summary>
    Task<DeviceCommandResult> RequestDoorOpenAsync(string? deviceId, int locationId,
        CancellationToken ct = default);
}
