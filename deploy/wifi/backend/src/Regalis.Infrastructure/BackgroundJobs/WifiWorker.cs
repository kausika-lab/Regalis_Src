using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regalis.Application.Wifi;
using Regalis.Domain.Entities.Booking;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Wifi;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services.Wifi;
using Regalis.Shared;

namespace Regalis.Infrastructure.BackgroundJobs;

/// <summary>
/// The Wi-Fi jobs (spec 7.4). Runs every 15 seconds:
///  - issue access for bookings and passes paid since Wi-Fi was switched on, follow cancellations and new dates;
///  - write due access into the controller (3 tries) and deliver it;
///  - expire access past its end date (the spec asks for every 15 minutes; checking more often is harmless);
///  - every 30 s, read the connected clients and refused attempts from the controller.
/// Hourly: pause / restore for overdue invoices. Daily: 08:00 IST expiry reminders, session retention.
/// Nothing here edits a booking, pass, invoice or payment: it only reads them.
/// </summary>
public class WifiJobs(RegalisDbContext db, WifiEngine engine, ILogger<WifiJobs> logger)
{
	private static DateTime _lastPoll = DateTime.MinValue;

	private DateTime Now => engine.Now;

	public async Task RunAsync(CancellationToken ct)
	{
		await Step("sources", () => SyncSourcesAsync(ct));
		await Step("controller", () => PushDueAsync(ct));
		await Step("expiry", () => ExpireDueAsync(ct));
		await Step("poll", () => PollAsync(false, ct));
		await Step("overdue", () => OverdueAsync(false, ct));
		await Step("reminders", () => RemindersAsync(ct));
		await Step("retention", () => RetentionAsync(ct));
	}

	private async Task Step(string name, Func<Task> work)
	{
		try
		{
			await work();
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogError(ex, "Wi-Fi job step {Step} failed", name);
			db.ChangeTracker.Clear();
			engine.ForgetConfig();
		}
	}

	// ---- issuing (spec 6.1, 4.4) -------------------------------------------------------------------

	public async Task SyncSourcesAsync(CancellationToken ct)
	{
		WifiConfig cfg = await engine.ConfigAsync(ct);
		DateTime now = Now;
		if (cfg.AutoIssue && cfg.IssueSinceUtc != DateTime.MaxValue)
		{
			DateTime since = cfg.IssueSinceUtc;
			IQueryable<WifiAccessSource> known = db.Set<WifiAccessSource>();
			List<Booking> bookings = await db.Bookings.AsNoTracking()
				.Where(b => b.EndUtc > now && b.CustomerPassId == null
					&& (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.CheckedIn)
					&& (b.PaymentStatus == BookingPaymentStatus.Paid || (cfg.PayLater && b.IsPayLater && b.Status != BookingStatus.Pending))
					&& !known.Any(s => s.SourceType == 1 && s.RefId == b.BookingId)
					&& (b.CreatedDate >= since
						|| db.Payments.Any(p => p.BookingId == b.BookingId && p.Status == PaymentStatus.Success && p.PaidUtc >= since)
						|| db.Invoices.Any(i => i.BookingId == b.BookingId && i.PaidUtc >= since)))
				.OrderBy(b => b.BookingId).Take(50).ToListAsync(ct);
			foreach (Booking b in bookings)
			{
				string code = await db.WorkspaceTypes.AsNoTracking().IgnoreQueryFilters().Where(t => t.WorkspaceTypeId == b.WorkspaceTypeId).Select(t => t.TypeCode).FirstOrDefaultAsync(ct) ?? "";
				string profile = code == "VIRTUAL_WORKSPACE" ? "virtual"
					: code == "MEETING_ROOM" ? "meeting"
					: (b.BillingMonths ?? 0) > 0 || (b.EndUtc - b.StartUtc).TotalDays >= 7 ? "member" : "short";
				await IssueForSourceAsync(b.CustomerId, 1, b.BookingId, b.BookingNumber, profile, b.StartUtc, b.EndUtc, ct);
			}
			DateOnly today = IstClock.IstDateOf(now);
			List<CustomerPass> passes = await db.CustomerPasses.AsNoTracking()
				.Where(p => (p.Status == CustomerPassStatus.Active || p.Status == CustomerPassStatus.Used) && p.ValidTo >= today
					&& (!p.IsPayLater || p.AmountPaid >= p.TotalAmount || cfg.PayLater)
					&& !known.Any(s => s.SourceType == 2 && s.RefId == p.CustomerPassId)
					&& (p.PurchaseUtc >= since || db.Payments.Any(x => x.CustomerPassId == p.CustomerPassId && x.Status == PaymentStatus.Success && x.PaidUtc >= since)))
				.OrderBy(p => p.CustomerPassId).Take(50).ToListAsync(ct);
			foreach (CustomerPass p in passes)
			{
				(DateTime start, DateTime end) = await PassWindowAsync(p, cfg, ct);
				if (end <= now)
				{
					continue;
				}
				await IssueForSourceAsync(p.CustomerId, 2, p.CustomerPassId, p.PassNumber, p.PassType == PassType.DayPass ? "daypass" : "member", start, end, ct);
			}
		}
		await FollowSourcesAsync(ct);
	}

	/// <summary>Day pass: opening to closing time (open item 3). Membership: to the end of its last day.</summary>
	private async Task<(DateTime Start, DateTime End)> PassWindowAsync(CustomerPass p, WifiConfig cfg, CancellationToken ct)
	{
		var loc = await db.Locations.AsNoTracking().IgnoreQueryFilters().Where(l => l.LocationId == p.LocationId)
			.Select(l => new { l.WorkingHoursStart, l.WorkingHoursEnd }).FirstOrDefaultAsync(ct);
		TimeOnly open = loc?.WorkingHoursStart ?? new TimeOnly(8, 0);
		TimeOnly close = loc?.WorkingHoursEnd ?? new TimeOnly(21, 0);
		if (p.PassType == PassType.DayPass)
		{
			return (IstClock.ToUtc(p.ValidFrom.ToDateTime(open)), IstClock.ToUtc(p.ValidTo.ToDateTime(close)));
		}
		return (IstClock.ToUtc(p.ValidFrom.ToDateTime(TimeOnly.MinValue)), IstClock.ToUtc(p.ValidTo.AddDays(1).ToDateTime(TimeOnly.MinValue)));
	}

	private async Task IssueForSourceAsync(long customerId, byte type, long refId, string number, string profileKey, DateTime start, DateTime end, CancellationToken ct)
	{
		WifiConfig cfg = await engine.ConfigAsync(ct);
		WifiProfileDto profile = cfg.Profile(profileKey);
		WifiAccessSource source = new WifiAccessSource { SourceType = type, RefId = refId, RefNumber = number, StartUtc = start, EndUtc = end, CreatedUtc = Now };
		string what = (type == 1 ? "Booking " : "Pass ") + number;
		if (string.IsNullOrEmpty(profile.Ssid))
		{
			db.Set<WifiAccessSource>().Add(source); // AccessId 0: this kind of booking gets no Wi-Fi.
			await db.SaveChangesAsync(ct);
			return;
		}
		User? user = await db.Users.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(u => u.UserId == customerId, ct);
		if (user == null || user.IsDeleted || !user.IsActive)
		{
			db.Set<WifiAccessSource>().Add(source); // AccessId 0: the account is deleted or switched off.
			await db.SaveChangesAsync(ct);
			return;
		}
		List<WifiAccess> mine = await db.Set<WifiAccess>()
			.Where(a => a.CustomerId == customerId && a.ParentAccessId == null && (a.Kind == WifiAccessKind.Booking || a.Kind == WifiAccessKind.Pass) && a.Status != WifiAccessStatus.Revoked)
			.OrderByDescending(a => a.ValidUntilUtc).ToListAsync(ct);
		WifiAccess? covering = mine.FirstOrDefault(a => a.Status is WifiAccessStatus.Issued or WifiAccessStatus.Active or WifiAccessStatus.Paused && a.ValidFromUtc <= start && a.ValidUntilUtc >= end);
		if (covering != null)
		{
			source.AccessId = covering.AccessId;
			db.Set<WifiAccessSource>().Add(source);
			engine.Log(covering.AccessId, "covered", $"{what} paid: already covered by this access (same password, no change).", null);
			await db.SaveChangesAsync(ct);
			return;
		}
		WifiAccess? renew = mine.FirstOrDefault(a => a.Ssid == profile.Ssid);
		if (renew != null)
		{
			bool wasActive = renew.Status == WifiAccessStatus.Active;
			DateTime oldUntil = renew.ValidUntilUtc;
			source.AccessId = renew.AccessId;
			db.Set<WifiAccessSource>().Add(source);
			renew.DeviceLimit = Math.Max(renew.DeviceLimit, profile.DeviceLimit);
			if (profileKey == "member")
			{
				renew.Profile = "member";
			}
			await db.SaveChangesAsync(ct);
			await engine.RecomputeAsync(renew, $"{what} paid: same password, new dates.", ct);
			if (wasActive && renew.ValidUntilUtc > oldUntil && renew.CustomerId is long cid)
			{
				engine.CustomerNotice(cid, renew.AccessId, "Your Wi-Fi has been extended", $"{renew.Ssid} now works until {WifiEngine.IstDateTime(renew.ValidUntilUtc)}, with the same password.");
			}
			await db.SaveChangesAsync(ct);
			return;
		}
		WifiAccess a = await engine.NewAccessAsync(type == 1 ? WifiAccessKind.Booking : WifiAccessKind.Pass, profileKey, customerId, user.FullName, user.Email, user.MobileNumber,
			user.CompanyName, profile.Ssid, start, end, profile.DeviceLimit, null, ct);
		if (type == 1)
		{
			a.BookingId = refId;
		}
		else
		{
			a.CustomerPassId = refId;
		}
		await db.SaveChangesAsync(ct);
		source.AccessId = a.AccessId;
		db.Set<WifiAccessSource>().Add(source);
		engine.Log(a.AccessId, "issued", $"Issued on payment of {what}: {a.Username} on {a.Ssid}, {a.DeviceLimit} devices, valid {WifiEngine.IstDateTime(start)} – {WifiEngine.IstDateTime(end)}.", null);
		await db.SaveChangesAsync(ct);
		if (start <= Now.AddHours(1) && await engine.PushAsync(a, ct))
		{
			await engine.DeliverAsync(a, "issued", ct);
		}
		await db.SaveChangesAsync(ct);
	}

	/// <summary>Cancelled bookings and passes revoke at once (rule switchable); moved dates move the access.</summary>
	private async Task FollowSourcesAsync(CancellationToken ct)
	{
		DateTime recent = Now.AddDays(-1);
		WifiConfig cfg = await engine.ConfigAsync(ct);
		// Read values only (AsNoTracking on a joined set makes the whole query untracked), then load
		// the source rows to change with tracking, by id.
		var bookingRows = await (from s in db.Set<WifiAccessSource>().AsNoTracking()
			join b in db.Bookings.AsNoTracking().IgnoreQueryFilters() on s.RefId equals b.BookingId
			where s.SourceType == 1 && !s.IsCancelled && s.AccessId > 0 && s.EndUtc > recent
				&& (b.Status == BookingStatus.Cancelled || b.PaymentStatus == BookingPaymentStatus.Refunded || b.StartUtc != s.StartUtc || b.EndUtc != s.EndUtc)
			select new { s.SourceId, b.Status, b.PaymentStatus, b.StartUtc, b.EndUtc }).Take(100).ToListAsync(ct);
		var passRows = await (from s in db.Set<WifiAccessSource>().AsNoTracking()
			join p in db.CustomerPasses.AsNoTracking().IgnoreQueryFilters() on s.RefId equals p.CustomerPassId
			where s.SourceType == 2 && !s.IsCancelled && s.AccessId > 0 && s.EndUtc > recent
			select new { s.SourceId, Pass = p }).Take(300).ToListAsync(ct);
		List<long> sourceIds = bookingRows.Select(r => r.SourceId).Concat(passRows.Select(r => r.SourceId)).ToList();
		Dictionary<long, WifiAccessSource> sources = await db.Set<WifiAccessSource>().Where(s => sourceIds.Contains(s.SourceId)).ToDictionaryAsync(s => s.SourceId, ct);
		Dictionary<long, (string Why, bool Cancel)> touched = new Dictionary<long, (string, bool)>();
		foreach (var r in bookingRows)
		{
			WifiAccessSource src = sources[r.SourceId];
			if (r.Status == BookingStatus.Cancelled || r.PaymentStatus == BookingPaymentStatus.Refunded)
			{
				src.IsCancelled = true;
				touched[src.AccessId] = ($"Booking {src.RefNumber} {(r.Status == BookingStatus.Cancelled ? "cancelled" : "refunded")}.", true);
			}
			else
			{
				src.StartUtc = r.StartUtc;
				src.EndUtc = r.EndUtc;
				touched.TryAdd(src.AccessId, ($"Booking {src.RefNumber} moved.", false));
			}
		}
		foreach (var r in passRows)
		{
			WifiAccessSource src = sources[r.SourceId];
			if (r.Pass.Status == CustomerPassStatus.Cancelled)
			{
				src.IsCancelled = true;
				touched[src.AccessId] = ($"Membership {src.RefNumber} ended (cancelled).", true);
				continue;
			}
			(DateTime start, DateTime end) = await PassWindowAsync(r.Pass, cfg, ct);
			if (start != src.StartUtc || (end != src.EndUtc && !(cfg.DayPassUntil == "24h" && src.EndUtc > end)))
			{
				src.StartUtc = start;
				src.EndUtc = end;
				touched.TryAdd(src.AccessId, ($"Pass {src.RefNumber} dates changed.", false));
			}
		}
		if (touched.Count == 0)
		{
			return;
		}
		await db.SaveChangesAsync(ct);
		foreach ((long accessId, (string why, bool cancel)) in touched)
		{
			WifiAccess? a = await db.Set<WifiAccess>().FirstOrDefaultAsync(x => x.AccessId == accessId, ct);
			if (a != null && a.Status != WifiAccessStatus.Revoked)
			{
				await engine.RecomputeAsync(a, why, ct, cancel);
				await db.SaveChangesAsync(ct);
			}
		}
	}

	// ---- controller writes and expiry ------------------------------------------------------------

	public async Task PushDueAsync(CancellationToken ct)
	{
		DateTime now = Now;
		DateTime soon = now.AddHours(1);
		List<WifiAccess> due = await db.Set<WifiAccess>()
			.Where(a => (a.Status == WifiAccessStatus.Issued || a.Status == WifiAccessStatus.Active) && a.ControllerState == WifiControllerState.Pending
				&& a.ValidFromUtc <= soon && a.ValidUntilUtc > now && (a.ControllerNextTryUtc == null || a.ControllerNextTryUtc <= now))
			.OrderBy(a => a.AccessId).Take(30).ToListAsync(ct);
		foreach (WifiAccess a in due)
		{
			if (await engine.PushAsync(a, ct) && a.DeliveredUtc == null)
			{
				await engine.DeliverAsync(a, "issued", ct);
			}
			await db.SaveChangesAsync(ct);
		}
	}

	public async Task ExpireDueAsync(CancellationToken ct)
	{
		DateTime now = Now;
		List<WifiAccess> over = await db.Set<WifiAccess>()
			.Where(a => (a.Status == WifiAccessStatus.Issued || a.Status == WifiAccessStatus.Active || a.Status == WifiAccessStatus.Paused) && a.ValidUntilUtc <= now)
			.OrderBy(a => a.ParentAccessId == null ? 1 : 0).Take(100).ToListAsync(ct);
		foreach (WifiAccess a in over)
		{
			await engine.ExpireAsync(a, ct);
			await db.SaveChangesAsync(ct);
		}
	}

	// ---- reading the controller (spec 6.2, 7.2) ---------------------------------------------------------

	public async Task PollAsync(bool force, CancellationToken ct)
	{
		DateTime now = Now;
		if (!force && (now - _lastPoll).TotalSeconds < 28)
		{
			return;
		}
		_lastPoll = now;
		WifiConfig cfg = await engine.ConfigAsync(ct);
		IWifiController ctrl = await engine.ControllerAsync(ct);
		DateTime since = DateTime.TryParse(cfg.Get("poll.eventsSinceUtc"), WifiEngine.Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime s) ? s : now.AddMinutes(-5);
		IReadOnlyList<WifiClientInfo> clients;
		IReadOnlyList<WifiAuthEvent> attempts;
		try
		{
			clients = await ctrl.ListClientsAsync(ct);
			attempts = await ctrl.ListAuthEventsAsync(since, ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			int fails = (int.TryParse(cfg.Get("poll.failCount"), out int f) ? f : 0) + 1;
			await engine.SetAsync("poll.ok", "0", null, ct);
			await engine.SetAsync("poll.error", WifiEngine.Clip(ex.Message, 500), null, ct);
			await engine.SetAsync("poll.lastTryUtc", now.ToString("o"), null, ct);
			await engine.SetAsync("poll.failCount", fails.ToString(WifiEngine.Inv), null, ct);
			if (fails == 3)
			{
				engine.AdminAlert("wifi.controller_down", "Wi-Fi controller not answering", $"The {ctrl.Type} controller has not answered 3 times in a row: {ex.Message} The Connected now list is out of date and new access cannot be created.", null, null, null);
			}
			await db.SaveChangesAsync(ct);
			return;
		}
		HashSet<string> blocked = (await db.Set<WifiBlock>().AsNoTracking().Where(b => b.UnblockedUtc == null).Select(b => b.MacAddress).ToListAsync(ct)).ToHashSet();
		List<string> refs = clients.Select(c => c.ControllerRef).Concat(attempts.Select(a => a.ControllerRef)).Where(r => r != null).Select(r => r!).Distinct().ToList();
		Dictionary<string, WifiAccess> byRef = (await db.Set<WifiAccess>().Where(a => a.ControllerRef != null && refs.Contains(a.ControllerRef)).ToListAsync(ct))
			.GroupBy(a => a.ControllerRef!).ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.AccessId).First());
		List<long> accessIds = byRef.Values.Select(a => a.AccessId).ToList();
		List<WifiDevice> devices = await db.Set<WifiDevice>().Where(d => accessIds.Contains(d.AccessId) && d.RemovedUtc == null).ToListAsync(ct);
		List<WifiSession> open = await db.Set<WifiSession>().Where(x => x.EndedUtc == null && x.Result == WifiSessionResult.Connected).ToListAsync(ct);
		Dictionary<string, WifiSession> openByMac = open.GroupBy(x => x.MacAddress).ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SessionId).First());
		HashSet<string> seen = new HashSet<string>();
		foreach (WifiClientInfo c in clients)
		{
			string mac = c.Mac.ToUpperInvariant();
			if (blocked.Contains(mac))
			{
				await ctrl.DisconnectAsync(mac, true, ct);
				continue;
			}
			WifiAccess? a = c.ControllerRef != null && byRef.TryGetValue(c.ControllerRef, out WifiAccess? hit) ? hit : null;
			if (a != null && a.Status is not (WifiAccessStatus.Active or WifiAccessStatus.Issued))
			{
				await ctrl.DisconnectAsync(mac, false, ct);
				Refused(a.AccessId, mac, c.DeviceName, c.Ssid, c.AccessPoint, WifiSessionResult.RefusedExpired, now);
				continue;
			}
			if (a != null)
			{
				WifiDevice? d = devices.FirstOrDefault(x => x.AccessId == a.AccessId && x.MacAddress == mac);
				if (d == null)
				{
					int used = devices.Count(x => x.AccessId == a.AccessId);
					if (used >= a.DeviceLimit)
					{
						await ctrl.DisconnectAsync(mac, false, ct);
						bool recently = await db.Set<WifiSession>().AnyAsync(x => x.MacAddress == mac && x.Result == WifiSessionResult.RefusedDeviceLimit && x.StartedUtc > now.AddMinutes(-30), ct);
						Refused(a.AccessId, mac, c.DeviceName, c.Ssid, c.AccessPoint, WifiSessionResult.RefusedDeviceLimit, now);
						if (!recently)
						{
							engine.Log(a.AccessId, "refused", $"{c.DeviceName ?? mac} refused: all {a.DeviceLimit} device slots are in use.", null);
							if (a.CustomerId is long cid && a.Kind is WifiAccessKind.Booking or WifiAccessKind.Pass)
							{
								engine.CustomerNotice(cid, a.AccessId, "Wi-Fi device limit reached",
									$"{c.DeviceName ?? "A new device"} could not join: your {a.DeviceLimit} device slots are in use. Remove a device in My Wi-Fi first.", NotificationPriority.High);
							}
						}
						continue;
					}
					d = new WifiDevice { AccessId = a.AccessId, MacAddress = mac, DeviceName = WifiEngine.Clip(c.DeviceName, 120), FirstSeenUtc = now, LastSeenUtc = now };
					db.Set<WifiDevice>().Add(d);
					devices.Add(d);
					engine.Log(a.AccessId, "connected", $"New device {c.DeviceName ?? mac} ({mac}) joined {c.Ssid} at {c.AccessPoint}.", null);
					if (cfg.DayPassUntil == "24h" && a.Profile == "daypass" && (a.ValidUntilUtc - a.ValidFromUtc).TotalHours < 24 && a.ValidUntilUtc < now.AddHours(24))
					{
						a.ValidUntilUtc = now.AddHours(24);
						engine.Log(a.AccessId, "extended", $"Day pass first used: valid for 24 hours, until {WifiEngine.IstDateTime(a.ValidUntilUtc)}.", null);
					}
				}
				d.LastSeenUtc = now;
				d.DeviceName = WifiEngine.Clip(c.DeviceName, 120) ?? d.DeviceName;
			}
			seen.Add(mac);
			if (openByMac.TryGetValue(mac, out WifiSession? sess) && (sess.AccessId != a?.AccessId || sess.StartedUtc < c.ConnectedSinceUtc.AddSeconds(-5)))
			{
				sess.EndedUtc = sess.LastSeenUtc;
				sess = null;
			}
			if (sess == null)
			{
				sess = new WifiSession { AccessId = a?.AccessId, MacAddress = mac, StartedUtc = c.ConnectedSinceUtc, Result = WifiSessionResult.Connected };
				db.Set<WifiSession>().Add(sess);
				if (a == null && cfg.RuleUnknownDevice && cfg.Mode != "shared")
				{
					DateTime day = now.AddHours(-24);
					bool told = await db.Set<WifiEvent>().AnyAsync(e => e.Event == "unknown" && e.Detail.Contains(mac) && e.CreatedUtc > day, ct);
					if (!told)
					{
						engine.Log(null, "unknown", $"Unrecognised device {c.DeviceName ?? "(no name)"} {mac} on {c.Ssid} at {c.AccessPoint}: no customer matched.", null);
						engine.AdminAlert("wifi.unknown_device", "Unrecognised device on the Wi-Fi",
							$"{c.DeviceName ?? "A device"} ({mac}) joined {c.Ssid} at {c.AccessPoint} and matches no customer access. Block it in Wi-Fi → Connected now if it should not be there.", null, null, mac);
					}
				}
			}
			sess.DeviceName = WifiEngine.Clip(c.DeviceName, 120);
			sess.Ssid = c.Ssid;
			sess.AccessPoint = c.AccessPoint;
			sess.IpAddress = c.Ip;
			sess.BytesUp = c.BytesUp;
			sess.BytesDown = c.BytesDown;
			sess.LastSeenUtc = now;
		}
		foreach (WifiSession x in open.Where(x => !seen.Contains(x.MacAddress)))
		{
			x.EndedUtc = now;
		}
		foreach (WifiAuthEvent e in attempts)
		{
			WifiAccess? a = e.ControllerRef != null && byRef.TryGetValue(e.ControllerRef, out WifiAccess? hit) ? hit : null;
			Refused(a?.AccessId, e.Mac.ToUpperInvariant(), e.DeviceName, e.Ssid, e.AccessPoint, (WifiSessionResult)Math.Clamp((int)e.Result, 2, 4), e.AtUtc);
			since = e.AtUtc > since ? e.AtUtc : since;
		}
		await engine.SetAsync("poll.eventsSinceUtc", since.ToString("o"), null, ct);
		await engine.SetAsync("poll.ok", "1", null, ct);
		await engine.SetAsync("poll.error", null, null, ct);
		await engine.SetAsync("poll.failCount", "0", null, ct);
		await engine.SetAsync("poll.lastUtc", now.ToString("o"), null, ct);
		string today = IstClock.IstDateOf(now).ToString("yyyy-MM-dd", WifiEngine.Inv);
		int online = seen.Count;
		if (cfg.Get("peak.date") != today || (int.TryParse(cfg.Get("peak.count"), out int peak) ? peak : 0) < online)
		{
			await engine.SetAsync("peak.date", today, null, ct);
			await engine.SetAsync("peak.count", online.ToString(WifiEngine.Inv), null, ct);
			await engine.SetAsync("peak.atUtc", now.ToString("o"), null, ct);
		}
		await db.SaveChangesAsync(ct);
	}

	private void Refused(long? accessId, string mac, string? name, string? ssid, string? ap, WifiSessionResult result, DateTime at)
	{
		db.Set<WifiSession>().Add(new WifiSession
		{
			AccessId = accessId,
			MacAddress = mac,
			DeviceName = WifiEngine.Clip(name, 120),
			Ssid = ssid,
			AccessPoint = ap,
			StartedUtc = at,
			EndedUtc = at,
			LastSeenUtc = at,
			Result = result
		});
	}

	// ---- overdue invoices (spec 4.4; open item 4) -------------------------------------------------------

	public async Task OverdueAsync(bool force, CancellationToken ct)
	{
		DateTime now = Now;
		WifiConfig cfg = await engine.ConfigAsync(ct);
		DateTime last = DateTime.TryParse(cfg.Get("overdue.lastUtc"), WifiEngine.Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime l) ? l : DateTime.MinValue;
		if (!force && (now - last).TotalMinutes < 60)
		{
			return;
		}
		await engine.SetAsync("overdue.lastUtc", now.ToString("o"), null, ct);
		await db.SaveChangesAsync(ct);
		cfg = await engine.ConfigAsync(ct);
		Dictionary<long, (string Number, int Days)> overdue = new Dictionary<long, (string, int)>();
		if (cfg.OverdueDays > 0)
		{
			DateOnly today = IstClock.IstDateOf(now);
			DateOnly cutoff = today.AddDays(-cfg.OverdueDays);
			var rows = await db.Invoices.AsNoTracking()
				.Where(i => (i.Status == InvoiceStatus.Pending || i.Status == InvoiceStatus.Sent || i.Status == InvoiceStatus.PartiallyPaid) && i.OutstandingAmount > 0 && i.DueDate <= cutoff)
				.OrderBy(i => i.DueDate).Select(i => new { i.CustomerId, i.InvoiceNumber, i.DueDate }).ToListAsync(ct);
			foreach (var r in rows)
			{
				overdue.TryAdd(r.CustomerId, (r.InvoiceNumber, today.DayNumber - r.DueDate.DayNumber));
			}
		}
		List<long> ids = overdue.Keys.ToList();
		List<WifiAccess> toPause = await db.Set<WifiAccess>()
			.Where(a => a.CustomerId != null && ids.Contains(a.CustomerId.Value) && a.ParentAccessId == null && (a.Kind == WifiAccessKind.Booking || a.Kind == WifiAccessKind.Pass)
				&& (a.Status == WifiAccessStatus.Active || a.Status == WifiAccessStatus.Issued) && a.ValidUntilUtc > now)
			.ToListAsync(ct);
		foreach (WifiAccess a in toPause)
		{
			(string number, int days) = overdue[a.CustomerId!.Value];
			await engine.PauseAsync(a, $"Invoice {number} is {days} days overdue.", ct);
			await db.SaveChangesAsync(ct);
		}
		List<WifiAccess> toResume = await db.Set<WifiAccess>()
			.Where(a => a.Status == WifiAccessStatus.Paused && a.ParentAccessId == null && (a.CustomerId == null || !ids.Contains(a.CustomerId.Value)))
			.ToListAsync(ct);
		foreach (WifiAccess a in toResume)
		{
			if (a.ValidUntilUtc <= now)
			{
				await engine.ExpireAsync(a, ct);
			}
			else
			{
				await engine.ResumeAsync(a, "Overdue invoice paid (or the rule was switched off).", null, ct);
				if (a.CustomerId is long cid)
				{
					engine.CustomerNotice(cid, a.AccessId, "Your Wi-Fi is back", $"{a.Ssid} works again with the same password, until {WifiEngine.IstDateTime(a.ValidUntilUtc)}.");
				}
			}
			await db.SaveChangesAsync(ct);
		}
	}

	// ---- reminders and retention --------------------------------------------------------------------

	public async Task RemindersAsync(CancellationToken ct)
	{
		WifiConfig cfg = await engine.ConfigAsync(ct);
		DateTime now = Now;
		DateTime ist = IstClock.ToIst(now);
		string today = ist.ToString("yyyy-MM-dd", WifiEngine.Inv);
		if (!cfg.Reminder || ist.TimeOfDay < new TimeSpan(8, 0, 0) || cfg.Get("reminder.lastDate") == today)
		{
			return;
		}
		await engine.SetAsync("reminder.lastDate", today, null, ct);
		await db.SaveChangesAsync(ct);
		DateTime within = now.AddHours(24);
		List<WifiAccess> ending = await db.Set<WifiAccess>()
			.Where(a => a.Status == WifiAccessStatus.Active && a.ReminderSentUtc == null && a.ValidUntilUtc > now && a.ValidUntilUtc <= within && a.Kind != WifiAccessKind.TeamMember)
			.Take(500).ToListAsync(ct);
		foreach (WifiAccess a in ending)
		{
			string when = WifiEngine.IstDateTime(a.ValidUntilUtc);
			if (a.CustomerId is long cid && a.Kind is WifiAccessKind.Booking or WifiAccessKind.Pass)
			{
				engine.CustomerNotice(cid, a.AccessId, "Your Wi-Fi ends soon", $"{a.Ssid} stops working on {when}. Renew your booking to keep the same password.");
			}
			engine.Log(a.AccessId, "reminded", $"Expiry reminder sent (ends {when}).", null);
			a.ReminderSentUtc = now;
			await db.SaveChangesAsync(ct);
		}
	}

	public async Task RetentionAsync(CancellationToken ct)
	{
		WifiConfig cfg = await engine.ConfigAsync(ct);
		string today = IstClock.IstDateOf(Now).ToString("yyyy-MM-dd", WifiEngine.Inv);
		if (cfg.Get("retention.lastDate") == today)
		{
			return;
		}
		await engine.SetAsync("retention.lastDate", today, null, ct);
		await db.SaveChangesAsync(ct);
		DateTime cutoff = Now.AddMonths(-cfg.RetentionMonths);
		int n = await db.Set<WifiSession>().Where(x => x.EndedUtc != null && x.StartedUtc < cutoff).ExecuteDeleteAsync(ct);
		DateTime week = Now.AddDays(-7);
		await db.Set<WifiSimEvent>().Where(x => x.AtUtc < week).ExecuteDeleteAsync(ct);
		if (n > 0)
		{
			logger.LogInformation("Wi-Fi retention: {Count} session record(s) older than {Months} months deleted", n, cfg.RetentionMonths);
		}
	}
}

public class WifiWorker(IServiceScopeFactory scopes, ILogger<WifiWorker> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				using IServiceScope scope = scopes.CreateScope();
				await scope.ServiceProvider.GetRequiredService<WifiJobs>().RunAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "Wi-Fi worker run failed");
			}
			await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
		}
	}
}
