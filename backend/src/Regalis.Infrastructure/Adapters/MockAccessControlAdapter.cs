using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;

namespace Regalis.Infrastructure.Adapters;

/// <summary>
/// Phase-1 device adapter (AccessControl:Provider = "Mock", A01): the vendor API is
/// not supplied yet, so this logs the door command and reports the device online.
/// Simulation contract for QA: any deviceId starting with "OFFLINE" reports the
/// device down, exercising the DeviceError path end to end.
/// </summary>
public class MockAccessControlAdapter(ILogger<MockAccessControlAdapter> logger) : IAccessControlAdapter
{
    public const string Name = "Mock";

    public string ProviderName => Name;

    public Task<DeviceCommandResult> RequestDoorOpenAsync(string? deviceId, int locationId,
        CancellationToken ct = default)
    {
        if (deviceId?.StartsWith("OFFLINE", StringComparison.OrdinalIgnoreCase) == true)
        {
            logger.LogWarning("Mock device {DeviceId} at location {LocationId}: simulated OFFLINE",
                deviceId, locationId);
            return Task.FromResult(new DeviceCommandResult(false, "Offline (simulated)"));
        }
        logger.LogInformation("Mock device {DeviceId} at location {LocationId}: door released",
            deviceId ?? "(front desk)", locationId);
        return Task.FromResult(new DeviceCommandResult(true, "Online"));
    }
}
