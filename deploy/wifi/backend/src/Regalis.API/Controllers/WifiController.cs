using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Wifi;

namespace Regalis.API.Controllers;

/// <summary>
/// Wi-Fi, admin portal (spec 7.3). WIFI.VIEW opens the screens; disconnecting, revoking, issuing and
/// settings need WIFI.MANAGE (spec 8). No admin endpoint returns a password.
/// </summary>
[Route("api/admin/wifi")]
public class WifiAdminController(IWifiService wifi) : ApiControllerBase
{
	private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

	private static string Stamp() => DateTime.UtcNow.AddMinutes(330).ToString("yyyy-MM-dd_HHmm");

	[HttpGet("connected")]
	[HasPermission("WIFI.VIEW")]
	public async Task<IActionResult> Connected([FromQuery] WifiConnectedRequest request, CancellationToken ct) => OkEnvelope(await wifi.ConnectedAsync(request, ct));

	[HttpGet("connected/export")]
	[HasPermission("WIFI.VIEW")]
	public async Task<IActionResult> ConnectedExport([FromQuery] WifiConnectedRequest request, CancellationToken ct) =>
		File(await wifi.ExportConnectedAsync(request, ct), Xlsx, $"wifi_connected_{Stamp()}.xlsx");

	[HttpGet("access")]
	[HasPermission("WIFI.VIEW")]
	public async Task<IActionResult> Access([FromQuery] WifiAccessListRequest request, CancellationToken ct) => OkEnvelope(await wifi.AccessListAsync(request, ct));

	[HttpGet("access/{id:long}")]
	[HasPermission("WIFI.VIEW")]
	public async Task<IActionResult> AccessOne(long id, CancellationToken ct) => OkEnvelope(await wifi.AccessAsync(id, ct));

	[HttpPost("access")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> IssueGuest(WifiGuestRequest request, CancellationToken ct)
	{
		WifiAccessRowDto row = await wifi.IssueGuestAsync(request, ct);
		return OkEnvelope(row, $"Guest access {row.AccessNo} issued.");
	}

	[HttpPost("access/{id:long}/members")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> AddMember(long id, WifiMemberRequest request, CancellationToken ct) => OkEnvelope(await wifi.AddMemberAsync(id, request, ct), "Team member added.");

	[HttpPost("access/{id:long}/resend")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> Resend(long id, WifiResendRequest request, CancellationToken ct)
	{
		WifiResendResultDto r = await wifi.ResendAsync(id, request, ct);
		return OkEnvelope(r, r.Message);
	}

	[HttpPost("access/{id:long}/reset")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> Reset(long id, CancellationToken ct) => OkEnvelope(await wifi.ResetAsync(id, ct), "New password issued and sent.");

	[HttpPatch("access/{id:long}")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> Update(long id, WifiUpdateAccessRequest request, CancellationToken ct) => OkEnvelope(await wifi.UpdateAsync(id, request, ct), "Access updated.");

	[HttpPost("access/{id:long}/revoke")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> Revoke(long id, WifiRevokeRequest request, CancellationToken ct) => OkEnvelope(await wifi.RevokeAsync(id, request, ct), "Access revoked.");

	[HttpPost("access/{id:long}/retry")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> Retry(long id, CancellationToken ct) => OkEnvelope(await wifi.RetryControllerAsync(id, ct));

	[HttpDelete("access/{id:long}/devices/{deviceId:long}")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> RemoveDevice(long id, long deviceId, CancellationToken ct)
	{
		await wifi.RemoveDeviceAsync(id, deviceId, ct);
		return OkEnvelope("Device removed; its slot is free.");
	}

	[HttpPost("clients/{mac}/disconnect")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> Disconnect(string mac, WifiDisconnectRequest request, CancellationToken ct)
	{
		await wifi.DisconnectAsync(mac, request, ct);
		return OkEnvelope(request.Block ? "Device blocked." : "Device disconnected.");
	}

	[HttpPost("clients/{mac}/unblock")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> Unblock(string mac, CancellationToken ct)
	{
		await wifi.UnblockAsync(mac, ct);
		return OkEnvelope("Device unblocked.");
	}

	[HttpGet("sessions")]
	[HasPermission("WIFI.VIEW")]
	public async Task<IActionResult> Sessions([FromQuery] WifiSessionRequest request, CancellationToken ct) => OkEnvelope(await wifi.SessionsAsync(request, ct));

	[HttpGet("sessions/export")]
	[HasPermission("WIFI.VIEW")]
	public async Task<IActionResult> SessionsExport([FromQuery] WifiSessionRequest request, CancellationToken ct) =>
		File(await wifi.ExportSessionsAsync(request, ct), Xlsx, $"wifi_usage_{Stamp()}.xlsx");

	[HttpGet("settings")]
	[HasPermission("WIFI.VIEW")]
	public async Task<IActionResult> Settings(CancellationToken ct) => OkEnvelope(await wifi.GetSettingsAsync(ct));

	[HttpPut("settings")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> SaveSettings(WifiSettingsDto request, CancellationToken ct) => OkEnvelope(await wifi.SaveSettingsAsync(request, ct), "Wi-Fi settings saved.");

	[HttpPost("settings/test")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> Test(CancellationToken ct) => OkEnvelope(await wifi.TestControllerAsync(ct));

	[HttpPost("simulator/join")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> SimJoin(WifiSimJoinRequest request, CancellationToken ct) => OkEnvelope(await wifi.SimJoinAsync(request, ct));

	[HttpPost("simulator/leave")]
	[HasPermission("WIFI.MANAGE")]
	public async Task<IActionResult> SimLeave(WifiSimLeaveRequest request, CancellationToken ct) => OkEnvelope(await wifi.SimLeaveAsync(request, ct));
}

/// <summary>My Wi-Fi in the customer portal and app (spec 5.4, 7.3). Signed-in customers only (checked in the service).</summary>
[Route("api/portal/wifi")]
public class WifiPortalController(IWifiPortalService wifi) : ApiControllerBase
{
	[HttpGet]
	public async Task<IActionResult> Get(CancellationToken ct) => OkEnvelope(await wifi.GetAsync(ct));

	[HttpPost("resend")]
	public async Task<IActionResult> Resend(WifiResendRequest request, CancellationToken ct)
	{
		WifiResendResultDto r = await wifi.ResendAsync(request, ct);
		return OkEnvelope(r, r.Message);
	}

	[HttpDelete("devices/{id:long}")]
	public async Task<IActionResult> RemoveDevice(long id, CancellationToken ct)
	{
		await wifi.RemoveDeviceAsync(id, ct);
		return OkEnvelope("Device removed. Forget the network on that device too, or it will join again when in range.");
	}

	[HttpPost("help")]
	public async Task<IActionResult> Help(WifiHelpRequest request, CancellationToken ct)
	{
		string number = await wifi.HelpAsync(request, ct);
		return OkEnvelope(new { ticketNumber = number }, $"Support ticket {number} raised. We will get back to you.");
	}
}
