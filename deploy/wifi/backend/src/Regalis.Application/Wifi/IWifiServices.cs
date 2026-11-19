using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Regalis.Shared.Api;

namespace Regalis.Application.Wifi;

/// <summary>
/// The Wi-Fi controller adapter (spec 7.2): create access, revoke access, list connected clients,
/// disconnect a client - plus the refused-attempt log, block/unblock and a connection test.
/// A different controller (UniFi, Omada, RADIUS, Mikrotik) is a new class behind this interface;
/// the screens and the engine do not change.
/// </summary>
public interface IWifiController
{
	string Type { get; }

	Task<WifiControllerStatus> TestAsync(CancellationToken ct);

	/// <summary>Writes a per-user password; returns the controller's own reference for it.</summary>
	Task<string> CreateAccessAsync(WifiKeySpec spec, CancellationToken ct);

	/// <summary>Takes the password out and drops every device that used it.</summary>
	Task RevokeAccessAsync(string controllerRef, CancellationToken ct);

	Task<IReadOnlyList<WifiClientInfo>> ListClientsAsync(CancellationToken ct);

	Task<IReadOnlyList<WifiAuthEvent>> ListAuthEventsAsync(DateTime sinceUtc, CancellationToken ct);

	Task DisconnectAsync(string mac, bool block, CancellationToken ct);

	Task UnblockAsync(string mac, CancellationToken ct);
}

/// <summary>Admin portal, /api/admin/wifi (WIFI.VIEW to look, WIFI.MANAGE to act).</summary>
public interface IWifiService
{
	Task<WifiConnectedDto> ConnectedAsync(WifiConnectedRequest request, CancellationToken ct);

	Task<byte[]> ExportConnectedAsync(WifiConnectedRequest request, CancellationToken ct);

	Task<WifiAccessListDto> AccessListAsync(WifiAccessListRequest request, CancellationToken ct);

	Task<WifiAccessDetailDto> AccessAsync(long accessId, CancellationToken ct);

	Task<WifiAccessRowDto> IssueGuestAsync(WifiGuestRequest request, CancellationToken ct);

	Task<WifiAccessRowDto> AddMemberAsync(long accessId, WifiMemberRequest request, CancellationToken ct);

	Task<WifiResendResultDto> ResendAsync(long accessId, WifiResendRequest request, CancellationToken ct);

	Task<WifiAccessRowDto> ResetAsync(long accessId, CancellationToken ct);

	Task<WifiAccessRowDto> UpdateAsync(long accessId, WifiUpdateAccessRequest request, CancellationToken ct);

	Task<WifiAccessRowDto> RevokeAsync(long accessId, WifiRevokeRequest request, CancellationToken ct);

	Task<WifiAccessRowDto> RetryControllerAsync(long accessId, CancellationToken ct);

	Task RemoveDeviceAsync(long accessId, long deviceId, CancellationToken ct);

	Task DisconnectAsync(string mac, WifiDisconnectRequest request, CancellationToken ct);

	Task UnblockAsync(string mac, CancellationToken ct);

	Task<PagedResult<WifiSessionDto>> SessionsAsync(WifiSessionRequest request, CancellationToken ct);

	Task<byte[]> ExportSessionsAsync(WifiSessionRequest request, CancellationToken ct);

	Task<WifiSettingsDto> GetSettingsAsync(CancellationToken ct);

	Task<WifiSettingsDto> SaveSettingsAsync(WifiSettingsDto request, CancellationToken ct);

	Task<WifiControllerStatus> TestControllerAsync(CancellationToken ct);

	Task<WifiSimResultDto> SimJoinAsync(WifiSimJoinRequest request, CancellationToken ct);

	Task<WifiSimResultDto> SimLeaveAsync(WifiSimLeaveRequest request, CancellationToken ct);
}

/// <summary>Customer portal and app, /api/portal/wifi: the signed-in customer's own access only.</summary>
public interface IWifiPortalService
{
	Task<WifiPortalDto> GetAsync(CancellationToken ct);

	Task<WifiResendResultDto> ResendAsync(WifiResendRequest request, CancellationToken ct);

	Task RemoveDeviceAsync(long deviceId, CancellationToken ct);

	Task<string> HelpAsync(WifiHelpRequest request, CancellationToken ct);
}
