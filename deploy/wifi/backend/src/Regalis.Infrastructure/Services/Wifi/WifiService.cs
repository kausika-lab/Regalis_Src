using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Abstractions;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Wifi;
using Regalis.Domain.Entities.Wifi;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.BackgroundJobs;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.Wifi;

/// <summary>Admin Wi-Fi screens (spec 3, 5.1-5.3). Passwords never leave this service except inside the customer's own email.</summary>
public class WifiService(RegalisDbContext db, WifiEngine engine, WifiJobs jobs, WifiControllerFactory controllers, ICurrentUser currentUser) : IWifiService
{
	private DateTime Now => engine.Now;

	private long Me => currentUser.UserId;

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	// ---- rows ---------------------------------------------------------------------------------------

	private sealed record Person(string Name, string? Email, string? Mobile, string? Company);

	private async Task<Dictionary<long, Person>> PeopleAsync(IEnumerable<long> ids, CancellationToken ct)
	{
		List<long> list = ids.Distinct().ToList();
		return await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => list.Contains(u.UserId))
			.ToDictionaryAsync(u => u.UserId, u => new Person(u.FullName, u.Email, u.MobileNumber, u.CompanyName), ct);
	}

	private async Task<List<WifiAccessRowDto>> RowsAsync(List<WifiAccess> list, CancellationToken ct)
	{
		if (list.Count == 0)
		{
			return new List<WifiAccessRowDto>();
		}
		List<long> ids = list.Select(a => a.AccessId).ToList();
		List<WifiAccess> children = await db.Set<WifiAccess>().AsNoTracking().Where(c => c.ParentAccessId != null && ids.Contains(c.ParentAccessId.Value)).ToListAsync(ct);
		List<long> all = ids.Concat(children.Select(c => c.AccessId)).ToList();
		Dictionary<long, int> devices = await db.Set<WifiDevice>().AsNoTracking().Where(d => all.Contains(d.AccessId) && d.RemovedUtc == null)
			.GroupBy(d => d.AccessId).ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
		HashSet<long> inUse = (await db.Set<WifiSession>().AsNoTracking().Where(s => s.EndedUtc == null && s.Result == WifiSessionResult.Connected && s.AccessId != null && ids.Contains(s.AccessId.Value))
			.Select(s => s.AccessId!.Value).Distinct().ToListAsync(ct)).ToHashSet();
		Dictionary<long, WifiAccessSource> latest = (await db.Set<WifiAccessSource>().AsNoTracking().Where(s => ids.Contains(s.AccessId)).ToListAsync(ct))
			.GroupBy(s => s.AccessId).ToDictionary(g => g.Key, g => g.Where(s => !s.IsCancelled).OrderByDescending(s => s.EndUtc).FirstOrDefault() ?? g.OrderByDescending(s => s.EndUtc).First());
		Dictionary<long, int> sourceCounts = await db.Set<WifiAccessSource>().AsNoTracking().Where(s => ids.Contains(s.AccessId) && !s.IsCancelled)
			.GroupBy(s => s.AccessId).ToDictionaryAsync(g => g.Key, g => g.Count(), ct);
		Dictionary<long, Person> people = await PeopleAsync(list.Where(a => a.CustomerId != null).Select(a => a.CustomerId!.Value), ct);
		List<long> issuerIds = list.Where(a => a.IssuedBy != null).Select(a => a.IssuedBy!.Value).Distinct().ToList();
		Dictionary<long, string> issuers = await db.Users.AsNoTracking().IgnoreQueryFilters()
			.Where(u => issuerIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
		List<WifiAccessRowDto> rows = new List<WifiAccessRowDto>();
		foreach (WifiAccess a in list)
		{
			Person? p = a.CustomerId is long cid && people.TryGetValue(cid, out Person? hit) ? hit : null;
			(string key, string label) = WifiEngine.DisplayStatus(a, inUse.Contains(a.AccessId), Now);
			if (key == "issued" && a.ControllerState == WifiControllerState.Live && a.DeliveredUtc == null && a.EmailStatus is "failed" or "none")
			{
				label = a.EmailStatus == "none" ? "No email address" : "Email failed";
			}
			if (a.ControllerState == WifiControllerState.Failed && a.Status is WifiAccessStatus.Issued or WifiAccessStatus.Active)
			{
				label = "Not created";
			}
			string? bookingNo = null, bookingLabel = null;
			if (latest.TryGetValue(a.AccessId, out WifiAccessSource? src))
			{
				bookingNo = src.RefNumber;
				bookingLabel = await engine.SourceLabelAsync(src.SourceType, src.RefId, false, ct);
			}
			else if (a.Kind == WifiAccessKind.Guest)
			{
				bookingLabel = "Guest · issued by hand";
			}
			List<WifiAccess> kids = children.Where(c => c.ParentAccessId == a.AccessId).ToList();
			string kind = a.Kind switch { WifiAccessKind.Booking => "booking", WifiAccessKind.Pass => "pass", WifiAccessKind.TeamMember => "team", _ => "guest" };
			rows.Add(new WifiAccessRowDto(a.AccessId, a.AccessNo, kind, a.Profile, a.CustomerId,
				a.Kind is WifiAccessKind.Guest or WifiAccessKind.TeamMember ? a.HolderName : p?.Name ?? a.HolderName,
				a.Kind is WifiAccessKind.Guest or WifiAccessKind.TeamMember ? a.HolderEmail : p?.Email ?? a.HolderEmail,
				a.Kind is WifiAccessKind.Guest or WifiAccessKind.TeamMember ? a.HolderMobile : p?.Mobile ?? a.HolderMobile,
				p?.Company, bookingNo, bookingLabel, a.Username, a.Ssid, a.ValidFromUtc, a.ValidUntilUtc,
				devices.GetValueOrDefault(a.AccessId), a.DeviceLimit, key, label, a.EmailStatus, a.EmailError, a.DeliveredUtc, a.SeenInAppUtc, a.WhatsAppUtc,
				a.ControllerState.ToString().ToLowerInvariant(), a.ControllerError, kids.Count,
				kids.Count == 0 ? null : kids.First().Username + (kids.Count > 1 ? $" + {kids.Count - 1} more" : ""),
				kids.Sum(k => devices.GetValueOrDefault(k.AccessId)), kids.Sum(k => k.DeviceLimit),
				a.RevokedReason, a.PauseReason, a.IssuedBy is long by ? issuers.GetValueOrDefault(by) ?? "Admin" : "System",
				sourceCounts.GetValueOrDefault(a.AccessId) > 0 && a.Kind is WifiAccessKind.Booking or WifiAccessKind.Pass));
		}
		return rows;
	}

	private async Task<WifiAccessRowDto> RowAsync(WifiAccess a, CancellationToken ct) => (await RowsAsync(new List<WifiAccess> { a }, ct))[0];

	private async Task<WifiAccess> LoadAsync(long id, CancellationToken ct) =>
		await db.Set<WifiAccess>().FirstOrDefaultAsync(a => a.AccessId == id, ct) ?? throw new NotFoundException("Wi-Fi access", id);

	// ---- connected now (spec 5.1) -----------------------------------------------------------------------

	private async Task<(List<WifiConnectedRowDto> Rows, WifiConnectedCardsDto Cards)> ConnectedRowsAsync(WifiConnectedRequest r, CancellationToken ct)
	{
		WifiConfig cfg = await engine.ConfigAsync(ct);
		DateTime now = Now;
		DateTime todayStart = IstClock.ToUtc(IstClock.ToIst(now).Date);
		List<WifiSession> open = await db.Set<WifiSession>().AsNoTracking().Where(s => s.EndedUtc == null && s.Result == WifiSessionResult.Connected).OrderBy(s => s.StartedUtc).ToListAsync(ct);
		List<long> aIds = open.Where(s => s.AccessId != null).Select(s => s.AccessId!.Value).Distinct().ToList();
		Dictionary<long, WifiAccess> accesses = await db.Set<WifiAccess>().AsNoTracking().Where(a => aIds.Contains(a.AccessId)).ToDictionaryAsync(a => a.AccessId, ct);
		Dictionary<long, Person> people = await PeopleAsync(accesses.Values.Where(a => a.CustomerId != null).Select(a => a.CustomerId!.Value), ct);
		Dictionary<long, WifiAccessSource> src = (await db.Set<WifiAccessSource>().AsNoTracking().Where(s => aIds.Contains(s.AccessId) && !s.IsCancelled).ToListAsync(ct))
			.GroupBy(s => s.AccessId).ToDictionary(g => g.Key, g => g.Where(s => s.StartUtc <= now).OrderByDescending(s => s.EndUtc).FirstOrDefault() ?? g.First());
		List<long> parentIds = accesses.Values.Where(a => a.ParentAccessId != null).Select(a => a.ParentAccessId!.Value).ToList();
		Dictionary<long, WifiAccess> parents = await db.Set<WifiAccess>().AsNoTracking().Where(a => parentIds.Contains(a.AccessId)).ToDictionaryAsync(a => a.AccessId, ct);
		HashSet<string> blocked = (await db.Set<WifiBlock>().AsNoTracking().Where(b => b.UnblockedUtc == null).Select(b => b.MacAddress).ToListAsync(ct)).ToHashSet();
		List<WifiConnectedRowDto> rows = new List<WifiConnectedRowDto>();
		Dictionary<long, int> seenPerAccess = new Dictionary<long, int>();
		foreach (WifiSession s in open)
		{
			WifiAccess? a = s.AccessId is long id && accesses.TryGetValue(id, out WifiAccess? hit) ? hit : null;
			Person? p = a?.CustomerId is long cid && people.TryGetValue(cid, out Person? ph) ? ph : null;
			string who = a == null ? "unknown" : a.Kind == WifiAccessKind.Guest ? "guest" : a.Kind == WifiAccessKind.TeamMember ? "team" : a.Profile is "daypass" or "short" or "meeting" ? "daypass" : "member";
			bool other = false;
			if (a != null)
			{
				int n = seenPerAccess.GetValueOrDefault(a.AccessId);
				other = n > 0;
				seenPerAccess[a.AccessId] = n + 1;
			}
			string? name = a == null ? null : a.Kind is WifiAccessKind.Guest or WifiAccessKind.TeamMember ? a.HolderName : p?.Name ?? a.HolderName;
			string? company = a?.Kind == WifiAccessKind.TeamMember && a.ParentAccessId is long pid && parents.TryGetValue(pid, out WifiAccess? par)
				? (await PeopleAsync(new[] { par.CustomerId ?? 0 }, ct)).Values.FirstOrDefault()?.Company ?? par.HolderName : p?.Company;
			string? bookingNo = null, bookingLabel = null;
			long srcId = a?.ParentAccessId ?? a?.AccessId ?? 0;
			if (src.TryGetValue(a?.AccessId ?? 0, out WifiAccessSource? sr) || src.TryGetValue(srcId, out sr))
			{
				bookingNo = sr.RefNumber;
				bookingLabel = await engine.SourceLabelAsync(sr.SourceType, sr.RefId, false, ct);
			}
			rows.Add(new WifiConnectedRowDto(s.SessionId, a?.AccessId, a?.AccessNo, a?.CustomerId, name, company, a == null ? null : a.Kind is WifiAccessKind.Guest or WifiAccessKind.TeamMember ? a.HolderMobile : p?.Mobile,
				a == null ? null : a.Kind is WifiAccessKind.Guest or WifiAccessKind.TeamMember ? a.HolderEmail : p?.Email, who, other, s.DeviceName, s.MacAddress, s.Ssid, s.AccessPoint, s.IpAddress,
				s.StartedUtc, s.BytesUp + s.BytesDown, bookingNo, bookingLabel, a?.ValidUntilUtc, blocked.Contains(s.MacAddress)));
		}
		// cards (computed over everyone online, before the filters)
		int onlinePeople = rows.Where(x => x.AccessId != null).Select(x => x.AccessId).Distinct().Count() + rows.Count(x => x.AccessId == null);
		List<long?> todayAccess = await db.Set<WifiSession>().AsNoTracking().Where(s => s.StartedUtc >= todayStart && s.Result == WifiSessionResult.Connected && s.AccessId != null)
			.Select(s => s.AccessId).Distinct().ToListAsync(ct);
		int customersToday = await db.Set<WifiAccess>().AsNoTracking().Where(a => todayAccess.Contains(a.AccessId) && a.CustomerId != null).Select(a => a.CustomerId).Distinct().CountAsync(ct);
		int withAccess = await db.Set<WifiAccess>().AsNoTracking().Where(a => a.CustomerId != null && (a.Status == WifiAccessStatus.Active || a.Status == WifiAccessStatus.Issued) && a.ValidFromUtc <= now && a.ValidUntilUtc > now)
			.Select(a => a.CustomerId).Distinct().CountAsync(ct);
		long bytesToday = await db.Set<WifiSession>().AsNoTracking().Where(s => s.Result == WifiSessionResult.Connected && (s.StartedUtc >= todayStart || s.EndedUtc == null || s.EndedUtc >= todayStart))
			.SumAsync(s => (long?)(s.BytesUp + s.BytesDown), ct) ?? 0;
		var failed = await db.Set<WifiSession>().AsNoTracking().Where(s => s.StartedUtc >= todayStart && s.Result != WifiSessionResult.Connected)
			.GroupBy(s => s.Result).Select(g => new { g.Key, N = g.Count() }).ToListAsync(ct);
		string today = IstClock.IstDateOf(now).ToString("yyyy-MM-dd", WifiEngine.Inv);
		int peak = cfg.Get("peak.date") == today && int.TryParse(cfg.Get("peak.count"), out int pk) ? pk : rows.Count;
		DateTime? peakAt = cfg.Get("peak.date") == today && DateTime.TryParse(cfg.Get("peak.atUtc"), WifiEngine.Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime pa) ? pa : null;
		WifiConnectedCardsDto cards = new WifiConnectedCardsDto(onlinePeople, rows.Count, customersToday, withAccess, rows.Count(x => x.Who is "guest" or "daypass"), bytesToday, Math.Max(peak, rows.Count), peakAt,
			failed.Sum(f => f.N), failed.Where(f => f.Key == WifiSessionResult.RefusedExpired).Sum(f => f.N), failed.Where(f => f.Key == WifiSessionResult.RefusedPassword).Sum(f => f.N),
			failed.Where(f => f.Key == WifiSessionResult.RefusedDeviceLimit).Sum(f => f.N));
		IEnumerable<WifiConnectedRowDto> q = rows;
		if (!string.IsNullOrWhiteSpace(r.Search))
		{
			string t = r.Search.Trim();
			q = q.Where(x => new[] { x.HolderName, x.Company, x.Mobile, x.Email, x.DeviceName, x.Mac, x.Ip, x.AccessNo, x.BookingNumber }.Any(v => v != null && v.Contains(t, StringComparison.OrdinalIgnoreCase)));
		}
		q = r.Who switch
		{
			"members" => q.Where(x => x.Who is "member" or "team"),
			"daypass" => q.Where(x => x.Who == "daypass"),
			"guests" => q.Where(x => x.Who == "guest"),
			"staff" or "unknown" => q.Where(x => x.Who == "unknown"),
			_ => q
		};
		if (!string.IsNullOrWhiteSpace(r.Ssid))
		{
			q = q.Where(x => x.Ssid == r.Ssid);
		}
		if (!string.IsNullOrWhiteSpace(r.Area))
		{
			q = q.Where(x => x.AccessPoint == r.Area);
		}
		return (q.ToList(), cards);
	}

	public async Task<WifiConnectedDto> ConnectedAsync(WifiConnectedRequest r, CancellationToken ct)
	{
		WifiConfig cfg = await engine.ConfigAsync(ct);
		(List<WifiConnectedRowDto> rows, WifiConnectedCardsDto cards) = await ConnectedRowsAsync(r, ct);
		int size = Math.Clamp(r.PageSize, 5, 200);
		int page = Math.Max(1, r.Page);
		List<string> areas = (await db.Set<WifiSession>().AsNoTracking().Where(s => s.AccessPoint != null && s.StartedUtc > Now.AddDays(-30)).Select(s => s.AccessPoint!).Distinct().ToListAsync(ct)).OrderBy(x => x).ToList();
		DateTime? last = DateTime.TryParse(cfg.Get("poll.lastUtc"), WifiEngine.Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime lp) ? lp : null;
		return new WifiConnectedDto(cards, PagedResult<WifiConnectedRowDto>.Create(rows.Skip((page - 1) * size).Take(size).ToList(), page, size, rows.Count),
			cfg.Networks.Where(n => !n.Internal || true).Select(n => n.Ssid).ToList(), areas, last, cfg.Get("poll.ok") != "0", cfg.Get("poll.error"), cfg.ControllerType, cfg.Mode);
	}

	public async Task<byte[]> ExportConnectedAsync(WifiConnectedRequest r, CancellationToken ct)
	{
		(List<WifiConnectedRowDto> rows, _) = await ConnectedRowsAsync(r, ct);
		return Excel("Connected now", new[] { "Customer", "Company", "Mobile", "Who", "Device", "MAC", "Network", "Area", "IP", "Connected since (IST)", "Minutes", "Data used (MB)", "Booking", "Access" },
			rows.Select(x => new object?[] { x.HolderName ?? "Unrecognised device", x.Company, x.Mobile, x.Who, x.DeviceName, x.Mac, x.Ssid, x.AccessPoint, x.Ip,
				IstClock.ToIst(x.StartedUtc), (int)(Now - x.StartedUtc).TotalMinutes, Math.Round(x.Bytes / 1048576.0, 1), x.BookingNumber, x.AccessNo }));
	}

	private static byte[] Excel(string sheet, string[] headers, IEnumerable<object?[]> data)
	{
		using XLWorkbook wb = new XLWorkbook();
		IXLWorksheet ws = wb.AddWorksheet(sheet);
		for (int i = 0; i < headers.Length; i++)
		{
			ws.Cell(1, i + 1).Value = headers[i];
			ws.Cell(1, i + 1).Style.Font.Bold = true;
		}
		int row = 2;
		foreach (object?[] r in data)
		{
			for (int i = 0; i < r.Length; i++)
			{
				IXLCell c = ws.Cell(row, i + 1);
				switch (r[i])
				{
					case null: break;
					case DateTime d: c.Value = d; c.Style.DateFormat.Format = "dd-mmm-yyyy hh:mm"; break;
					case int n: c.Value = n; break;
					case long l: c.Value = l; break;
					case double f: c.Value = f; break;
					default: c.Value = r[i]!.ToString(); break;
				}
			}
			row++;
		}
		ws.Columns().AdjustToContents(1, Math.Min(row, 300));
		using MemoryStream ms = new MemoryStream();
		wb.SaveAs(ms);
		return ms.ToArray();
	}

	// ---- access issued (spec 5.2) ---------------------------------------------------------------------

	public async Task<WifiAccessListDto> AccessListAsync(WifiAccessListRequest r, CancellationToken ct)
	{
		DateTime now = Now;
		DateTime endOfToday = IstClock.ToUtc(IstClock.ToIst(now).Date.AddDays(1));
		IQueryable<WifiAccess> q = db.Set<WifiAccess>().AsNoTracking().Where(a => a.ParentAccessId == null);
		if (!string.IsNullOrWhiteSpace(r.Search))
		{
			string t = r.Search.Trim();
			IQueryable<long> users = db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.FullName.Contains(t) || u.Email.Contains(t) || u.MobileNumber.Contains(t) || (u.CompanyName != null && u.CompanyName.Contains(t))).Select(u => u.UserId);
			IQueryable<long> srcs = db.Set<WifiAccessSource>().Where(s => s.RefNumber.Contains(t)).Select(s => s.AccessId);
			IQueryable<long?> parents = db.Set<WifiAccess>().Where(c => c.ParentAccessId != null && (c.Username.Contains(t) || c.HolderName.Contains(t))).Select(c => c.ParentAccessId);
			q = q.Where(a => a.Username.Contains(t) || a.AccessNo.Contains(t) || a.HolderName.Contains(t) || (a.HolderMobile != null && a.HolderMobile.Contains(t))
				|| (a.CustomerId != null && users.Contains(a.CustomerId.Value)) || srcs.Contains(a.AccessId) || parents.Contains(a.AccessId));
		}
		if (!string.IsNullOrWhiteSpace(r.Kind))
		{
			q = r.Kind == "team" ? q.Where(a => db.Set<WifiAccess>().Any(c => c.ParentAccessId == a.AccessId)) : q.Where(a => a.Profile == r.Kind);
		}
		string period = r.Period ?? "month";
		if (period != "all")
		{
			DateTime from = period switch
			{
				"7" => now.AddDays(-7),
				"30" => now.AddDays(-30),
				"90" => now.AddDays(-90),
				_ => IstClock.ToUtc(new DateTime(IstClock.ToIst(now).Year, IstClock.ToIst(now).Month, 1))
			};
			q = q.Where(a => a.ValidUntilUtc >= from || a.Status == WifiAccessStatus.Active || a.Status == WifiAccessStatus.Issued || a.Status == WifiAccessStatus.Paused);
		}
		IQueryable<WifiAccess> active = q.Where(a => a.Status == WifiAccessStatus.Active);
		IQueryable<WifiAccess> expiring = q.Where(a => (a.Status == WifiAccessStatus.Active || a.Status == WifiAccessStatus.Issued) && a.ValidUntilUtc > now && a.ValidUntilUtc <= endOfToday);
		IQueryable<WifiAccess> undelivered = q.Where(a => a.DeliveredUtc == null && (a.Status == WifiAccessStatus.Issued) && (a.EmailStatus == "failed" || a.EmailStatus == "none" || a.ControllerState == WifiControllerState.Failed));
		WifiAccessCountsDto counts = new WifiAccessCountsDto(await q.CountAsync(ct), await active.CountAsync(ct), await expiring.CountAsync(ct),
			await q.CountAsync(a => a.Status == WifiAccessStatus.Expired, ct), await q.CountAsync(a => a.Status == WifiAccessStatus.Revoked, ct), await undelivered.CountAsync(ct),
			await q.CountAsync(a => a.Status == WifiAccessStatus.Paused, ct));
		q = r.Chip switch
		{
			"active" => active,
			"expiring" => expiring,
			"expired" => q.Where(a => a.Status == WifiAccessStatus.Expired),
			"revoked" => q.Where(a => a.Status == WifiAccessStatus.Revoked),
			"paused" => q.Where(a => a.Status == WifiAccessStatus.Paused),
			"undelivered" => undelivered,
			_ => q
		};
		int size = Math.Clamp(r.PageSize, 5, 200);
		int page = Math.Max(1, r.Page);
		int total = await q.CountAsync(ct);
		List<WifiAccess> list = await q.OrderBy(a => a.Status == WifiAccessStatus.Active || a.Status == WifiAccessStatus.Issued || a.Status == WifiAccessStatus.Paused ? 0 : 1)
			.ThenByDescending(a => a.ValidUntilUtc).ThenByDescending(a => a.AccessId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
		return new WifiAccessListDto(counts, PagedResult<WifiAccessRowDto>.Create(await RowsAsync(list, ct), page, size, total));
	}

	public async Task<WifiAccessDetailDto> AccessAsync(long id, CancellationToken ct)
	{
		WifiAccess a = await db.Set<WifiAccess>().AsNoTracking().FirstOrDefaultAsync(x => x.AccessId == id, ct) ?? throw new NotFoundException("Wi-Fi access", id);
		List<WifiAccess> kids = await db.Set<WifiAccess>().AsNoTracking().Where(c => c.ParentAccessId == id).OrderBy(c => c.AccessId).ToListAsync(ct);
		List<WifiSession> open = await db.Set<WifiSession>().AsNoTracking().Where(s => s.AccessId == id && s.EndedUtc == null && s.Result == WifiSessionResult.Connected).ToListAsync(ct);
		List<WifiDeviceDto> devices = (await db.Set<WifiDevice>().AsNoTracking().Where(d => d.AccessId == id).OrderBy(d => d.RemovedUtc != null).ThenBy(d => d.FirstSeenUtc).ToListAsync(ct))
			.Select(d =>
			{
				WifiSession? on = open.FirstOrDefault(s => s.MacAddress == d.MacAddress);
				return new WifiDeviceDto(d.DeviceId, d.MacAddress, d.DeviceName, d.FirstSeenUtc, d.LastSeenUtc, on != null && d.RemovedUtc == null, on?.StartedUtc, d.RemovedUtc, d.RemovedBy);
			}).ToList();
		List<WifiEvent> evs = await db.Set<WifiEvent>().AsNoTracking().Where(e => e.AccessId == id).OrderByDescending(e => e.CreatedUtc).Take(200).ToListAsync(ct);
		List<long> actorIds = evs.Where(e => e.ActorUserId != null).Select(e => e.ActorUserId!.Value).Distinct().ToList();
		Dictionary<long, string> actors = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => actorIds.Contains(u.UserId))
			.ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
		List<WifiSourceDto> sources = new List<WifiSourceDto>();
		foreach (WifiAccessSource s in await db.Set<WifiAccessSource>().AsNoTracking().Where(s => s.AccessId == id).OrderBy(s => s.StartUtc).ToListAsync(ct))
		{
			sources.Add(new WifiSourceDto(s.SourceType == 1 ? "booking" : "pass", s.RefNumber, await engine.SourceLabelAsync(s.SourceType, s.RefId, true, ct), s.StartUtc, s.EndUtc, s.IsCancelled));
		}
		return new WifiAccessDetailDto(await RowAsync(a, ct), devices,
			evs.Select(e => new WifiEventDto(e.EventId, e.Event, e.Detail, e.ActorUserId is long u ? actors.GetValueOrDefault(u) ?? "Admin" : "System", e.CreatedUtc)).ToList(),
			await RowsAsync(kids, ct), sources);
	}

	// ---- actions (spec 3.1) ----------------------------------------------------------------------------

	public async Task<WifiAccessRowDto> IssueGuestAsync(WifiGuestRequest r, CancellationToken ct)
	{
		WifiConfig cfg = await engine.ConfigAsync(ct);
		string name = WifiEngine.Clip(r.Name, 150) ?? throw new BusinessRuleException("Write the guest's name.");
		string? email = WifiEngine.Clip(r.Email, 256);
		string? mobile = WifiEngine.Clip(r.Mobile, 20);
		if (email == null && mobile == null)
		{
			throw new BusinessRuleException("Give an email or a mobile number, so the details can reach the guest.");
		}
		if (email != null && !System.Text.RegularExpressions.Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
		{
			throw new BusinessRuleException("That email address does not look right.");
		}
		DateTime from = r.ValidFromUtc ?? Now;
		if (r.ValidUntilUtc <= Now || r.ValidUntilUtc <= from)
		{
			throw new BusinessRuleException("The end time must be in the future.");
		}
		if (r.ValidUntilUtc > from.AddDays(31))
		{
			throw new BusinessRuleException("Guest access lasts at most 31 days. Members get access through a booking or pass.");
		}
		string profileKey = r.Profile is "guest" or "daypass" or "member" or "meeting" or "short" ? r.Profile : "guest";
		WifiProfileDto profile = cfg.Profile(profileKey);
		if (string.IsNullOrEmpty(profile.Ssid))
		{
			profile = cfg.Profile("guest");
		}
		int limit = Math.Clamp(r.DeviceLimit ?? profile.DeviceLimit, 1, 10);
		WifiAccess a = await engine.NewAccessAsync(WifiAccessKind.Guest, "guest", r.CustomerId, name, email, mobile, null, profile.Ssid, from, r.ValidUntilUtc, limit, Me, ct);
		a.Note = WifiEngine.Clip(r.Note, 500);
		await db.SaveChangesAsync(ct);
		engine.Log(a.AccessId, "issued", $"Guest access issued by hand: {a.Username} on {a.Ssid}, {limit} devices, until {WifiEngine.IstDateTime(a.ValidUntilUtc)}" + (a.Note == null ? "." : $". Note: {a.Note}"), Me);
		await db.SaveChangesAsync(ct);
		if (from <= Now.AddHours(1) && await engine.PushAsync(a, ct))
		{
			await engine.DeliverAsync(a, "issued", ct);
		}
		await db.SaveChangesAsync(ct);
		return await RowAsync(a, ct);
	}

	public async Task<WifiAccessRowDto> AddMemberAsync(long id, WifiMemberRequest r, CancellationToken ct)
	{
		WifiAccess parent = await LoadAsync(id, ct);
		if (parent.Kind is not (WifiAccessKind.Booking or WifiAccessKind.Pass) || parent.Status is WifiAccessStatus.Revoked or WifiAccessStatus.Expired)
		{
			throw new BusinessRuleException("Team members can be added to a current booking or membership access only.");
		}
		string name = WifiEngine.Clip(r.Name, 150) ?? throw new BusinessRuleException("Write the team member's name.");
		if (WifiEngine.Clip(r.Email, 256) == null && WifiEngine.Clip(r.Mobile, 20) == null)
		{
			throw new BusinessRuleException("Give the team member's email or mobile, so their details can reach them.");
		}
		WifiConfig cfg = await engine.ConfigAsync(ct);
		string? company = parent.CustomerId is long cid ? await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.UserId == cid).Select(u => u.CompanyName).FirstOrDefaultAsync(ct) : null;
		WifiAccess m = await engine.NewAccessAsync(WifiAccessKind.TeamMember, parent.Profile, parent.CustomerId, name, r.Email, r.Mobile, company ?? parent.HolderName,
			parent.Ssid, parent.ValidFromUtc, parent.ValidUntilUtc, cfg.Profile(parent.Profile).DeviceLimit, Me, ct);
		m.ParentAccessId = parent.AccessId;
		if (parent.Status == WifiAccessStatus.Paused)
		{
			m.Status = WifiAccessStatus.Paused;
			m.PauseReason = parent.PauseReason;
			m.PausedUtc = Now;
		}
		await db.SaveChangesAsync(ct);
		engine.Log(m.AccessId, "issued", $"Team member access for {name} on company access {parent.AccessNo}: {m.Username}.", Me);
		engine.Log(parent.AccessId, "member", $"Team member {name} added ({m.AccessNo}, {m.Username}).", Me);
		await db.SaveChangesAsync(ct);
		if (m.Status == WifiAccessStatus.Issued && m.ValidFromUtc <= Now.AddHours(1) && await engine.PushAsync(m, ct))
		{
			await engine.DeliverAsync(m, "issued", ct);
		}
		await db.SaveChangesAsync(ct);
		return await RowAsync(m, ct);
	}

	public async Task<WifiResendResultDto> ResendAsync(long id, WifiResendRequest r, CancellationToken ct)
	{
		WifiAccess a = await LoadAsync(id, ct);
		if (a.Status is WifiAccessStatus.Revoked or WifiAccessStatus.Expired)
		{
			throw new BusinessRuleException("This access has ended. Extend it (or re-issue) first.");
		}
		if (a.ControllerState != WifiControllerState.Live)
		{
			throw new BusinessRuleException(a.Status == WifiAccessStatus.Paused ? "This access is paused for an overdue invoice." : "This access is not in the Wi-Fi controller yet, so it would not work. Retry the controller first.");
		}
		if (r.Channel == "whatsapp")
		{
			string url = await engine.WhatsAppUrlAsync(a, ct) ?? throw new BusinessRuleException("There is no mobile number to send a WhatsApp message to.");
			a.WhatsAppUtc = Now;
			a.DeliveredUtc ??= Now;
			if (a.Status == WifiAccessStatus.Issued)
			{
				a.Status = WifiAccessStatus.Active;
			}
			engine.Log(a.AccessId, "resent", $"WhatsApp message opened for {a.HolderMobile} (click-to-chat).", Me);
			await db.SaveChangesAsync(ct);
			return new WifiResendResultDto("whatsapp", true, "WhatsApp opened with the message ready to send.", url);
		}
		string? error = await engine.SendEmailAsync(a, "resend", ct);
		if (error == null)
		{
			a.DeliveredUtc ??= Now;
			if (a.Status == WifiAccessStatus.Issued)
			{
				a.Status = WifiAccessStatus.Active;
			}
		}
		engine.Log(a.AccessId, "resent", error == null ? $"Details emailed again to {a.HolderEmail}." : $"Resend by email failed: {error}", Me);
		await db.SaveChangesAsync(ct);
		return new WifiResendResultDto("email", error == null, error == null ? $"Emailed to {a.HolderEmail}." : "The email could not be sent: " + error, null);
	}

	public async Task<WifiAccessRowDto> ResetAsync(long id, CancellationToken ct)
	{
		WifiAccess a = await LoadAsync(id, ct);
		if (a.Status is WifiAccessStatus.Revoked or WifiAccessStatus.Expired)
		{
			throw new BusinessRuleException("This access has ended; there is no password to reset.");
		}
		WifiConfig cfg = await engine.ConfigAsync(ct);
		if (cfg.Mode == "shared")
		{
			throw new BusinessRuleException("In shared-password mode everyone has the same password: change it in Settings (and on the Wi-Fi equipment).");
		}
		bool wasLive = a.ControllerState == WifiControllerState.Live;
		await engine.RemoveFromControllerAsync(a, "password reset", ct);
		a.PasswordEnc = engine.Secrets.Encrypt(WifiSecrets.NewPassword(a.HolderName));
		a.ControllerState = WifiControllerState.Pending;
		a.ControllerAttempts = 0;
		a.ControllerNextTryUtc = null;
		a.UpdatedUtc = Now;
		await db.Set<WifiDevice>().Where(d => d.AccessId == a.AccessId && d.RemovedUtc == null)
			.ExecuteUpdateAsync(u => u.SetProperty(d => d.RemovedUtc, (DateTime?)Now).SetProperty(d => d.RemovedBy, "reset"), ct);
		engine.Log(a.AccessId, "reset", "New password issued for this customer only; every device was signed out and must join with the new one.", Me);
		await db.SaveChangesAsync(ct);
		if (a.Status != WifiAccessStatus.Paused && a.ValidFromUtc <= Now.AddHours(1) && await engine.PushAsync(a, ct) && (wasLive || a.DeliveredUtc == null))
		{
			await engine.DeliverAsync(a, "reset", ct);
		}
		await db.SaveChangesAsync(ct);
		return await RowAsync(a, ct);
	}

	public async Task<WifiAccessRowDto> UpdateAsync(long id, WifiUpdateAccessRequest r, CancellationToken ct)
	{
		WifiAccess a = await LoadAsync(id, ct);
		if (a.Status == WifiAccessStatus.Revoked)
		{
			throw new BusinessRuleException("Revoked access stays revoked. Issue new access instead.");
		}
		string reason = WifiEngine.Clip(r.Reason, 300) ?? throw new BusinessRuleException("Write why (it goes in the access log).");
		if (r.DeviceLimit is int limit)
		{
			if (limit < 1 || limit > 20)
			{
				throw new BusinessRuleException("The device limit is between 1 and 20.");
			}
			if (limit != a.DeviceLimit)
			{
				engine.Log(a.AccessId, "limit", $"Device limit {a.DeviceLimit} → {limit}. {reason}", Me);
				a.DeviceLimit = limit;
			}
		}
		if (r.ValidUntilUtc is DateTime until && until != a.ValidUntilUtc)
		{
			if (until <= Now)
			{
				throw new BusinessRuleException("The new end must be in the future. To end it now, use Revoke.");
			}
			engine.Log(a.AccessId, "extended", $"Valid until {WifiEngine.IstDateTime(a.ValidUntilUtc)} → {WifiEngine.IstDateTime(until)}. {reason}", Me);
			a.ValidUntilUtc = until;
			a.ReminderSentUtc = null;
			if (a.Kind is WifiAccessKind.Booking or WifiAccessKind.Pass)
			{
				foreach (WifiAccess c in await db.Set<WifiAccess>().Where(c => c.ParentAccessId == a.AccessId && c.Status != WifiAccessStatus.Revoked).ToListAsync(ct))
				{
					c.ValidUntilUtc = until;
				}
			}
			if (a.Status == WifiAccessStatus.Expired)
			{
				await engine.ResumeAsync(a, "Re-issued by an admin.", Me, ct);
				if (a.ControllerState == WifiControllerState.Live)
				{
					await engine.DeliverAsync(a, "renewed", ct);
				}
			}
		}
		a.UpdatedUtc = Now;
		await db.SaveChangesAsync(ct);
		return await RowAsync(a, ct);
	}

	public async Task<WifiAccessRowDto> RevokeAsync(long id, WifiRevokeRequest r, CancellationToken ct)
	{
		WifiAccess a = await LoadAsync(id, ct);
		string reason = WifiEngine.Clip(r.Reason, 500) ?? throw new BusinessRuleException("Write the reason for revoking (it is recorded).");
		if (a.Status == WifiAccessStatus.Revoked)
		{
			throw new BusinessRuleException("Already revoked.");
		}
		await engine.RevokeAsync(a, reason, Me, ct);
		await db.SaveChangesAsync(ct);
		return await RowAsync(a, ct);
	}

	public async Task<WifiAccessRowDto> RetryControllerAsync(long id, CancellationToken ct)
	{
		WifiAccess a = await LoadAsync(id, ct);
		if (a.Status is not (WifiAccessStatus.Issued or WifiAccessStatus.Active) || a.ControllerState == WifiControllerState.Live)
		{
			throw new BusinessRuleException("Nothing to retry: it is already in the controller, or it has ended.");
		}
		a.ControllerAttempts = 0;
		a.ControllerState = WifiControllerState.Pending;
		a.ControllerNextTryUtc = null;
		engine.Log(a.AccessId, "retry", "Controller write retried by an admin.", Me);
		bool ok = a.ValidFromUtc <= Now.AddHours(1) && await engine.PushAsync(a, ct);
		if (ok && a.DeliveredUtc == null)
		{
			await engine.DeliverAsync(a, "issued", ct);
		}
		await db.SaveChangesAsync(ct);
		return await RowAsync(a, ct);
	}

	public async Task RemoveDeviceAsync(long accessId, long deviceId, CancellationToken ct)
	{
		WifiDevice d = await db.Set<WifiDevice>().FirstOrDefaultAsync(x => x.DeviceId == deviceId && x.AccessId == accessId && x.RemovedUtc == null, ct)
			?? throw new NotFoundException("Device", deviceId);
		await RemoveDeviceCoreAsync(engine, db, d, "admin", Me, ct);
		await db.SaveChangesAsync(ct);
	}

	internal static async Task RemoveDeviceCoreAsync(WifiEngine engine, RegalisDbContext db, WifiDevice d, string by, long? actor, CancellationToken ct)
	{
		d.RemovedUtc = engine.Now;
		d.RemovedBy = by;
		try
		{
			await (await engine.ControllerAsync(ct)).DisconnectAsync(d.MacAddress, false, ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			engine.Log(d.AccessId, "controller.failed", $"Could not disconnect {d.MacAddress}: {ex.Message}", actor);
		}
		DateTime now = engine.Now;
		await db.Set<WifiSession>().Where(s => s.AccessId == d.AccessId && s.MacAddress == d.MacAddress && s.EndedUtc == null)
			.ExecuteUpdateAsync(u => u.SetProperty(s => s.EndedUtc, (DateTime?)now), ct);
		engine.Log(d.AccessId, "device.removed", $"Device {d.DeviceName ?? d.MacAddress} ({d.MacAddress}) removed by the {by}; its slot is free.", actor);
	}

	public async Task DisconnectAsync(string mac, WifiDisconnectRequest r, CancellationToken ct)
	{
		string m = WifiEngine.NormaliseMac(mac);
		IWifiController ctrl = await engine.ControllerAsync(ct);
		await ctrl.DisconnectAsync(m, r.Block, ct);
		DateTime now = Now;
		List<WifiSession> open = await db.Set<WifiSession>().Where(s => s.MacAddress == m && s.EndedUtc == null).ToListAsync(ct);
		foreach (WifiSession s in open)
		{
			s.EndedUtc = now;
		}
		long? accessId = open.FirstOrDefault()?.AccessId;
		if (r.Block)
		{
			if (!await db.Set<WifiBlock>().AnyAsync(b => b.MacAddress == m && b.UnblockedUtc == null, ct))
			{
				db.Set<WifiBlock>().Add(new WifiBlock { MacAddress = m, Reason = WifiEngine.Clip(r.Reason, 300), BlockedBy = Me, BlockedUtc = now });
			}
			engine.Log(accessId, "blocked", $"Device {m} blocked." + (r.Reason == null ? "" : " " + r.Reason), Me);
		}
		else
		{
			engine.Log(accessId, "disconnected", $"Device {m} disconnected by an admin.", Me);
		}
		await db.SaveChangesAsync(ct);
	}

	public async Task UnblockAsync(string mac, CancellationToken ct)
	{
		string m = WifiEngine.NormaliseMac(mac);
		await (await engine.ControllerAsync(ct)).UnblockAsync(m, ct);
		foreach (WifiBlock b in await db.Set<WifiBlock>().Where(b => b.MacAddress == m && b.UnblockedUtc == null).ToListAsync(ct))
		{
			b.UnblockedUtc = Now;
			b.UnblockedBy = Me;
		}
		engine.Log(null, "unblocked", $"Device {m} unblocked.", Me);
		await db.SaveChangesAsync(ct);
	}

	// ---- usage log -----------------------------------------------------------------------------------

	private IQueryable<WifiSession> SessionQuery(WifiSessionRequest r)
	{
		DateTime to = r.ToUtc ?? Now.AddMinutes(1);
		DateTime from = r.FromUtc ?? Now.AddDays(-7);
		IQueryable<WifiSession> q = db.Set<WifiSession>().AsNoTracking().Where(s => s.StartedUtc <= to && (s.EndedUtc == null || s.EndedUtc >= from));
		if (r.AccessId is long id)
		{
			q = q.Where(s => s.AccessId == id);
		}
		q = r.Result switch
		{
			"connected" => q.Where(s => s.Result == WifiSessionResult.Connected && s.AccessId != null),
			"refused" => q.Where(s => s.Result != WifiSessionResult.Connected),
			"unknown" => q.Where(s => s.AccessId == null && s.Result == WifiSessionResult.Connected),
			_ => q
		};
		if (!string.IsNullOrWhiteSpace(r.Search))
		{
			string t = r.Search.Trim();
			IQueryable<long> users = db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.FullName.Contains(t) || u.MobileNumber.Contains(t)).Select(u => u.UserId);
			IQueryable<long> acc = db.Set<WifiAccess>().Where(a => a.Username.Contains(t) || a.AccessNo.Contains(t) || a.HolderName.Contains(t) || (a.CustomerId != null && users.Contains(a.CustomerId.Value))).Select(a => a.AccessId);
			q = q.Where(s => s.MacAddress.Contains(t) || (s.DeviceName != null && s.DeviceName.Contains(t)) || (s.IpAddress != null && s.IpAddress.Contains(t)) || (s.AccessId != null && acc.Contains(s.AccessId.Value)));
		}
		return q;
	}

	private async Task<List<WifiSessionDto>> SessionRowsAsync(List<WifiSession> list, CancellationToken ct)
	{
		List<long> ids = list.Where(s => s.AccessId != null).Select(s => s.AccessId!.Value).Distinct().ToList();
		Dictionary<long, WifiAccess> acc = await db.Set<WifiAccess>().AsNoTracking().Where(a => ids.Contains(a.AccessId)).ToDictionaryAsync(a => a.AccessId, ct);
		Dictionary<long, Person> people = await PeopleAsync(acc.Values.Where(a => a.CustomerId != null && a.Kind is WifiAccessKind.Booking or WifiAccessKind.Pass).Select(a => a.CustomerId!.Value), ct);
		return list.Select(s =>
		{
			WifiAccess? a = s.AccessId is long id && acc.TryGetValue(id, out WifiAccess? hit) ? hit : null;
			string? name = a == null ? null : a.CustomerId is long cid && a.Kind is WifiAccessKind.Booking or WifiAccessKind.Pass && people.TryGetValue(cid, out Person? p) ? p.Name : a.HolderName;
			DateTime end = s.EndedUtc ?? Now;
			string result = s.Result switch
			{
				WifiSessionResult.RefusedPassword => "refused (wrong password)",
				WifiSessionResult.RefusedExpired => "refused (expired)",
				WifiSessionResult.RefusedDeviceLimit => "refused (device limit)",
				_ => s.AccessId == null ? "connected (no customer matched)" : s.EndedUtc == null ? "connected (online now)" : "connected"
			};
			return new WifiSessionDto(s.SessionId, s.AccessId, a?.AccessNo, name, s.DeviceName, s.MacAddress, s.Ssid, s.AccessPoint, s.StartedUtc, s.EndedUtc,
				(int)Math.Max(0, (end - s.StartedUtc).TotalMinutes), s.BytesUp + s.BytesDown, result);
		}).ToList();
	}

	public async Task<PagedResult<WifiSessionDto>> SessionsAsync(WifiSessionRequest r, CancellationToken ct)
	{
		IQueryable<WifiSession> q = SessionQuery(r);
		int size = Math.Clamp(r.PageSize, 5, 500);
		int page = Math.Max(1, r.Page);
		int total = await q.CountAsync(ct);
		List<WifiSession> list = await q.OrderByDescending(s => s.StartedUtc).ThenByDescending(s => s.SessionId).Skip((page - 1) * size).Take(size).ToListAsync(ct);
		return PagedResult<WifiSessionDto>.Create(await SessionRowsAsync(list, ct), page, size, total);
	}

	public async Task<byte[]> ExportSessionsAsync(WifiSessionRequest r, CancellationToken ct)
	{
		List<WifiSession> list = await SessionQuery(r).OrderByDescending(s => s.StartedUtc).Take(50000).ToListAsync(ct);
		List<WifiSessionDto> rows = await SessionRowsAsync(list, ct);
		return Excel("Usage log", new[] { "Started (IST)", "Ended (IST)", "Minutes", "Customer", "Access", "Device", "MAC", "Network", "Access point", "Data used (MB)", "Result" },
			rows.Select(x => new object?[] { IstClock.ToIst(x.StartedUtc), x.EndedUtc is DateTime e ? IstClock.ToIst(e) : null, x.Minutes, x.HolderName, x.AccessNo, x.DeviceName, x.Mac, x.Ssid, x.AccessPoint,
				Math.Round(x.Bytes / 1048576.0, 1), x.Result }));
	}

	// ---- settings (spec 5.3) -------------------------------------------------------------------------

	public async Task<WifiSettingsDto> GetSettingsAsync(CancellationToken ct)
	{
		engine.ForgetConfig();
		WifiConfig c = await engine.ConfigAsync(ct);
		DateTime? last = DateTime.TryParse(c.Get("poll.lastUtc"), WifiEngine.Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime lp) ? lp : null;
		int clients = await db.Set<WifiSession>().AsNoTracking().CountAsync(s => s.EndedUtc == null && s.Result == WifiSessionResult.Connected, ct);
		return new WifiSettingsDto(c.ControllerType, c.ControllerUrl, c.ControllerUser, null, c.ControllerKeyEnc != null, c.ControllerSite,
			c.Mode, null, c.SharedPasswordEnc != null, c.Networks, c.Profiles.Values.ToList(),
			c.AutoIssue, c.IssueSinceUtc == DateTime.MaxValue ? null : c.IssueSinceUtc, c.PayLater,
			c.DeliverEmail, c.DeliverWhatsApp, c.DeliverQr, c.Reminder, c.DayPassUntil, c.IdleMinutes, c.RuleCancel,
			c.Get("rules.overdue") == "0" ? 0 : (int.TryParse(c.Get("rules.overdueDays"), out int od) ? od : 7), c.RuleUnknownDevice, c.RetentionMonths,
			c.Get("poll.ok") != "0" && last != null, c.Get("poll.error"), last, c.ControllerType == "simulated" ? SimulatedWifiController.AccessPoints.Length : null, clients);
	}

	public async Task<WifiSettingsDto> SaveSettingsAsync(WifiSettingsDto r, CancellationToken ct)
	{
		if (!WifiControllerFactory.Types.Contains(r.ControllerType))
		{
			throw new BusinessRuleException("Choose a controller type from the list.");
		}
		if (!WifiControllerFactory.IsInstalled(r.ControllerType))
		{
			throw new BusinessRuleException($"There is no {r.ControllerType} adapter in this build yet. Keep \"Simulated\" until the Wi-Fi hardware is confirmed (open item 1) and its adapter is added.");
		}
		if (r.Mode is not ("per_customer" or "portal" or "shared"))
		{
			throw new BusinessRuleException("Choose how access is created.");
		}
		WifiConfig old = await engine.ConfigAsync(ct);
		if (r.Mode == "shared" && string.IsNullOrWhiteSpace(r.SharedPassword) && old.SharedPasswordEnc == null)
		{
			throw new BusinessRuleException("Type the shared password: in this mode it is the one everybody is sent.");
		}
		if (!string.IsNullOrWhiteSpace(r.SharedPassword) && r.SharedPassword.Trim().Length is < 8 or > 63)
		{
			throw new BusinessRuleException("A Wi-Fi password is 8 to 63 characters.");
		}
		List<WifiNetworkDto> networks = (r.Networks ?? new List<WifiNetworkDto>()).Where(n => !string.IsNullOrWhiteSpace(n.Ssid))
			.Select(n => new WifiNetworkDto(n.Ssid.Trim(), Math.Clamp(n.SpeedMbps, 0, 10000), WifiEngine.Clip(n.Purpose, 200) ?? "", n.Internal)).ToList();
		if (networks.Count == 0 || networks.Select(n => n.Ssid).Distinct(StringComparer.OrdinalIgnoreCase).Count() != networks.Count || networks.Any(n => n.Ssid.Length > 32))
		{
			throw new BusinessRuleException("List each network once; a network name is at most 32 characters.");
		}
		List<WifiProfileDto> profiles = new List<WifiProfileDto>();
		foreach (WifiProfileDto d in WifiEngine.DefaultProfiles())
		{
			WifiProfileDto? p = r.Profiles?.FirstOrDefault(x => x.Key == d.Key);
			string ssid = p?.Ssid?.Trim() ?? d.Ssid;
			if (ssid.Length > 0 && !networks.Any(n => n.Ssid == ssid && !n.Internal))
			{
				throw new BusinessRuleException($"\"{d.Label}\" uses {ssid}, which is not a customer network in the list.");
			}
			int limit = p?.DeviceLimit ?? d.DeviceLimit;
			if (ssid.Length > 0 && (limit < 1 || limit > 20))
			{
				throw new BusinessRuleException("Device limits are between 1 and 20.");
			}
			profiles.Add(d with { Ssid = ssid, DeviceLimit = ssid.Length == 0 ? 0 : limit });
		}
		if (r.DayPassUntil is not ("closing" or "24h") || r.OverdueDays is < 0 or > 90 || r.RetentionMonths is < 1 or > 120 || r.IdleMinutes is < 0 or > 1440)
		{
			throw new BusinessRuleException("One of the rules is out of range.");
		}
		async Task Set(string k, string? v) => await engine.SetAsync(k, v, Me, ct);
		await Set("controller.type", r.ControllerType);
		await Set("controller.url", WifiEngine.Clip(r.ControllerUrl, 300));
		await Set("controller.user", WifiEngine.Clip(r.ControllerUser, 120));
		await Set("controller.site", WifiEngine.Clip(r.ControllerSite, 120));
		if (!string.IsNullOrWhiteSpace(r.ControllerKey))
		{
			await Set("controller.key", engine.Secrets.Encrypt(r.ControllerKey.Trim()));
		}
		await Set("mode", r.Mode);
		if (!string.IsNullOrWhiteSpace(r.SharedPassword))
		{
			await Set("shared.password", engine.Secrets.Encrypt(r.SharedPassword.Trim()));
		}
		await Set("networks", JsonSerializer.Serialize(networks, Json));
		await Set("profiles", JsonSerializer.Serialize(profiles, Json));
		await Set("issue.auto", r.AutoIssue ? "1" : "0");
		if (r.AutoIssue && old.IssueSinceUtc == DateTime.MaxValue)
		{
			await Set("issue.sinceUtc", Now.ToString("o"));
		}
		if (r.IssueSinceUtc is DateTime since && since < old.IssueSinceUtc)
		{
			await Set("issue.sinceUtc", since.ToString("o"));
			engine.Log(null, "settings", $"Issuing back-dated: bookings and passes paid since {WifiEngine.IstDateTime(since)} that are still running get access.", Me);
		}
		await Set("issue.payLater", r.PayLater ? "1" : "0");
		await Set("delivery.email", r.DeliverEmail ? "1" : "0");
		await Set("delivery.whatsapp", r.DeliverWhatsApp ? "1" : "0");
		await Set("delivery.qr", r.DeliverQr ? "1" : "0");
		await Set("delivery.reminder", r.Reminder ? "1" : "0");
		await Set("rules.dayPassUntil", r.DayPassUntil);
		await Set("rules.idleMinutes", r.IdleMinutes.ToString(WifiEngine.Inv));
		await Set("rules.cancel", r.RuleCancel ? "1" : "0");
		await Set("rules.overdue", r.OverdueDays > 0 ? "1" : "0");
		if (r.OverdueDays > 0)
		{
			await Set("rules.overdueDays", r.OverdueDays.ToString(WifiEngine.Inv));
		}
		await Set("rules.unknownDevice", r.RuleUnknownDevice ? "1" : "0");
		await Set("retention.months", r.RetentionMonths.ToString(WifiEngine.Inv));
		if (old.Mode != r.Mode)
		{
			engine.Log(null, "settings", $"How access is created changed: {old.Mode} → {r.Mode}. Access already issued keeps working as before; new access follows the new choice.", Me);
		}
		engine.Log(null, "settings", "Wi-Fi settings saved.", Me);
		await db.SaveChangesAsync(ct);
		engine.ForgetConfig();
		return await GetSettingsAsync(ct);
	}

	public async Task<WifiControllerStatus> TestControllerAsync(CancellationToken ct)
	{
		WifiControllerStatus status = await (await engine.ControllerAsync(ct)).TestAsync(ct);
		engine.Log(null, "settings", $"Controller test: {(status.Connected ? "connected" : "failed")} — {status.Message}", Me);
		await db.SaveChangesAsync(ct);
		return status;
	}

	// ---- simulated controller lab -------------------------------------------------------------------

	private async Task<SimulatedWifiController> SimAsync(CancellationToken ct)
	{
		WifiConfig cfg = await engine.ConfigAsync(ct);
		if (cfg.ControllerType != "simulated")
		{
			throw new BusinessRuleException("Simulating devices is only possible with the simulated controller.");
		}
		return (SimulatedWifiController)controllers.Get("simulated");
	}

	public async Task<WifiSimResultDto> SimJoinAsync(WifiSimJoinRequest r, CancellationToken ct)
	{
		SimulatedWifiController sim = await SimAsync(ct);
		WifiConfig cfg = await engine.ConfigAsync(ct);
		string mac = string.IsNullOrWhiteSpace(r.Mac) ? "02:" + string.Join(":", Enumerable.Range(0, 5).Select(_ => WifiSecrets.Random("0123456789ABCDEF", 2))) : WifiEngine.NormaliseMac(r.Mac);
		string name = WifiEngine.Clip(r.DeviceName, 120) ?? "Test device";
		string? ap = WifiEngine.Clip(r.AccessPoint, 120);
		string outcome;
		if (r.How == "shared")
		{
			string ssid = WifiEngine.Clip(r.Ssid, 64) ?? cfg.Networks.First().Ssid;
			outcome = await sim.JoinAsync(mac, name, ssid, ap, null, true, ct);
		}
		else
		{
			WifiAccess a = await db.Set<WifiAccess>().AsNoTracking().FirstOrDefaultAsync(x => x.AccessId == r.AccessId, ct) ?? throw new BusinessRuleException("Pick the access the device uses.");
			string? pw = engine.Password(a, cfg);
			if (r.How == "wrong")
			{
				pw = (pw ?? "") + "x";
			}
			if (r.How != "wrong" && a.ControllerState != WifiControllerState.Live && a.ControllerRef != null)
			{
				await sim.RecordExpiredAttemptAsync(mac, name, a.Ssid, ap, a.ControllerRef, ct);
				outcome = "refused-expired";
			}
			else
			{
				outcome = await sim.JoinAsync(mac, name, a.Ssid, ap, pw, cfg.Mode == "shared", ct);
			}
		}
		engine.Log(null, "simulated", $"Simulated device {name} ({mac}): {outcome}.", Me);
		await db.SaveChangesAsync(ct);
		await jobs.PollAsync(true, ct);
		return new WifiSimResultDto(mac, outcome);
	}

	public async Task<WifiSimResultDto> SimLeaveAsync(WifiSimLeaveRequest r, CancellationToken ct)
	{
		SimulatedWifiController sim = await SimAsync(ct);
		string mac = WifiEngine.NormaliseMac(r.Mac);
		bool left = await sim.LeaveAsync(mac, ct);
		await jobs.PollAsync(true, ct);
		return new WifiSimResultDto(mac, left ? "left" : "was not online");
	}
}
