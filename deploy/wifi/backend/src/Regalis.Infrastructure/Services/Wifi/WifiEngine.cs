using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Wifi;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Entities.Operations;
using Regalis.Domain.Entities.Wifi;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Reporting.Analytics;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services.Wifi;

/// <summary>Settings as read from MST_WifiSetting, with the spec's defaults (open items 2-6, 8).</summary>
public sealed class WifiConfig
{
	public string ControllerType { get; init; } = "simulated";
	public string? ControllerUrl { get; init; }
	public string? ControllerUser { get; init; }
	public string? ControllerKeyEnc { get; init; }
	public string? ControllerSite { get; init; }
	/// <summary>per_customer (recommended) / portal / shared (spec 1.1).</summary>
	public string Mode { get; init; } = "per_customer";
	public string? SharedPasswordEnc { get; init; }
	public List<WifiNetworkDto> Networks { get; init; } = WifiEngine.DefaultNetworks();
	public Dictionary<string, WifiProfileDto> Profiles { get; init; } = WifiEngine.DefaultProfiles().ToDictionary(p => p.Key);
	public bool AutoIssue { get; init; } = true;
	public DateTime IssueSinceUtc { get; init; } = DateTime.MaxValue;
	public bool PayLater { get; init; }
	public bool DeliverEmail { get; init; } = true;
	public bool DeliverWhatsApp { get; init; } = true;
	public bool DeliverQr { get; init; } = true;
	public bool Reminder { get; init; }
	/// <summary>closing (open item 3 default) / 24h (24 hours from first use, single-day passes).</summary>
	public string DayPassUntil { get; init; } = "closing";
	public int IdleMinutes { get; init; } = 30;
	public bool RuleCancel { get; init; } = true;
	/// <summary>0 = never pause (open item 4).</summary>
	public int OverdueDays { get; init; } = 7;
	public bool RuleUnknownDevice { get; init; } = true;
	public int RetentionMonths { get; init; } = 12;
	public Dictionary<string, string?> Raw { get; init; } = new();

	public WifiProfileDto Profile(string key) => Profiles.TryGetValue(key, out WifiProfileDto? p) ? p : WifiEngine.DefaultProfiles().First(x => x.Key == key);

	public int SpeedFor(string ssid) => Networks.FirstOrDefault(n => n.Ssid == ssid)?.SpeedMbps ?? 0;

	public string? Get(string key) => Raw.TryGetValue(key, out string? v) ? v : null;
}

/// <summary>
/// Everything the admin screens, the customer portal and the background job share: settings,
/// issuing and renewing, the controller calls with retries, delivery (email + app + WhatsApp link),
/// status changes and the access log.
/// </summary>
public class WifiEngine(RegalisDbContext db, IDateTimeProvider clock, INumberSequenceService numbers, IReportMailer mailer, WifiSecrets secrets,
	WifiControllerFactory controllers, ILogger<WifiEngine> logger)
{
	public RegalisDbContext Db => db;

	public WifiSecrets Secrets => secrets;

	public DateTime Now => clock.UtcNow;

	public static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

	public static List<WifiNetworkDto> DefaultNetworks() => new List<WifiNetworkDto>
	{
		new WifiNetworkDto("Regalis-Members", 50, "Monthly members and cabins · per-person password", false),
		new WifiNetworkDto("Regalis-Guest", 20, "Day passes, meeting rooms and visitors · one-day code", false),
		new WifiNetworkDto("Regalis-Staff", 0, "Admin and housekeeping devices only · not issued to customers", true)
	};

	public static List<WifiProfileDto> DefaultProfiles() => new List<WifiProfileDto>
	{
		new WifiProfileDto("member", "Members (monthly passes, cabins and desks booked for 7 days or more)", "Regalis-Members", 3),
		new WifiProfileDto("daypass", "Day passes", "Regalis-Guest", 2),
		new WifiProfileDto("short", "Desks and cabins booked by the hour or day", "Regalis-Guest", 2),
		new WifiProfileDto("meeting", "Meeting rooms", "Regalis-Guest", 2),
		new WifiProfileDto("virtual", "Virtual office (no Wi-Fi: they do not work here)", "", 0),
		new WifiProfileDto("guest", "Guests issued by hand", "Regalis-Guest", 2)
	};

	private static readonly JsonSerializerOptions Json = new JsonSerializerOptions(JsonSerializerDefaults.Web);

	// ---- settings ----------------------------------------------------------------------------

	private WifiConfig? _config;

	public async Task<WifiConfig> ConfigAsync(CancellationToken ct)
	{
		if (_config != null)
		{
			return _config;
		}
		Dictionary<string, string?> s = await db.Set<WifiSetting>().AsNoTracking().ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, ct);
		string? S(string k) => s.TryGetValue(k, out string? v) && !string.IsNullOrWhiteSpace(v) ? v : null;
		bool B(string k, bool d) => S(k) is string v ? v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase) : d;
		int I(string k, int d) => int.TryParse(S(k), out int n) && n >= 0 ? n : d;
		List<WifiNetworkDto> networks = DefaultNetworks();
		Dictionary<string, WifiProfileDto> profiles = DefaultProfiles().ToDictionary(p => p.Key);
		try
		{
			if (S("networks") is string nj)
			{
				networks = JsonSerializer.Deserialize<List<WifiNetworkDto>>(nj, Json) ?? networks;
			}
			if (S("profiles") is string pj)
			{
				foreach (WifiProfileDto p in JsonSerializer.Deserialize<List<WifiProfileDto>>(pj, Json) ?? new List<WifiProfileDto>())
				{
					if (profiles.ContainsKey(p.Key))
					{
						profiles[p.Key] = profiles[p.Key] with { Ssid = p.Ssid ?? "", DeviceLimit = p.DeviceLimit };
					}
				}
			}
		}
		catch (JsonException ex)
		{
			logger.LogWarning(ex, "Wi-Fi settings: networks/profiles JSON unreadable, using defaults");
		}
		_config = new WifiConfig
		{
			ControllerType = S("controller.type") ?? "simulated",
			ControllerUrl = S("controller.url"),
			ControllerUser = S("controller.user"),
			ControllerKeyEnc = S("controller.key"),
			ControllerSite = S("controller.site"),
			Mode = S("mode") ?? "per_customer",
			SharedPasswordEnc = S("shared.password"),
			Networks = networks,
			Profiles = profiles,
			AutoIssue = B("issue.auto", true),
			IssueSinceUtc = DateTime.TryParse(S("issue.sinceUtc"), Inv, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime since) ? since : DateTime.MaxValue,
			PayLater = B("issue.payLater", false),
			DeliverEmail = B("delivery.email", true),
			DeliverWhatsApp = B("delivery.whatsapp", true),
			DeliverQr = B("delivery.qr", true),
			Reminder = B("delivery.reminder", false),
			DayPassUntil = S("rules.dayPassUntil") ?? "closing",
			IdleMinutes = I("rules.idleMinutes", 30),
			RuleCancel = B("rules.cancel", true),
			OverdueDays = B("rules.overdue", true) ? I("rules.overdueDays", 7) : 0,
			RuleUnknownDevice = B("rules.unknownDevice", true),
			RetentionMonths = Math.Max(1, I("retention.months", 12)),
			Raw = s
		};
		return _config;
	}

	public void ForgetConfig() => _config = null;

	public async Task SetAsync(string key, string? value, long? userId, CancellationToken ct)
	{
		WifiSetting? row = await db.Set<WifiSetting>().FirstOrDefaultAsync(x => x.SettingKey == key, ct);
		if (row == null)
		{
			row = new WifiSetting { SettingKey = key };
			db.Set<WifiSetting>().Add(row);
		}
		row.SettingValue = value;
		row.UpdatedBy = userId;
		row.UpdatedUtc = Now;
		_config = null;
	}

	public async Task<IWifiController> ControllerAsync(CancellationToken ct) => controllers.Get((await ConfigAsync(ct)).ControllerType);

	public async Task<string> PortalUrlAsync(CancellationToken ct)
	{
		Dictionary<string, string> s = await db.AppSettings.AsNoTracking()
			.Where(a => a.SettingKey == "Notifications.CustomerEmailPortalUrl" || a.SettingKey == "Quotation.PortalBaseUrl")
			.ToDictionaryAsync(a => a.SettingKey, a => a.SettingValue ?? "", ct);
		string url = s.GetValueOrDefault("Notifications.CustomerEmailPortalUrl") is string a && a.Length > 0 ? a
			: s.GetValueOrDefault("Quotation.PortalBaseUrl") is string b && b.Length > 0 ? b : "https://myregalis.com";
		return url.TrimEnd('/') + "/wifi";
	}

	// ---- reading a row ---------------------------------------------------------------------------

	/// <summary>The password a person types: theirs, or the shared one in "shared" mode.</summary>
	public string? Password(WifiAccess a, WifiConfig cfg) =>
		cfg.Mode == "shared" ? secrets.Decrypt(cfg.SharedPasswordEnc) : secrets.Decrypt(a.PasswordEnc);

	public static (string Key, string Label) DisplayStatus(WifiAccess a, bool inUse, DateTime now) => a.Status switch
	{
		WifiAccessStatus.Revoked => ("revoked", "Revoked"),
		WifiAccessStatus.Expired => ("expired", "Expired"),
		WifiAccessStatus.Paused => ("paused", "Paused"),
		WifiAccessStatus.Issued when a.ValidFromUtc > now.AddHours(1) => ("issued", "Starts " + IstDate(a.ValidFromUtc)),
		WifiAccessStatus.Issued => ("issued", "Issued"),
		_ when inUse => ("inuse", "In use"),
		_ when a.ValidUntilUtc - now <= TimeSpan.FromHours(24) => ("expiring", "Expiring"),
		_ => ("active", "Active")
	};

	public static string IstDate(DateTime utc) => IstClock.ToIst(utc).ToString("d MMM yyyy", Inv);

	public static string IstDateTime(DateTime utc) => IstClock.ToIst(utc).ToString("d MMM yyyy, h:mm tt", Inv);

	public static string H(string? s) => WebUtility.HtmlEncode(s ?? string.Empty);

	public static string? Clip(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : (s.Trim().Length > max ? s.Trim().Substring(0, max) : s.Trim());

	public static string NormaliseMac(string? mac)
	{
		string hex = new string((mac ?? string.Empty).ToUpperInvariant().Where(Uri.IsHexDigit).ToArray());
		if (hex.Length != 12)
		{
			throw new Regalis.Application.Common.Exceptions.BusinessRuleException("A MAC address has 12 hex digits, for example A4:83:E7:1C:22:90.");
		}
		return string.Join(":", Enumerable.Range(0, 6).Select(i => hex.Substring(i * 2, 2)));
	}

	// ---- the access log ----------------------------------------------------------------------------

	public void Log(long? accessId, string ev, string detail, long? actor, object? meta = null)
	{
		db.Set<WifiEvent>().Add(new WifiEvent
		{
			AccessId = accessId,
			Event = ev,
			Detail = Clip(detail, 1000) ?? ev,
			ActorUserId = actor,
			MetaJson = meta == null ? null : JsonSerializer.Serialize(meta, Json),
			CreatedUtc = Now
		});
	}

	public void AdminAlert(string code, string title, string body, long? customerId, long? accessId, string? label, bool actionNeeded = true) =>
		AdminAlerts.Raise(db, new AdminEvent(AdminNotificationType.Support, code, title, body,
			actionNeeded ? AdminNotificationSeverity.ActionNeeded : AdminNotificationSeverity.Info, customerId, "WifiAccess", accessId, label, "Open Wi-Fi", "/admin/wifi"), Now);

	public void CustomerNotice(long customerId, long accessId, string title, string message, NotificationPriority priority = NotificationPriority.Normal)
	{
		db.Notifications.Add(new Notification
		{
			UserId = customerId,
			Audience = NotificationAudience.SingleUser,
			Title = Clip(title, 200) ?? title,
			Message = Clip(message, 1000) ?? message,
			Category = NotificationCategory.Booking,
			Priority = priority,
			ReferenceType = "WifiAccess",
			ReferenceId = accessId,
			ActionLabel = "Open My Wi-Fi",
			ActionRoute = "/wifi",
			SentUtc = Now
		});
	}

	// ---- creating ---------------------------------------------------------------------------------

	public async Task<string> UniqueUsernameAsync(string wanted, CancellationToken ct)
	{
		HashSet<string> pending = db.ChangeTracker.Entries<WifiAccess>().Select(e => e.Entity.Username).ToHashSet(StringComparer.OrdinalIgnoreCase);
		string candidate = wanted;
		for (int i = 2; i < 500; i++)
		{
			if (!pending.Contains(candidate) && !await db.Set<WifiAccess>().AnyAsync(a => a.Username == candidate, ct))
			{
				return candidate;
			}
			candidate = wanted + i.ToString(Inv);
		}
		return wanted + "-" + WifiSecrets.Code(4).ToLowerInvariant();
	}

	public static string CodeUsername(string profile) => profile switch
	{
		"meeting" => "mr-",
		"guest" => "guest-",
		_ => "day-"
	} + WifiSecrets.Code(6);

	/// <summary>A new access row (not yet in the controller: the job writes it there when it starts).</summary>
	public async Task<WifiAccess> NewAccessAsync(WifiAccessKind kind, string profile, long? customerId, string name, string? email, string? mobile, string? company,
		string ssid, DateTime fromUtc, DateTime untilUtc, int deviceLimit, long? issuedBy, CancellationToken ct)
	{
		string wanted = profile == "member" || kind == WifiAccessKind.TeamMember
			? WifiSecrets.UserBase(name, kind == WifiAccessKind.TeamMember ? company : null)
			: CodeUsername(profile);
		WifiAccess a = new WifiAccess
		{
			AccessNo = await numbers.NextAsync("WifiAccess", ct),
			Kind = kind,
			Profile = profile,
			CustomerId = customerId,
			HolderName = Clip(name, 150) ?? "Guest",
			HolderEmail = Clip(email, 256),
			HolderMobile = Clip(mobile, 20),
			Ssid = ssid,
			Username = await UniqueUsernameAsync(wanted, ct),
			PasswordEnc = secrets.Encrypt(WifiSecrets.NewPassword(name)),
			ValidFromUtc = fromUtc,
			ValidUntilUtc = untilUtc,
			DeviceLimit = Math.Max(1, deviceLimit),
			Status = WifiAccessStatus.Issued,
			ControllerState = WifiControllerState.Pending,
			IssuedBy = issuedBy,
			IssuedUtc = Now,
			CreatedUtc = Now,
			UpdatedUtc = Now
		};
		db.Set<WifiAccess>().Add(a);
		return a;
	}

	// ---- the controller ----------------------------------------------------------------------------

	/// <summary>Writes the access into the controller (or not, in shared mode). Three tries a minute apart, then the admin is told "access not created" (spec 7.2).</summary>
	public async Task<bool> PushAsync(WifiAccess a, CancellationToken ct)
	{
		WifiConfig cfg = await ConfigAsync(ct);
		if (cfg.Mode == "shared")
		{
			a.ControllerState = WifiControllerState.Live;
			a.ControllerRef = null;
			a.ControllerError = null;
			a.UpdatedUtc = Now;
			return true;
		}
		IWifiController ctrl = controllers.Get(cfg.ControllerType);
		string? password = secrets.Decrypt(a.PasswordEnc);
		try
		{
			if (password == null)
			{
				throw new InvalidOperationException("The stored password cannot be read (encryption key changed). Use Reset password.");
			}
			string reference = await ctrl.CreateAccessAsync(new WifiKeySpec(a.Ssid, a.Username, password, a.DeviceLimit, cfg.SpeedFor(a.Ssid), a.ValidUntilUtc), ct);
			a.ControllerRef = reference;
			a.ControllerState = WifiControllerState.Live;
			a.ControllerAttempts = 0;
			a.ControllerNextTryUtc = null;
			a.ControllerError = null;
			a.UpdatedUtc = Now;
			Log(a.AccessId, "controller", $"Written to the Wi-Fi controller ({ctrl.Type}), reference {reference}.", null);
			return true;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			a.ControllerAttempts++;
			a.ControllerError = Clip(ex.Message, 500);
			a.UpdatedUtc = Now;
			logger.LogWarning("Wi-Fi: controller write for {AccessNo} failed (try {Try}): {Error}", a.AccessNo, a.ControllerAttempts, ex.Message);
			if (a.ControllerAttempts >= 3)
			{
				a.ControllerState = WifiControllerState.Failed;
				a.ControllerNextTryUtc = null;
				Log(a.AccessId, "controller.failed", $"Access not created: the Wi-Fi controller could not be reached after 3 tries ({a.ControllerError}). The customer has NOT been told it is ready.", null);
				AdminAlert("wifi.not_created", "Wi-Fi access not created — " + a.HolderName,
					$"{a.AccessNo} ({a.Username}) could not be written to the Wi-Fi controller after 3 tries: {a.ControllerError} The customer has not been told it is ready. Fix the controller, then use Retry in Wi-Fi → Access issued.",
					a.CustomerId, a.AccessId, a.AccessNo);
			}
			else
			{
				a.ControllerState = WifiControllerState.Pending;
				a.ControllerNextTryUtc = Now.AddMinutes(a.ControllerAttempts);
			}
			return false;
		}
	}

	/// <summary>Takes the password out of the controller. Errors are logged and the admin told; the row still changes status.</summary>
	public async Task RemoveFromControllerAsync(WifiAccess a, string why, CancellationToken ct)
	{
		if (a.ControllerState != WifiControllerState.Live)
		{
			a.ControllerState = a.ControllerState == WifiControllerState.Failed ? WifiControllerState.Failed : WifiControllerState.Removed;
			a.ControllerNextTryUtc = null;
			return;
		}
		if (a.ControllerRef != null)
		{
			WifiConfig cfg = await ConfigAsync(ct);
			try
			{
				await controllers.Get(cfg.ControllerType).RevokeAccessAsync(a.ControllerRef, ct);
			}
			catch (Exception ex) when (ex is not OperationCanceledException)
			{
				Log(a.AccessId, "controller.failed", $"Could not take the password out of the controller ({why}): {ex.Message}", null);
				AdminAlert("wifi.revoke_failed", "Wi-Fi password still in the controller — " + a.HolderName,
					$"{a.AccessNo} ({a.Username}) should have stopped working ({why}) but the controller could not be reached: {ex.Message} Remove it by hand.", a.CustomerId, a.AccessId, a.AccessNo);
			}
		}
		a.ControllerState = WifiControllerState.Removed;
		a.UpdatedUtc = Now;
		DateTime now = Now;
		await db.Set<WifiSession>().Where(s => s.AccessId == a.AccessId && s.EndedUtc == null)
			.ExecuteUpdateAsync(u => u.SetProperty(s => s.EndedUtc, (DateTime?)now), ct);
	}

	// ---- delivery (spec 2, 5.5) -----------------------------------------------------------------------

	public async Task<string> TitleAsync(WifiAccess a, CancellationToken ct)
	{
		if (a.Kind == WifiAccessKind.Guest)
		{
			return "Guest access";
		}
		if (a.Kind == WifiAccessKind.TeamMember)
		{
			WifiAccess? parent = a.ParentAccessId == null ? null : await db.Set<WifiAccess>().AsNoTracking().FirstOrDefaultAsync(x => x.AccessId == a.ParentAccessId, ct);
			return parent == null ? "Team access" : await TitleAsync(parent, ct);
		}
		WifiAccessSource? src = await db.Set<WifiAccessSource>().AsNoTracking().Where(s => s.AccessId == a.AccessId && !s.IsCancelled).OrderByDescending(s => s.EndUtc).FirstOrDefaultAsync(ct);
		return src == null ? (a.Profile == "daypass" ? "Day pass" : "Your booking") : await SourceLabelAsync(src.SourceType, src.RefId, true, ct);
	}

	/// <summary>"Cabin C-001 · monthly membership", "Day pass · 12 Nov", "Meeting room · 4 hours".</summary>
	public async Task<string> SourceLabelAsync(byte type, long refId, bool withName, CancellationToken ct)
	{
		if (type == 2)
		{
			var p = await (from cp in db.CustomerPasses.AsNoTracking().IgnoreQueryFilters()
				join pp in db.PassPlans.AsNoTracking().IgnoreQueryFilters() on cp.PassPlanId equals pp.PassPlanId
				where cp.CustomerPassId == refId
				select new { cp.PassType, cp.ValidFrom, cp.ValidTo, pp.PlanName }).FirstOrDefaultAsync(ct);
			if (p == null)
			{
				return "Pass";
			}
			return p.PassType == PassType.DayPass
				? (p.ValidFrom == p.ValidTo ? $"Day pass · {p.ValidFrom.ToString("d MMM", Inv)}" : $"{p.PlanName} · day passes")
				: $"{p.PlanName} · membership";
		}
		var b = await (from bk in db.Bookings.AsNoTracking().IgnoreQueryFilters()
			join w in db.Workspaces.AsNoTracking().IgnoreQueryFilters() on bk.WorkspaceId equals w.WorkspaceId
			join t in db.WorkspaceTypes.AsNoTracking().IgnoreQueryFilters() on bk.WorkspaceTypeId equals t.WorkspaceTypeId
			where bk.BookingId == refId
			select new { bk.StartUtc, bk.EndUtc, bk.BillingMonths, w.WorkspaceName, t.TypeName, t.TypeCode }).FirstOrDefaultAsync(ct);
		if (b == null)
		{
			return "Booking";
		}
		string when;
		if (b.BillingMonths is byte months && months > 0)
		{
			when = months == 1 ? "monthly" : $"{months} months";
		}
		else
		{
			TimeSpan d = b.EndUtc - b.StartUtc;
			when = d.TotalHours < 20 ? $"{Math.Round(d.TotalHours, 1).ToString(Inv)} hours" : d.TotalDays < 1.5 ? IstClock.ToIst(b.StartUtc).ToString("d MMM", Inv) : $"{Math.Round(d.TotalDays)} days";
		}
		return (withName ? b.WorkspaceName : b.TypeName) + " · " + when;
	}

	public async Task<byte[]?> QrPngAsync(WifiAccess a, CancellationToken ct)
	{
		WifiConfig cfg = await ConfigAsync(ct);
		string? pw = Password(a, cfg);
		try
		{
			return WifiQr.Png(WifiSecrets.QrText(a.Ssid, pw, cfg.Mode == "portal"));
		}
		catch (Exception ex)
		{
			logger.LogWarning(ex, "Wi-Fi QR for {AccessNo} failed", a.AccessNo);
			return null;
		}
	}

	/// <summary>The email of spec 5.5. Returns null when it was accepted, else the error.</summary>
	public async Task<string?> SendEmailAsync(WifiAccess a, string reason, CancellationToken ct)
	{
		WifiConfig cfg = await ConfigAsync(ct);
		if (string.IsNullOrWhiteSpace(a.HolderEmail))
		{
			a.EmailStatus = "none";
			a.EmailError = "No email address.";
			return a.EmailError;
		}
		string? pw = Password(a, cfg);
		if (pw == null)
		{
			a.EmailStatus = "failed";
			a.EmailError = "The password cannot be read. Use Reset password.";
			return a.EmailError;
		}
		string title = await TitleAsync(a, ct);
		string first = a.HolderName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? a.HolderName;
		string link = await PortalUrlAsync(ct);
		string booking = "";
		if (a.BookingId != null || a.CustomerPassId != null)
		{
			WifiAccessSource? src = await db.Set<WifiAccessSource>().AsNoTracking().Where(s => s.AccessId == a.AccessId && !s.IsCancelled).OrderByDescending(s => s.EndUtc).FirstOrDefaultAsync(ct);
			booking = src == null ? "" : $"{src.RefNumber} · {title}";
		}
		string intro = reason switch
		{
			"reset" => "Your Wi-Fi password has been changed. The old one no longer works; here are your new details.",
			"renewed" => $"Your Wi-Fi for <b>{H(title)}</b> works again, with the same password. Here are your details.",
			"resend" => $"Here are your Wi-Fi details for <b>{H(title)}</b> again.",
			_ when a.Kind == WifiAccessKind.Guest => "Here is your Wi-Fi access for Regalis Workspace.",
			_ when a.Kind == WifiAccessKind.TeamMember => $"Your company has given you your own Wi-Fi access at Regalis Workspace ({H(title)}).",
			_ => $"Your payment for <b>{H(title)}</b> has been received, and your Wi-Fi is ready. Here are your details."
		};
		bool portal = cfg.Mode == "portal";
		string mono = "font-family:Consolas,'Courier New',monospace;font-weight:700";
		StringBuilder html = new StringBuilder();
		html.Append("<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:15px;line-height:1.55;color:#241F19;max-width:560px\">");
		html.Append("<p style=\"font-family:Georgia,serif;font-size:18px;font-weight:700;margin:0 0 16px\">REGALIS WORKSPACE</p>");
		html.Append($"<p>Hello {H(first)},</p><p>{intro}</p>");
		html.Append("<div style=\"background:#F6EFE2;border:1px solid #E6DAC2;border-radius:12px;padding:16px 20px;margin:16px 0\">");
		html.Append($"<div style=\"color:#6E6559;font-size:13px\">Network name</div><div style=\"{mono};font-size:20px;margin-bottom:10px\">{H(a.Ssid)}</div>");
		if (portal)
		{
			html.Append($"<div style=\"color:#6E6559;font-size:13px\">Username</div><div style=\"{mono};font-size:20px;margin-bottom:10px\">{H(a.Username)}</div>");
		}
		html.Append($"<div style=\"color:#6E6559;font-size:13px\">Password</div><div style=\"{mono};font-size:24px\">{H(pw)}</div></div>");
		html.Append("<table style=\"width:100%;border-collapse:collapse;font-size:14px\">");
		void Row(string k, string v) => html.Append($"<tr><td style=\"padding:8px 0;border-bottom:1px solid #E6DAC2;color:#6E6559;width:40%\">{H(k)}</td><td style=\"padding:8px 0;border-bottom:1px solid #E6DAC2\">{H(v)}</td></tr>");
		if (booking.Length > 0)
		{
			Row("Booking", booking);
		}
		Row("Valid from", IstDateTime(a.ValidFromUtc));
		Row("Valid until", IstDateTime(a.ValidUntilUtc));
		Row("Devices allowed", a.DeviceLimit.ToString(Inv));
		html.Append("</table>");
		if (portal)
		{
			html.Append("<p>Join the network, and sign in on the Wi-Fi login page that opens with the username and password above.</p>");
		}
		if (cfg.DeliverQr)
		{
			html.Append("<p style=\"margin-top:18px\"><b>Or scan to join</b><br><span style=\"color:#6E6559\">The QR code is attached (wifi-qr.png). Point your phone camera at it and it joins the network without typing anything.</span></p>");
		}
		if (a.CustomerId != null && a.Kind != WifiAccessKind.Guest && a.Kind != WifiAccessKind.TeamMember)
		{
			html.Append($"<p><a href=\"{H(link)}\" style=\"display:inline-block;background:#CC9A3D;color:#241F19;font-weight:700;padding:10px 18px;border-radius:10px;text-decoration:none\">Open My Wi-Fi in the app</a></p>");
		}
		html.Append($"<p style=\"color:#6E6559;font-size:13px;border-top:1px solid #E6DAC2;padding-top:12px\">This password is for you alone, on up to {a.DeviceLimit} device{(a.DeviceLimit == 1 ? "" : "s")}. It stops working on {H(IstDateTime(a.ValidUntilUtc))}" +
			(a.Kind == WifiAccessKind.Guest ? "." : " and starts again when you renew.") +
			" Your connections (device, time, data used) are recorded against your access; the websites you visit are not. Trouble connecting? Reply to this email or ask at the front desk.</p></div>");
		List<(string, string, byte[])> files = new List<(string, string, byte[])>();
		if (cfg.DeliverQr && await QrPngAsync(a, ct) is byte[] png)
		{
			files.Add(("wifi-qr.png", "image/png", png));
		}
		string subject = $"Your Wi-Fi details for {title} — Regalis Workspace";
		string? error;
		try
		{
			error = await mailer.SendAsync(new[] { a.HolderEmail! }, subject, html.ToString(), files, ct);
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			error = ex.Message;
		}
		a.EmailStatus = error == null ? "sent" : "failed";
		a.EmailError = Clip(error, 500);
		if (error == null)
		{
			a.EmailSentUtc = Now;
		}
		Log(a.AccessId, error == null ? "delivered" : "delivery.failed", error == null ? $"Email sent to {a.HolderEmail} ({reason})." : $"Email to {a.HolderEmail} failed: {error}", null);
		return error;
	}

	/// <summary>Email (when switched on) and the in-app notice; Issued becomes Active once either reached the person.</summary>
	public async Task DeliverAsync(WifiAccess a, string reason, CancellationToken ct)
	{
		WifiConfig cfg = await ConfigAsync(ct);
		bool reached = false;
		if (a.CustomerId is long cid && a.Kind is WifiAccessKind.Booking or WifiAccessKind.Pass)
		{
			string title = await TitleAsync(a, ct);
			CustomerNotice(cid, a.AccessId, reason == "reset" ? "Your Wi-Fi password was changed" : "Your Wi-Fi is ready",
				$"{title}: network {a.Ssid}, valid until {IstDateTime(a.ValidUntilUtc)} on up to {a.DeviceLimit} devices. Open My Wi-Fi for the password and QR code.");
			Log(a.AccessId, "delivered", "Shown in the customer app and portal under My Wi-Fi.", null);
			reached = true;
		}
		if (cfg.DeliverEmail || a.Kind is WifiAccessKind.Guest or WifiAccessKind.TeamMember)
		{
			reached |= await SendEmailAsync(a, reason, ct) == null;
		}
		if (reached)
		{
			a.DeliveredUtc ??= Now;
			if (a.Status == WifiAccessStatus.Issued)
			{
				a.Status = WifiAccessStatus.Active;
			}
		}
		a.UpdatedUtc = Now;
	}

	/// <summary>Click-to-chat link (open item 7): the message is typed out in the sender's WhatsApp.</summary>
	public async Task<string?> WhatsAppUrlAsync(WifiAccess a, CancellationToken ct)
	{
		WifiConfig cfg = await ConfigAsync(ct);
		string digits = new string((a.HolderMobile ?? string.Empty).Where(char.IsDigit).ToArray());
		if (digits.Length < 10)
		{
			return null;
		}
		if (digits.Length == 10)
		{
			digits = "91" + digits;
		}
		string title = await TitleAsync(a, ct);
		string text = $"Regalis Wi-Fi for {title}: network {a.Ssid}, password {Password(a, cfg)}" + (cfg.Mode == "portal" ? $", username {a.Username}" : "") +
			$"\nValid until {IstDateTime(a.ValidUntilUtc)}, up to {a.DeviceLimit} devices. QR code and devices: {await PortalUrlAsync(ct)}";
		return $"https://wa.me/{digits}?text={Uri.EscapeDataString(text)}";
	}

	// ---- status changes (spec 6.3) ----------------------------------------------------------------

	private async Task<List<WifiAccess>> ChildrenAsync(WifiAccess a, CancellationToken ct) =>
		a.Kind == WifiAccessKind.TeamMember ? new List<WifiAccess>() : await db.Set<WifiAccess>().Where(x => x.ParentAccessId == a.AccessId).ToListAsync(ct);

	public async Task RevokeAsync(WifiAccess a, string reason, long? actor, CancellationToken ct, bool tellCustomer = true)
	{
		if (a.Status == WifiAccessStatus.Revoked)
		{
			return;
		}
		await RemoveFromControllerAsync(a, "revoked", ct);
		a.Status = WifiAccessStatus.Revoked;
		a.RevokedReason = Clip(reason, 500);
		a.RevokedBy = actor;
		a.RevokedUtc = Now;
		a.UpdatedUtc = Now;
		Log(a.AccessId, "revoked", "Revoked: " + reason, actor);
		if (tellCustomer && a.CustomerId is long cid && a.Kind is WifiAccessKind.Booking or WifiAccessKind.Pass)
		{
			CustomerNotice(cid, a.AccessId, "Your Wi-Fi access has ended", $"{a.Ssid} ({a.AccessNo}) no longer works. Ask at the front desk if this is unexpected.", NotificationPriority.High);
		}
		foreach (WifiAccess c in await ChildrenAsync(a, ct))
		{
			await RevokeAsync(c, "Company access revoked: " + reason, actor, ct, false);
		}
	}

	public async Task ExpireAsync(WifiAccess a, CancellationToken ct)
	{
		if (a.Status is WifiAccessStatus.Expired or WifiAccessStatus.Revoked)
		{
			return;
		}
		await RemoveFromControllerAsync(a, "expired", ct);
		a.Status = WifiAccessStatus.Expired;
		a.ExpiredUtc = Now;
		a.UpdatedUtc = Now;
		Log(a.AccessId, "expired", $"Expired: the end date {IstDateTime(a.ValidUntilUtc)} passed.", null);
		foreach (WifiAccess c in await ChildrenAsync(a, ct))
		{
			if (c.ValidUntilUtc <= Now)
			{
				await ExpireAsync(c, ct);
			}
		}
	}

	public async Task PauseAsync(WifiAccess a, string reason, CancellationToken ct)
	{
		if (a.Status is not (WifiAccessStatus.Active or WifiAccessStatus.Issued))
		{
			return;
		}
		await RemoveFromControllerAsync(a, "paused", ct);
		a.Status = WifiAccessStatus.Paused;
		a.PausedUtc = Now;
		a.PauseReason = Clip(reason, 300);
		a.UpdatedUtc = Now;
		Log(a.AccessId, "paused", "Paused: " + reason, null);
		if (a.CustomerId is long cid && a.Kind is WifiAccessKind.Booking or WifiAccessKind.Pass)
		{
			CustomerNotice(cid, a.AccessId, "Your Wi-Fi is paused", reason + " Pay it and your Wi-Fi comes back with the same password.", NotificationPriority.High);
		}
		foreach (WifiAccess c in await ChildrenAsync(a, ct))
		{
			await PauseAsync(c, "Company access paused: " + reason, ct);
		}
	}

	/// <summary>Back to the controller with the same password (invoice paid, or validity extended after expiry).</summary>
	public async Task ResumeAsync(WifiAccess a, string why, long? actor, CancellationToken ct)
	{
		if (a.Status is not (WifiAccessStatus.Paused or WifiAccessStatus.Expired))
		{
			return;
		}
		bool wasDelivered = a.DeliveredUtc != null;
		a.Status = wasDelivered ? WifiAccessStatus.Active : WifiAccessStatus.Issued;
		a.PausedUtc = null;
		a.PauseReason = null;
		a.ExpiredUtc = null;
		a.ReminderSentUtc = null;
		a.ControllerState = WifiControllerState.Pending;
		a.ControllerAttempts = 0;
		a.ControllerNextTryUtc = null;
		a.UpdatedUtc = Now;
		Log(a.AccessId, "restored", why + " Same password.", actor);
		if (a.ValidFromUtc <= Now.AddHours(1))
		{
			await PushAsync(a, ct);
		}
		foreach (WifiAccess c in await ChildrenAsync(a, ct))
		{
			if (c.Status is WifiAccessStatus.Paused or WifiAccessStatus.Expired)
			{
				c.ValidUntilUtc = a.ValidUntilUtc;
				await ResumeAsync(c, "Company access restored.", actor, ct);
			}
		}
	}

	/// <summary>
	/// Validity = the union of the bookings and passes still standing. A cancellation that leaves nothing
	/// current or still to come revokes at once (spec 4.4, cancel rule); otherwise the dates follow.
	/// </summary>
	public async Task RecomputeAsync(WifiAccess a, string why, CancellationToken ct, bool cancellation = false)
	{
		WifiConfig cfg = await ConfigAsync(ct);
		List<WifiAccessSource> sources = await db.Set<WifiAccessSource>().Where(s => s.AccessId == a.AccessId).ToListAsync(ct);
		List<WifiAccessSource> live = sources.Where(s => !s.IsCancelled).ToList();
		if (live.Count == 0 || (cancellation && live.All(s => s.EndUtc <= Now)))
		{
			if (sources.Count > 0 && cfg.RuleCancel && a.Status != WifiAccessStatus.Revoked)
			{
				await RevokeAsync(a, why, null, ct);
				return;
			}
			if (live.Count == 0)
			{
				return;
			}
		}
		DateTime from = live.Min(s => s.StartUtc);
		DateTime until = live.Max(s => s.EndUtc);
		if (!cfg.RuleCancel && until < a.ValidUntilUtc)
		{
			until = a.ValidUntilUtc;
		}
		if (from == a.ValidFromUtc && until == a.ValidUntilUtc)
		{
			return;
		}
		Log(a.AccessId, "extended", $"{why} Valid {IstDateTime(from)} – {IstDateTime(until)} (was until {IstDateTime(a.ValidUntilUtc)}).", null);
		if (until > a.ValidUntilUtc)
		{
			a.ReminderSentUtc = null;
		}
		a.ValidFromUtc = from;
		a.ValidUntilUtc = until;
		a.UpdatedUtc = Now;
		foreach (WifiAccess c in await ChildrenAsync(a, ct))
		{
			if (c.Status != WifiAccessStatus.Revoked)
			{
				c.ValidFromUtc = from;
				c.ValidUntilUtc = until;
				c.UpdatedUtc = Now;
			}
		}
		if (until <= Now)
		{
			await ExpireAsync(a, ct);
		}
		else if (a.Status == WifiAccessStatus.Expired)
		{
			await ResumeAsync(a, "Booking renewed.", null, ct);
			await DeliverAsync(a, "renewed", ct);
		}
	}

	public async Task<List<(long UserId, string Email)>> WifiAdminsAsync(CancellationToken ct)
	{
		List<int> roleIds = await (from rp in db.RolePermissions.AsNoTracking()
			join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.PermissionId
			where p.PermissionCode == "WIFI.MANAGE"
			select rp.RoleId).Distinct().ToListAsync(ct);
		return (await (from ur in db.UserRoles.AsNoTracking()
			join u in db.Users.AsNoTracking() on ur.UserId equals u.UserId
			where roleIds.Contains(ur.RoleId) && u.UserType != UserType.Customer && u.Status == UserStatus.Active && u.IsActive
			select new { u.UserId, u.Email }).Distinct().ToListAsync(ct)).Select(x => (x.UserId, x.Email)).ToList();
	}
}
