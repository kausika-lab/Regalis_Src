using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Abstractions;
using Regalis.Application.Wifi;
using Regalis.Domain.Entities.Wifi;
using Regalis.Infrastructure.Persistence;

namespace Regalis.Infrastructure.Services.Wifi;

/// <summary>Picks the controller adapter named in Wi-Fi Settings.</summary>
public class WifiControllerFactory(IServiceScopeFactory scopes, WifiSecrets secrets, IDateTimeProvider clock)
{
	public static readonly string[] Types = { "simulated", "unifi", "omada", "radius", "mikrotik" };

	public static bool IsInstalled(string type) => type == "simulated";

	public IWifiController Get(string type) => type == "simulated"
		? new SimulatedWifiController(scopes, secrets, clock)
		: new NotInstalledWifiController(type);
}

/// <summary>
/// UniFi, Omada, RADIUS and Mikrotik are listed in Settings (spec 5.3) but no adapter is written yet:
/// the hardware is not confirmed (open item 1). Every call fails plainly, so nothing is ever reported
/// to a customer as ready (spec 7.2).
/// </summary>
public sealed class NotInstalledWifiController(string type) : IWifiController
{
	public string Type => type;

	private InvalidOperationException Fail() => new InvalidOperationException($"No {type} adapter is installed in this build. Confirm the Wi-Fi hardware (open item 1) and add the adapter.");

	public Task<WifiControllerStatus> TestAsync(CancellationToken ct) =>
		Task.FromResult(new WifiControllerStatus(false, Fail().Message, 0, 0));

	public Task<string> CreateAccessAsync(WifiKeySpec spec, CancellationToken ct) => throw Fail();

	public Task RevokeAccessAsync(string controllerRef, CancellationToken ct) => throw Fail();

	public Task<IReadOnlyList<WifiClientInfo>> ListClientsAsync(CancellationToken ct) => throw Fail();

	public Task<IReadOnlyList<WifiAuthEvent>> ListAuthEventsAsync(DateTime sinceUtc, CancellationToken ct) => throw Fail();

	public Task DisconnectAsync(string mac, bool block, CancellationToken ct) => throw Fail();

	public Task UnblockAsync(string mac, CancellationToken ct) => throw Fail();
}

/// <summary>
/// Stands in for the Wi-Fi equipment until it is confirmed. Holds its own passwords, clients and
/// refused attempts in TRN_WifiSim* and works on its own database context, as a separate box would.
/// Devices join only through the "Simulate a device" panel in Wi-Fi Settings. Data used grows with
/// time online at a steady per-device rate (no traffic is real).
/// </summary>
public sealed class SimulatedWifiController(IServiceScopeFactory scopes, WifiSecrets secrets, IDateTimeProvider clock) : IWifiController
{
	public static readonly string[] AccessPoints = { "Floor 1 · AP-01", "Floor 1 · AP-02", "Floor 2 · AP-04", "Board Room · AP-07" };

	public string Type => "simulated";

	/// <summary>Stable across restarts (string.GetHashCode is not).</summary>
	private static int H(string s) => s.Aggregate(17, (a, ch) => unchecked(a * 31 + ch)) & 0x7FFFFFFF;

	private async Task<T> With<T>(Func<RegalisDbContext, Task<T>> work)
	{
		using IServiceScope scope = scopes.CreateScope();
		RegalisDbContext db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
		return await work(db);
	}

	public Task<WifiControllerStatus> TestAsync(CancellationToken ct) => With(async db =>
	{
		int online = await db.Set<WifiSimClient>().CountAsync(c => c.ConnectedUtc != null, ct);
		return new WifiControllerStatus(true, "Simulated controller: no Wi-Fi equipment is connected. Devices join only from \"Simulate a device\".", AccessPoints.Length, online);
	});

	public Task<string> CreateAccessAsync(WifiKeySpec spec, CancellationToken ct) => With(async db =>
	{
		string reference = "SIM-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
		db.Set<WifiSimKey>().Add(new WifiSimKey { ControllerRef = reference, Ssid = spec.Ssid, Username = spec.Username, PasswordEnc = secrets.Encrypt(spec.Password), CreatedUtc = clock.UtcNow });
		await db.SaveChangesAsync(ct);
		return reference;
	});

	public Task RevokeAccessAsync(string controllerRef, CancellationToken ct) => With(async db =>
	{
		await db.Set<WifiSimKey>().Where(k => k.ControllerRef == controllerRef).ExecuteDeleteAsync(ct);
		await db.Set<WifiSimClient>().Where(c => c.ControllerRef == controllerRef && c.ConnectedUtc != null)
			.ExecuteUpdateAsync(s => s.SetProperty(c => c.ConnectedUtc, (DateTime?)null), ct);
		return true;
	});

	public Task<IReadOnlyList<WifiClientInfo>> ListClientsAsync(CancellationToken ct) => With<IReadOnlyList<WifiClientInfo>>(async db =>
	{
		DateTime now = clock.UtcNow;
		List<WifiSimClient> rows = await db.Set<WifiSimClient>().AsNoTracking().Where(c => c.ConnectedUtc != null && !c.IsBlocked).ToListAsync(ct);
		return rows.Select(c =>
		{
			double seconds = Math.Max(0, (now - c.ConnectedUtc!.Value).TotalSeconds);
			long rate = 20_000 + (H(c.MacAddress) % 180_000);
			long down = c.BytesDown + (long)(seconds * rate);
			return new WifiClientInfo(c.MacAddress, c.DeviceName, c.Ssid, c.AccessPoint, c.IpAddress, c.ControllerRef, c.ConnectedUtc.Value, c.BytesUp + down / 6, down);
		}).ToList();
	});

	public Task<IReadOnlyList<WifiAuthEvent>> ListAuthEventsAsync(DateTime sinceUtc, CancellationToken ct) => With<IReadOnlyList<WifiAuthEvent>>(async db =>
		(await db.Set<WifiSimEvent>().AsNoTracking().Where(e => e.AtUtc > sinceUtc).OrderBy(e => e.AtUtc).Take(500).ToListAsync(ct))
		.Select(e => new WifiAuthEvent(e.MacAddress, e.DeviceName, e.Ssid, e.AccessPoint, e.ControllerRef, e.Result, e.AtUtc)).ToList());

	public Task DisconnectAsync(string mac, bool block, CancellationToken ct) => With(async db =>
	{
		WifiSimClient? c = await db.Set<WifiSimClient>().FirstOrDefaultAsync(x => x.MacAddress == mac, ct);
		if (c == null && block)
		{
			c = new WifiSimClient { MacAddress = mac };
			db.Set<WifiSimClient>().Add(c);
		}
		if (c != null)
		{
			c.ConnectedUtc = null;
			c.IsBlocked = c.IsBlocked || block;
			await db.SaveChangesAsync(ct);
		}
		return true;
	});

	public Task UnblockAsync(string mac, CancellationToken ct) => With(async db =>
	{
		await db.Set<WifiSimClient>().Where(x => x.MacAddress == mac).ExecuteUpdateAsync(s => s.SetProperty(c => c.IsBlocked, false), ct);
		return true;
	});

	// ---- the "Simulate a device" panel ------------------------------------------------------------

	/// <summary>
	/// A device tries to join. password = what the device typed (null = an open/shared network login
	/// that names nobody). Returns connected / refused-password / blocked.
	/// </summary>
	public Task<string> JoinAsync(string mac, string? deviceName, string ssid, string? accessPoint, string? password, bool shared, CancellationToken ct) => With(async db =>
	{
		DateTime now = clock.UtcNow;
		WifiSimClient? c = await db.Set<WifiSimClient>().FirstOrDefaultAsync(x => x.MacAddress == mac, ct);
		if (c?.IsBlocked == true)
		{
			return "blocked";
		}
		string? reference = null;
		if (!shared)
		{
			List<WifiSimKey> keys = await db.Set<WifiSimKey>().AsNoTracking().Where(k => k.Ssid == ssid).ToListAsync(ct);
			reference = keys.FirstOrDefault(k => secrets.Decrypt(k.PasswordEnc) == password)?.ControllerRef;
			if (reference == null)
			{
				db.Set<WifiSimEvent>().Add(new WifiSimEvent { MacAddress = mac, DeviceName = deviceName, Ssid = ssid, AccessPoint = accessPoint, Result = 2, AtUtc = now });
				await db.SaveChangesAsync(ct);
				return "refused-password";
			}
		}
		if (c == null)
		{
			c = new WifiSimClient { MacAddress = mac };
			db.Set<WifiSimClient>().Add(c);
		}
		c.ControllerRef = reference;
		c.DeviceName = deviceName;
		c.Ssid = ssid;
		c.AccessPoint = accessPoint ?? AccessPoints[H(mac) % AccessPoints.Length];
		c.IpAddress ??= $"10.20.{H(mac) % 200 + 1}.{(H(mac) / 7) % 250 + 2}";
		c.ConnectedUtc = now;
		c.BytesUp = 0;
		c.BytesDown = 0;
		await db.SaveChangesAsync(ct);
		return "connected";
	});

	/// <summary>A refused attempt with a password that was taken out (expired/revoked access).</summary>
	public Task RecordExpiredAttemptAsync(string mac, string? deviceName, string ssid, string? accessPoint, string? controllerRef, CancellationToken ct) => With(async db =>
	{
		db.Set<WifiSimEvent>().Add(new WifiSimEvent { MacAddress = mac, DeviceName = deviceName, Ssid = ssid, AccessPoint = accessPoint, ControllerRef = controllerRef, Result = 3, AtUtc = clock.UtcNow });
		await db.SaveChangesAsync(ct);
		return true;
	});

	public Task<bool> LeaveAsync(string mac, CancellationToken ct) => With(async db =>
		await db.Set<WifiSimClient>().Where(x => x.MacAddress == mac && x.ConnectedUtc != null)
			.ExecuteUpdateAsync(s => s.SetProperty(c => c.ConnectedUtc, (DateTime?)null), ct) > 0);
}
