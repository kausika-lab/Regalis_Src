using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Crm;
using Regalis.Domain.Entities.Crm;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Reporting.Analytics;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services.Crm;

/// <summary>Mobile and email normalising (document section 13.3).</summary>
public static class CrmText
{
	/// <summary>The last 10 digits, stripped of +91, spaces, dashes and brackets; "" when fewer than 10 digits.</summary>
	public static string NormaliseMobile(string? mobile)
	{
		if (string.IsNullOrWhiteSpace(mobile))
		{
			return string.Empty;
		}
		string digits = new string(mobile.Where(char.IsAsciiDigit).ToArray());
		return digits.Length < 10 ? string.Empty : digits.Substring(digits.Length - 10);
	}

	/// <summary>True for exactly 10 digits after dropping a leading +91 / 91 / 0 and the separators.</summary>
	public static bool IsValidMobile(string? mobile)
	{
		if (string.IsNullOrWhiteSpace(mobile))
		{
			return false;
		}
		string trimmed = mobile.Trim();
		if (trimmed.Any(c => !(char.IsAsciiDigit(c) || c == '+' || c == ' ' || c == '-' || c == '(' || c == ')')))
		{
			return false;
		}
		string digits = new string(trimmed.Where(char.IsAsciiDigit).ToArray());
		if (digits.Length == 12 && digits.StartsWith("91", StringComparison.Ordinal))
		{
			digits = digits.Substring(2);
		}
		else if (digits.Length == 11 && digits.StartsWith("0", StringComparison.Ordinal))
		{
			digits = digits.Substring(1);
		}
		return digits.Length == 10;
	}

	public static string? NormaliseEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

	public static bool IsValidEmail(string email)
	{
		string e = email.Trim();
		int at = e.IndexOf('@');
		return e.Length <= 256 && at > 0 && at == e.LastIndexOf('@') && e.IndexOf('.', at) > at + 1 && !e.EndsWith('.') && !e.Contains(' ');
	}

	public static string? Clip(string? value, int max)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return null;
		}
		string v = value.Trim();
		return v.Length <= max ? v : v.Substring(0, max);
	}

	public static string Html(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

	public static string HashIp(string? ip)
	{
		if (string.IsNullOrWhiteSpace(ip))
		{
			return null!;
		}
		byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes("regalis-crm|" + ip));
		return Convert.ToHexString(bytes);
	}

	public static string Ist(DateTime utc) => IstClock.ToIst(utc).ToString("d MMM, hh:mm tt", CultureInfo.InvariantCulture);

	public static string Minutes(int minutes)
	{
		if (minutes < 60)
		{
			return minutes + (minutes == 1 ? " minute" : " minutes");
		}
		if (minutes < 60 * 24)
		{
			int h = minutes / 60;
			return h + (h == 1 ? " hour" : " hours");
		}
		int d = minutes / (60 * 24);
		return d + (d == 1 ? " day" : " days");
	}
}

/// <summary>The CRM settings, read from MST_CrmSetting with the document's defaults.</summary>
public sealed class CrmSettingsSnapshot
{
	public int TargetWeb { get; init; } = 30;
	public int TargetWhatsApp { get; init; } = 30;
	public int TargetCallback { get; init; } = 15;
	public int TargetEmail { get; init; } = 240;
	public int Escalate { get; init; } = 60;
	public TimeSpan WorkStart { get; init; } = new TimeSpan(9, 0, 0);
	public TimeSpan WorkEnd { get; init; } = new TimeSpan(19, 0, 0);
	/// <summary>1 = Monday ... 7 = Sunday.</summary>
	public HashSet<int> WorkDays { get; init; } = new HashSet<int> { 1, 2, 3, 4, 5, 6 };
	public string ConsentText { get; init; } = CrmEngine.DefaultConsent;
	public string MarketingText { get; init; } = CrmEngine.DefaultMarketing;
	public int VisitRetentionDays { get; init; } = 90;
	public int LostRetentionMonths { get; init; } = 24;
	public bool EmailEnabled { get; init; }
	public bool AckEmailEnabled { get; init; }
	public bool DigestEnabled { get; init; }
	public string? WhatsAppNumber { get; init; }
	public string? PublicPhone { get; init; }
	public string? PrivacyUrl { get; init; }
	public DateTime? EmailTestedUtc { get; init; }
	public string? EmailTestedTo { get; init; }
	public string? DigestLastDate { get; init; }

	public CrmSettingsDto ToDto() => new CrmSettingsDto(TargetWeb, TargetWhatsApp, TargetCallback, TargetEmail, Escalate,
		WorkStart.ToString(@"hh\:mm"), WorkEnd.ToString(@"hh\:mm"), WorkDays.OrderBy(d => d).ToList(), ConsentText, MarketingText,
		VisitRetentionDays, LostRetentionMonths, EmailEnabled, AckEmailEnabled, DigestEnabled, WhatsAppNumber, PublicPhone, PrivacyUrl,
		EmailTestedUtc, EmailTestedTo);

	/// <summary>True when the IST moment falls inside working hours.</summary>
	public bool IsWorking(DateTime ist) => WorkDays.Contains(DayNo(ist.DayOfWeek)) && ist.TimeOfDay >= WorkStart && ist.TimeOfDay < WorkEnd;

	public static int DayNo(DayOfWeek d) => d == DayOfWeek.Sunday ? 7 : (int)d;

	/// <summary>The clock only runs during working hours: an enquiry at 11 PM is not late at 11:30 PM.</summary>
	public DateTime AddWorkingMinutes(DateTime startUtc, int minutes)
	{
		if (WorkDays.Count == 0 || WorkEnd <= WorkStart)
		{
			return startUtc.AddMinutes(minutes);
		}
		DateTime ist = IstClock.ToIst(startUtc);
		double left = minutes;
		for (int guard = 0; guard < 400; guard++)
		{
			DateTime dayStart = ist.Date + WorkStart;
			DateTime dayEnd = ist.Date + WorkEnd;
			if (!WorkDays.Contains(DayNo(ist.DayOfWeek)) || ist >= dayEnd)
			{
				ist = ist.Date.AddDays(1) + WorkStart;
				continue;
			}
			if (ist < dayStart)
			{
				ist = dayStart;
			}
			double available = (dayEnd - ist).TotalMinutes;
			if (left <= available)
			{
				return IstClock.ToUtc(ist.AddMinutes(left));
			}
			left -= available;
			ist = ist.Date.AddDays(1) + WorkStart;
		}
		return startUtc.AddMinutes(minutes);
	}

	/// <summary>Working minutes between two moments (0 when b is before a).</summary>
	public int WorkingMinutesBetween(DateTime aUtc, DateTime bUtc)
	{
		if (bUtc <= aUtc)
		{
			return 0;
		}
		if (WorkDays.Count == 0 || WorkEnd <= WorkStart)
		{
			return (int)(bUtc - aUtc).TotalMinutes;
		}
		DateTime a = IstClock.ToIst(aUtc);
		DateTime b = IstClock.ToIst(bUtc);
		double total = 0;
		for (DateTime day = a.Date; day <= b.Date; day = day.AddDays(1))
		{
			if (!WorkDays.Contains(DayNo(day.DayOfWeek)))
			{
				continue;
			}
			DateTime s = day + WorkStart;
			DateTime e = day + WorkEnd;
			DateTime from = a > s ? a : s;
			DateTime to = b < e ? b : e;
			if (to > from)
			{
				total += (to - from).TotalMinutes;
			}
			if ((b.Date - a.Date).TotalDays > 400)
			{
				break;
			}
		}
		return (int)Math.Floor(total);
	}
}

/// <summary>
/// Shared CRM machinery: settings, lookups, the response clock, the assignment rules,
/// notifications and the one place a lead is written (both doors produce the same record).
/// </summary>
public class CrmEngine(RegalisDbContext db, IDateTimeProvider clock, INumberSequenceService numbers, IReportMailer mailer, ILogger<CrmEngine> logger)
{
	public const string DefaultConsent = "I agree Regalis WorkSpace may contact me about workspace availability. We will not pass your details to anyone else.";

	public const string DefaultMarketing = "Also send me occasional news and offers from Regalis WorkSpace. (Optional)";

	public RegalisDbContext Db => db;

	public DateTime Now => clock.UtcNow;

	// ---- settings --------------------------------------------------------------------------

	private CrmSettingsSnapshot? _settings;

	public async Task<CrmSettingsSnapshot> SettingsAsync(CancellationToken ct)
	{
		if (_settings != null)
		{
			return _settings;
		}
		Dictionary<string, string?> s = await db.Set<CrmSetting>().AsNoTracking().ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue, ct);
		int I(string k, int d) => s.TryGetValue(k, out var v) && int.TryParse(v, out int n) && n >= 0 ? n : d;
		bool B(string k) => s.TryGetValue(k, out var v) && (v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
		string? S(string k) => s.TryGetValue(k, out var v) && !string.IsNullOrWhiteSpace(v) ? v : null;
		TimeSpan T(string k, TimeSpan d) => s.TryGetValue(k, out var v) && TimeSpan.TryParseExact(v, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan t) ? t : d;
		HashSet<int> days = new HashSet<int>();
		foreach (string part in (S("hours.days") ?? "1,2,3,4,5,6").Split(',', StringSplitOptions.RemoveEmptyEntries))
		{
			if (int.TryParse(part.Trim(), out int dn) && dn >= 1 && dn <= 7)
			{
				days.Add(dn);
			}
		}
		_settings = new CrmSettingsSnapshot
		{
			TargetWeb = I("target.web", 30),
			TargetWhatsApp = I("target.whatsapp", 30),
			TargetCallback = I("target.callback", 15),
			TargetEmail = I("target.email", 240),
			Escalate = I("target.escalate", 60),
			WorkStart = T("hours.start", new TimeSpan(9, 0, 0)),
			WorkEnd = T("hours.end", new TimeSpan(19, 0, 0)),
			WorkDays = days,
			ConsentText = S("consent.text") ?? DefaultConsent,
			MarketingText = S("consent.marketing") ?? DefaultMarketing,
			VisitRetentionDays = I("retention.visitDays", 90),
			LostRetentionMonths = I("retention.lostMonths", 24),
			EmailEnabled = B("email.enabled"),
			AckEmailEnabled = B("email.ack"),
			DigestEnabled = B("email.digest"),
			WhatsAppNumber = S("public.whatsapp"),
			PublicPhone = S("public.phone"),
			PrivacyUrl = S("public.privacyUrl"),
			EmailTestedUtc = DateTime.TryParse(S("email.testedUtc"), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime tested) ? tested : null,
			EmailTestedTo = S("email.testedTo"),
			DigestLastDate = S("digest.lastDate")
		};
		return _settings;
	}

	public void ForgetSettings() => _settings = null;

	public async Task SetSettingAsync(string key, string? value, long? userId, CancellationToken ct)
	{
		CrmSetting? row = await db.Set<CrmSetting>().FirstOrDefaultAsync(x => x.SettingKey == key, ct);
		if (row == null)
		{
			row = new CrmSetting { SettingKey = key };
			db.Set<CrmSetting>().Add(row);
		}
		row.SettingValue = value;
		row.UpdatedBy = userId;
		row.UpdatedUtc = Now;
		_settings = null;
	}

	/// <summary>Minutes allowed for the first reply, by how the lead arrived; null = no clock (walk-in, phone call).</summary>
	public static int? TargetFor(CrmChannel channel, string sourceCode, CrmSettingsSnapshot s) => channel switch
	{
		CrmChannel.WebForm or CrmChannel.QuoteRequest => s.TargetWeb,
		CrmChannel.Callback => s.TargetCallback,
		CrmChannel.WhatsApp => s.TargetWhatsApp,
		_ => sourceCode switch
		{
			"EMAIL" => s.TargetEmail,
			"WHATSAPP" => s.TargetWhatsApp,
			_ => null
		}
	};

	// ---- lookups --------------------------------------------------------------------------

	public Task<CrmStatus> StatusByCodeAsync(string code, CancellationToken ct) =>
		db.Set<CrmStatus>().AsNoTracking().FirstAsync(s => s.Code == code, ct);

	public async Task<int> SourceIdByCodeAsync(string code, CancellationToken ct) =>
		await db.Set<CrmSource>().AsNoTracking().Where(s => s.Code == code).Select(s => s.SourceId).FirstOrDefaultAsync(ct);

	/// <summary>Staff who hold CRM.VIEW (active, not customers).</summary>
	public async Task<List<(long UserId, string Name, string Email, string Roles)>> CrmStaffAsync(CancellationToken ct)
	{
		List<int> roleIds = await (from rp in db.RolePermissions.AsNoTracking()
			join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.PermissionId
			where p.PermissionCode == "CRM.VIEW"
			select rp.RoleId).Distinct().ToListAsync(ct);
		var rows = await (from ur in db.UserRoles.AsNoTracking()
			join u in db.Users.AsNoTracking() on ur.UserId equals u.UserId
			join r in db.Roles.AsNoTracking() on ur.RoleId equals r.RoleId
			where roleIds.Contains(ur.RoleId) && u.UserType != UserType.Customer && u.Status == UserStatus.Active && u.IsActive
			select new { u.UserId, u.FullName, u.Email, r.RoleName }).ToListAsync(ct);
		return rows.GroupBy(r => r.UserId)
			.Select(g => (g.Key, g.First().FullName, g.First().Email, string.Join(", ", g.Select(x => x.RoleName).Distinct())))
			.OrderBy(x => x.FullName).ToList();
	}

	/// <summary>On duty = ticked in CRM Settings, or checked in today through Attendance.</summary>
	public async Task<HashSet<long>> OnDutyAsync(CancellationToken ct)
	{
		HashSet<long> set = (await db.Set<CrmTeamMember>().AsNoTracking().Where(m => m.IsActive && m.IsOnDuty).Select(m => m.UserId).ToListAsync(ct)).ToHashSet();
		DateOnly today = IstClock.IstDateOf(Now);
		List<long> team = await db.Set<CrmTeamMember>().AsNoTracking().Where(m => m.IsActive).Select(m => m.UserId).ToListAsync(ct);
		if (team.Count > 0)
		{
			foreach (long id in await db.Attendances.AsNoTracking().Where(a => team.Contains(a.UserId) && a.AttendanceDate == today && a.CheckOutUtc == null).Select(a => a.UserId).Distinct().ToListAsync(ct))
			{
				set.Add(id);
			}
		}
		return set;
	}

	/// <summary>Managers: team members ticked as manager; otherwise staff holding CRM.LEAD.VIEWALL.</summary>
	public async Task<List<long>> ManagersAsync(CancellationToken ct)
	{
		List<long> ticked = await db.Set<CrmTeamMember>().AsNoTracking().Where(m => m.IsActive && m.IsManager).Select(m => m.UserId).ToListAsync(ct);
		if (ticked.Count > 0)
		{
			return ticked;
		}
		List<int> roleIds = await (from rp in db.RolePermissions.AsNoTracking()
			join p in db.Permissions.AsNoTracking() on rp.PermissionId equals p.PermissionId
			where p.PermissionCode == "CRM.LEAD.VIEWALL"
			select rp.RoleId).Distinct().ToListAsync(ct);
		return await (from ur in db.UserRoles.AsNoTracking()
			join u in db.Users.AsNoTracking() on ur.UserId equals u.UserId
			where roleIds.Contains(ur.RoleId) && u.UserType != UserType.Customer && u.Status == UserStatus.Active && u.IsActive
			select u.UserId).Distinct().ToListAsync(ct);
	}

	public async Task<string> UserNameAsync(long? userId, CancellationToken ct)
	{
		if (!userId.HasValue)
		{
			return "System";
		}
		return await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => u.UserId == userId).Select(u => u.FullName).FirstOrDefaultAsync(ct) ?? "Unknown";
	}

	/// <summary>Customers (user type 1) whose mobile ends with the same 10 digits, or whose email matches.</summary>
	public async Task<List<(User User, string MatchedOn)>> MatchingCustomersAsync(string mobileNorm, string? emailNorm, CancellationToken ct)
	{
		List<(User, string)> found = new List<(User, string)>();
		if (mobileNorm.Length == 10)
		{
			List<User> byMobile = await db.Users.AsNoTracking().Where(u => u.UserType == UserType.Customer && u.MobileNumber.EndsWith(mobileNorm)).Take(5).ToListAsync(ct);
			found.AddRange(byMobile.Where(u => CrmText.NormaliseMobile(u.MobileNumber) == mobileNorm).Select(u => (u, "mobile")));
		}
		if (emailNorm != null)
		{
			List<User> byEmail = await db.Users.AsNoTracking().Where(u => u.UserType == UserType.Customer && u.Email == emailNorm).Take(5).ToListAsync(ct);
			found.AddRange(byEmail.Where(u => found.All(f => f.Item1.UserId != u.UserId)).Select(u => (u, "email")));
		}
		return found;
	}

	// ---- assignment -----------------------------------------------------------------------

	/// <summary>Document section 8.4: rules in order, the first that matches wins. Returns the owner (null = unassigned) and the rule label.</summary>
	public async Task<(long? OwnerId, string Rule)> ApplyRulesAsync(CrmLead lead, CancellationToken ct)
	{
		List<CrmAssignmentRule> rules = await db.Set<CrmAssignmentRule>().AsNoTracking().Where(r => r.IsActive).OrderBy(r => r.SortOrder).ThenBy(r => r.RuleId).ToListAsync(ct);
		HashSet<long> onDuty = await OnDutyAsync(ct);
		List<CrmTeamMember> team = await db.Set<CrmTeamMember>().AsNoTracking().Where(m => m.IsActive).ToListAsync(ct);
		HashSet<long> staff = (await CrmStaffAsync(ct)).Select(s => s.UserId).ToHashSet();
		bool isLarge = lead.LookingForId.HasValue && await db.Set<CrmLookingFor>().AsNoTracking().AnyAsync(l => l.LookingForId == lead.LookingForId && l.IsLarge, ct);
		foreach (CrmAssignmentRule rule in rules)
		{
			switch (rule.ConditionType)
			{
			case CrmRuleCondition.ExistingCustomer:
			{
				List<(User User, string MatchedOn)> customers = await MatchingCustomersAsync(lead.MobileNormalised, lead.EmailNormalised, ct);
				if (customers.Count == 0)
				{
					break;
				}
				long customerId = customers[0].User.UserId;
				long? keeper = await db.Set<CrmLead>().AsNoTracking().Where(l => l.CustomerId == customerId && l.OwnerUserId != null)
					.OrderByDescending(l => l.ConvertedUtc).Select(l => l.OwnerUserId).FirstOrDefaultAsync(ct);
				if (keeper.HasValue && staff.Contains(keeper.Value))
				{
					return (keeper, rule.Label);
				}
				break;
			}
			case CrmRuleCondition.Location:
			{
				if (!lead.LocationId.HasValue || (rule.LocationId.HasValue && rule.LocationId != lead.LocationId))
				{
					break;
				}
				List<CrmTeamMember> here = team.Where(m => m.LocationId == lead.LocationId && onDuty.Contains(m.UserId) && staff.Contains(m.UserId)).ToList();
				if (here.Count > 0)
				{
					return (await NextRoundRobinAsync(here.Select(m => m.UserId).ToList(), ct), rule.Label);
				}
				break;
			}
			case CrmRuleCondition.Size:
			{
				int min = rule.MinPeople ?? 5;
				bool matches = isLarge || (lead.PeopleCount.HasValue && lead.PeopleCount > min) || (rule.LookingForId.HasValue && rule.LookingForId == lead.LookingForId);
				if (!matches)
				{
					break;
				}
				long? target = await TargetAsync(rule, onDuty, staff, ct);
				if (target.HasValue)
				{
					return (target, rule.Label);
				}
				break;
			}
			case CrmRuleCondition.RoundRobin:
			{
				List<long> pool = team.Where(m => onDuty.Contains(m.UserId) && staff.Contains(m.UserId)).Select(m => m.UserId).ToList();
				if (pool.Count == 0)
				{
					pool = onDuty.Where(staff.Contains).ToList();
				}
				if (pool.Count > 0)
				{
					return (await NextRoundRobinAsync(pool, ct), rule.Label);
				}
				break;
			}
			case CrmRuleCondition.Fallback:
				return (null, rule.Label);
			}
		}
		return (null, "No rule matched");
	}

	private async Task<long?> TargetAsync(CrmAssignmentRule rule, HashSet<long> onDuty, HashSet<long> staff, CancellationToken ct)
	{
		switch (rule.TargetType)
		{
		case CrmRuleTarget.NamedUser:
			return rule.TargetUserId.HasValue && staff.Contains(rule.TargetUserId.Value) ? rule.TargetUserId : null;
		case CrmRuleTarget.Manager:
		{
			List<long> managers = (await ManagersAsync(ct)).Where(staff.Contains).ToList();
			if (managers.Count == 0)
			{
				return null;
			}
			List<long> dutyManagers = managers.Where(onDuty.Contains).ToList();
			return await NextRoundRobinAsync(dutyManagers.Count > 0 ? dutyManagers : managers, ct);
		}
		default:
		{
			List<long> pool = onDuty.Where(staff.Contains).ToList();
			return pool.Count == 0 ? null : await NextRoundRobinAsync(pool, ct);
		}
		}
	}

	/// <summary>Round robin: whoever was assigned longest ago (never first).</summary>
	private async Task<long> NextRoundRobinAsync(List<long> pool, CancellationToken ct)
	{
		List<CrmTeamMember> members = await db.Set<CrmTeamMember>().Where(m => pool.Contains(m.UserId)).ToListAsync(ct);
		long pick = pool.OrderBy(id => members.FirstOrDefault(m => m.UserId == id)?.LastAssignedUtc ?? DateTime.MinValue).ThenBy(id => id).First();
		CrmTeamMember? row = members.FirstOrDefault(m => m.UserId == pick);
		if (row != null)
		{
			row.LastAssignedUtc = Now;
		}
		return pick;
	}

	// ---- writing ---------------------------------------------------------------------------

	public CrmLeadActivity Log(long leadId, CrmActivityKind kind, string detail, long? byUserId, bool system = false, DateTime? at = null)
	{
		CrmLeadActivity row = new CrmLeadActivity
		{
			LeadId = leadId,
			Kind = kind,
			Detail = CrmText.Clip(detail, 2000) ?? kind.ToString(),
			HappenedUtc = at ?? Now,
			ByUserId = system ? null : byUserId,
			IsSystem = system
		};
		db.Set<CrmLeadActivity>().Add(row);
		return row;
	}

	public async Task<string> NextLeadNumberAsync(CancellationToken ct) => await numbers.NextAsync("CrmLead", ct);

	/// <summary>Starts the response clock and assigns. Call after the lead row is saved (it needs the id).</summary>
	public async Task AfterCreateAsync(CrmLead lead, string sourceCode, long? assignToUserId, bool useRules, long? actorUserId, CancellationToken ct)
	{
		CrmSettingsSnapshot s = await SettingsAsync(ct);
		int? target = TargetFor(lead.Channel, sourceCode, s);
		if (target.HasValue && lead.FirstContactUtc == null)
		{
			lead.ResponseDueUtc = s.AddWorkingMinutes(lead.CreatedDate == default ? Now : lead.CreatedDate, target.Value);
		}
		string rule = "";
		if (assignToUserId.HasValue)
		{
			lead.OwnerUserId = assignToUserId;
		}
		else if (useRules)
		{
			(long? owner, string label) = await ApplyRulesAsync(lead, ct);
			lead.OwnerUserId = owner;
			rule = label;
		}
		if (lead.OwnerUserId.HasValue)
		{
			string ownerName = await UserNameAsync(lead.OwnerUserId, ct);
			Log(lead.LeadId, CrmActivityKind.Assignment, useRules && !assignToUserId.HasValue ? $"Assigned to {ownerName} by the rules ({rule})." : $"Assigned to {ownerName}.", actorUserId, useRules && !assignToUserId.HasValue);
		}
		else
		{
			Log(lead.LeadId, CrmActivityKind.Assignment, $"Left unassigned ({(rule == "" ? "nobody chosen" : rule)}). The manager has been told.", actorUserId, true);
		}
	}

	// ---- notifications ---------------------------------------------------------------------

	public void InApp(long? leadId, string kind, long userId, string title, string? body)
	{
		db.Set<CrmNotification>().Add(new CrmNotification
		{
			LeadId = leadId,
			Kind = kind,
			RecipientUserId = userId,
			Channel = "inapp",
			Title = CrmText.Clip(title, 200) ?? kind,
			Body = CrmText.Clip(body, 2000),
			CreatedUtc = Now,
			SentUtc = Now,
			Delivered = true
		});
	}

	/// <summary>Sends now when email is switched on in CRM Settings; every attempt is logged with its result.</summary>
	public async Task EmailAsync(long? leadId, string kind, long? userId, string? address, string subject, string html, CancellationToken ct, bool isAcknowledgement = false)
	{
		CrmSettingsSnapshot s = await SettingsAsync(ct);
		if (!s.EmailEnabled || (isAcknowledgement && !s.AckEmailEnabled))
		{
			return;
		}
		if (string.IsNullOrWhiteSpace(address) && userId.HasValue)
		{
			address = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.Email).FirstOrDefaultAsync(ct);
		}
		if (string.IsNullOrWhiteSpace(address))
		{
			return;
		}
		CrmNotification log = new CrmNotification
		{
			LeadId = leadId,
			Kind = kind,
			RecipientUserId = userId,
			RecipientAddress = address,
			Channel = "email",
			Title = CrmText.Clip(subject, 200) ?? kind,
			CreatedUtc = Now
		};
		string? error = await mailer.SendAsync(new[] { address }, subject, Wrap(html), Array.Empty<(string, string, byte[])>(), ct);
		log.SentUtc = Now;
		log.Delivered = error == null;
		log.Error = CrmText.Clip(error, 500);
		log.ReadUtc = Now;
		db.Set<CrmNotification>().Add(log);
		if (error != null)
		{
			logger.LogWarning("CRM email {Kind} to {Address} failed: {Error}", kind, address, error);
		}
	}

	public static string Wrap(string inner) =>
		"<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:14px;line-height:1.5;color:#1d1d24;max-width:560px\">" + inner +
		"<p style=\"color:#77736b;font-size:12px;margin-top:24px\">Regalis WorkSpace</p></div>";

	/// <summary>A new lead: tell the owner at once (in-app + email), or every manager when nobody owns it.</summary>
	public async Task AnnounceNewLeadAsync(CrmLead lead, string sourceName, CancellationToken ct)
	{
		string what = lead.LookingForId.HasValue
			? (await db.Set<CrmLookingFor>().AsNoTracking().Where(l => l.LookingForId == lead.LookingForId).Select(l => l.Name).FirstOrDefaultAsync(ct) ?? "")
			: "";
		string summary = $"{lead.Name}{(lead.Company == null ? "" : " — " + lead.Company)} · {sourceName}{(what == "" ? "" : " · " + what)}";
		string route = $"/admin/crm/leads/{lead.LeadId}";
		if (lead.OwnerUserId.HasValue)
		{
			InApp(lead.LeadId, "lead.new", lead.OwnerUserId.Value, $"New lead {lead.LeadNumber} is yours", summary);
			await EmailAsync(lead.LeadId, "lead.new", lead.OwnerUserId, null, $"New lead {lead.LeadNumber}: {lead.Name}",
				$"<p>A new lead has been assigned to you.</p><p><b>{CrmText.Html(summary)}</b></p><p>Mobile: {CrmText.Html(lead.Mobile)}</p>" +
				(lead.ResponseDueUtc.HasValue ? $"<p>Reply by <b>{CrmText.Ist(lead.ResponseDueUtc.Value)} IST</b>.</p>" : "") +
				$"<p>Open it in the admin portal: {CrmText.Html(route)}</p>", ct);
			return;
		}
		foreach (long manager in await ManagersAsync(ct))
		{
			InApp(lead.LeadId, "lead.unassigned", manager, $"Unassigned lead {lead.LeadNumber}", summary + " · nobody is on duty to take it.");
			await EmailAsync(lead.LeadId, "lead.unassigned", manager, null, $"Unassigned lead {lead.LeadNumber}: {lead.Name}",
				$"<p>A new lead arrived and nobody is on duty to take it.</p><p><b>{CrmText.Html(summary)}</b></p><p>Assign it in the admin portal: {CrmText.Html(route)}</p>", ct);
		}
	}
}
