using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Crm;
using Regalis.Domain.Entities.Crm;

namespace Regalis.Infrastructure.Services.Crm;

public partial class CrmService
{
	public async Task<CrmSettingsDto> GetSettingsAsync(CancellationToken ct = default)
	{
		EnsureStaff();
		return (await engine.SettingsAsync(ct)).ToDto();
	}

	public async Task<CrmSettingsDto> SaveSettingsAsync(CrmSettingsDto s, CancellationToken ct = default)
	{
		EnsureStaff();
		void Range(int value, int min, int max, string what)
		{
			if (value < min || value > max)
			{
				throw new BusinessRuleException($"{what} must be between {min} and {max}.");
			}
		}
		Range(s.TargetWebMinutes, 1, 1440, "The website enquiry target (minutes)");
		Range(s.TargetWhatsAppMinutes, 1, 1440, "The WhatsApp target (minutes)");
		Range(s.TargetCallbackMinutes, 1, 1440, "The callback target (minutes)");
		Range(s.TargetEmailMinutes, 1, 5000, "The email target (minutes)");
		Range(s.EscalateMinutes, 1, 5000, "Escalate to the manager after (minutes)");
		Range(s.VisitRetentionDays, 7, 730, "Anonymous visits kept (days)");
		Range(s.LostRetentionMonths, 1, 120, "A lost lead kept (months)");
		if (!TimeSpan.TryParseExact(s.WorkStart, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan start) || !TimeSpan.TryParseExact(s.WorkEnd, @"hh\:mm", CultureInfo.InvariantCulture, out TimeSpan end) || end <= start)
		{
			throw new BusinessRuleException("Working hours need a start and an end time, with the end after the start.");
		}
		List<int> days = (s.WorkDays ?? new List<int>()).Where(d => d >= 1 && d <= 7).Distinct().OrderBy(d => d).ToList();
		if (days.Count == 0)
		{
			throw new BusinessRuleException("Choose at least one working day.");
		}
		string consent = CrmText.Clip(s.ConsentText, 1000) ?? throw new BusinessRuleException("Enter the wording shown beside the consent tick box.");
		string marketing = CrmText.Clip(s.MarketingText, 1000) ?? CrmEngine.DefaultMarketing;
		CrmSettingsSnapshot current = await engine.SettingsAsync(ct);
		if (s.EmailEnabled && !current.EmailEnabled && current.EmailTestedUtc == null)
		{
			throw new BusinessRuleException("Send a test email first and check that it arrived. A reminder that silently fails is worse than no reminder at all.");
		}
		string? wa = CrmText.Clip(s.WhatsAppNumber, 20);
		if (wa != null && !CrmText.IsValidMobile(wa) && new string(wa.Where(char.IsAsciiDigit).ToArray()).Length is < 10 or > 15)
		{
			throw new BusinessRuleException("The WhatsApp number should be a mobile number with the country code, for example +91 98450 00000.");
		}
		string? privacy = CrmText.Clip(s.PrivacyUrl, 300);
		if (privacy != null && !(privacy.StartsWith("/", StringComparison.Ordinal) || privacy.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
		{
			throw new BusinessRuleException("The privacy notice link must start with https:// or /.");
		}
		Dictionary<string, string?> values = new Dictionary<string, string?>
		{
			["target.web"] = s.TargetWebMinutes.ToString(CultureInfo.InvariantCulture),
			["target.whatsapp"] = s.TargetWhatsAppMinutes.ToString(CultureInfo.InvariantCulture),
			["target.callback"] = s.TargetCallbackMinutes.ToString(CultureInfo.InvariantCulture),
			["target.email"] = s.TargetEmailMinutes.ToString(CultureInfo.InvariantCulture),
			["target.escalate"] = s.EscalateMinutes.ToString(CultureInfo.InvariantCulture),
			["hours.start"] = start.ToString(@"hh\:mm"),
			["hours.end"] = end.ToString(@"hh\:mm"),
			["hours.days"] = string.Join(",", days),
			["consent.text"] = consent,
			["consent.marketing"] = marketing,
			["retention.visitDays"] = s.VisitRetentionDays.ToString(CultureInfo.InvariantCulture),
			["retention.lostMonths"] = s.LostRetentionMonths.ToString(CultureInfo.InvariantCulture),
			["email.enabled"] = s.EmailEnabled ? "1" : "0",
			["email.ack"] = s.AckEmailEnabled ? "1" : "0",
			["email.digest"] = s.DigestEnabled ? "1" : "0",
			["public.whatsapp"] = wa,
			["public.phone"] = CrmText.Clip(s.PublicPhone, 30),
			["public.privacyUrl"] = privacy
		};
		foreach (var kv in values)
		{
			await engine.SetSettingAsync(kv.Key, kv.Value, Me, ct);
		}
		await db.SaveChangesAsync(ct);
		engine.ForgetSettings();
		return (await engine.SettingsAsync(ct)).ToDto();
	}

	/// <summary>Section 8.5: prove email actually leaves production before switching on a single notification.
	/// This sends even while CRM email is switched off.</summary>
	public async Task<string> SendTestEmailAsync(CrmTestEmailRequest request, CancellationToken ct = default)
	{
		EnsureStaff();
		string to = CrmText.Clip(request.To, 256) ?? throw new BusinessRuleException("Enter the address to send the test to — ideally one outside the company.");
		if (!CrmText.IsValidEmail(to))
		{
			throw new BusinessRuleException("Check the email address.");
		}
		string? error = await mailerProxy.SendAsync(new[] { to }, "Regalis CRM: test email",
			CrmEngine.Wrap($"<p>This is a test from Regalis CRM, sent by {CrmText.Html(currentUser.FullName)} at {CrmText.Ist(Now)} IST.</p><p>If you can read this, CRM notifications can be switched on in CRM Settings.</p>"),
			Array.Empty<(string, string, byte[])>(), ct);
		db.Set<CrmNotification>().Add(new CrmNotification
		{
			Kind = "test",
			RecipientAddress = to,
			RecipientUserId = Me,
			Channel = "email",
			Title = "Regalis CRM: test email",
			CreatedUtc = Now,
			SentUtc = Now,
			Delivered = error == null,
			Error = CrmText.Clip(error, 500),
			ReadUtc = Now
		});
		if (error != null)
		{
			await db.SaveChangesAsync(ct);
			throw new BusinessRuleException("The test email was not sent: " + error);
		}
		await engine.SetSettingAsync("email.testedUtc", Now.ToString("o", CultureInfo.InvariantCulture), Me, ct);
		await engine.SetSettingAsync("email.testedTo", to, Me, ct);
		await db.SaveChangesAsync(ct);
		return $"Sent to {to}. Open it and check it arrived (and is not in spam) before switching notifications on.";
	}

	// ---- editable lists ----------------------------------------------------------------------

	public async Task<object> SaveListItemAsync(string list, int? id, CrmSaveListItemRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		string name = CrmText.Clip(r.Name, 100) ?? throw new BusinessRuleException("Enter a name.");
		switch (list)
		{
		case "sources":
		{
			CrmSource row = id.HasValue ? await db.Set<CrmSource>().FirstOrDefaultAsync(x => x.SourceId == id, ct) ?? throw new NotFoundException("Source", id.Value) : new CrmSource();
			if (await db.Set<CrmSource>().AnyAsync(x => x.Name == name && x.SourceId != row.SourceId, ct))
			{
				throw new BusinessRuleException("There is already a source with that name.");
			}
			if (!id.HasValue)
			{
				row.Code = "SRC_" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
				row.SortOrder = (await db.Set<CrmSource>().MaxAsync(x => (int?)x.SortOrder, ct) ?? 0) + 10;
				row.IsWebsite = r.IsWebsite ?? false;
				db.Set<CrmSource>().Add(row);
			}
			row.Name = name;
			row.UtmSource = CrmText.Clip(r.UtmSource, 200)?.ToLowerInvariant();
			row.UtmMedium = CrmText.Clip(r.UtmMedium, 100)?.ToLowerInvariant();
			row.ReferrerMatch = CrmText.Clip(r.ReferrerMatch, 200)?.ToLowerInvariant();
			if (r.IsActive.HasValue)
			{
				row.IsActive = r.IsActive.Value;
			}
			await db.SaveChangesAsync(ct);
			return ToDto(row);
		}
		case "statuses":
		{
			CrmStatus row = id.HasValue ? await db.Set<CrmStatus>().FirstOrDefaultAsync(x => x.StatusId == id, ct) ?? throw new NotFoundException("Status", id.Value) : new CrmStatus();
			bool builtIn = BuiltInStatuses.Contains(row.Code);
			if (!id.HasValue)
			{
				row.Code = "CUSTOM_" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpperInvariant();
				// A new stage goes before Won and Lost.
				int wonOrder = await db.Set<CrmStatus>().Where(x => x.Code == "WON").Select(x => x.SortOrder).FirstAsync(ct);
				int maxOpen = await db.Set<CrmStatus>().Where(x => !x.IsClosed).MaxAsync(x => (int?)x.SortOrder, ct) ?? 0;
				row.SortOrder = Math.Min(maxOpen + 5, wonOrder - 1);
				row.Requires = CrmStatusRequirement.None;
				db.Set<CrmStatus>().Add(row);
			}
			if (builtIn && r.IsActive == false)
			{
				throw new BusinessRuleException("The six built-in stages cannot be switched off; rename them instead.");
			}
			row.Name = CrmText.Clip(name, 60)!;
			row.Description = CrmText.Clip(r.Description, 200);
			row.Colour = r.Colour is "neutral" or "info" or "warning" or "success" or "danger" or "gold" ? r.Colour : "neutral";
			if (r.IsActive.HasValue && !builtIn)
			{
				if (!r.IsActive.Value && id.HasValue && await db.Set<CrmLead>().AnyAsync(l => l.StatusId == row.StatusId && l.ClosedUtc == null, ct))
				{
					throw new BusinessRuleException("Leads are still in this stage. Move them to another stage first.");
				}
				row.IsActive = r.IsActive.Value;
			}
			await db.SaveChangesAsync(ct);
			return ToDto(row);
		}
		case "lost-reasons":
		{
			CrmLostReason row = id.HasValue ? await db.Set<CrmLostReason>().FirstOrDefaultAsync(x => x.LostReasonId == id, ct) ?? throw new NotFoundException("Lost reason", id.Value) : new CrmLostReason();
			if (!id.HasValue)
			{
				row.SortOrder = (await db.Set<CrmLostReason>().MaxAsync(x => (int?)x.SortOrder, ct) ?? 0) + 10;
				db.Set<CrmLostReason>().Add(row);
			}
			row.Name = name;
			row.Hint = CrmText.Clip(r.Hint, 200);
			if (r.IsActive.HasValue)
			{
				row.IsActive = r.IsActive.Value;
			}
			await db.SaveChangesAsync(ct);
			return ToDto(row);
		}
		case "looking-for":
		{
			CrmLookingFor row = id.HasValue ? await db.Set<CrmLookingFor>().FirstOrDefaultAsync(x => x.LookingForId == id, ct) ?? throw new NotFoundException("Looking for", id.Value) : new CrmLookingFor();
			if (!id.HasValue)
			{
				row.SortOrder = (await db.Set<CrmLookingFor>().MaxAsync(x => (int?)x.SortOrder, ct) ?? 0) + 10;
				db.Set<CrmLookingFor>().Add(row);
			}
			row.Name = name;
			row.WorkspaceTypeId = r.WorkspaceTypeId;
			row.IsLarge = r.IsLarge ?? row.IsLarge;
			if (r.IsActive.HasValue)
			{
				row.IsActive = r.IsActive.Value;
			}
			await db.SaveChangesAsync(ct);
			return ToDto(row);
		}
		default:
			throw new NotFoundException("CRM list", 0);
		}
	}

	public async Task ReorderAsync(string list, List<int> ids, CancellationToken ct = default)
	{
		EnsureStaff();
		if (ids == null || ids.Count == 0)
		{
			return;
		}
		switch (list)
		{
		case "assignment-rules":
		{
			List<CrmAssignmentRule> rows = await db.Set<CrmAssignmentRule>().ToListAsync(ct);
			for (int i = 0; i < ids.Count; i++)
			{
				CrmAssignmentRule? row = rows.FirstOrDefault(x => x.RuleId == ids[i]);
				if (row != null)
				{
					row.SortOrder = (i + 1) * 10;
				}
			}
			break;
		}
		case "lost-reasons":
		{
			List<CrmLostReason> rows = await db.Set<CrmLostReason>().ToListAsync(ct);
			for (int i = 0; i < ids.Count; i++)
			{
				CrmLostReason? row = rows.FirstOrDefault(x => x.LostReasonId == ids[i]);
				if (row != null)
				{
					row.SortOrder = (i + 1) * 10;
				}
			}
			break;
		}
		case "looking-for":
		{
			List<CrmLookingFor> rows = await db.Set<CrmLookingFor>().ToListAsync(ct);
			for (int i = 0; i < ids.Count; i++)
			{
				CrmLookingFor? row = rows.FirstOrDefault(x => x.LookingForId == ids[i]);
				if (row != null)
				{
					row.SortOrder = (i + 1) * 10;
				}
			}
			break;
		}
		case "sources":
		{
			List<CrmSource> rows = await db.Set<CrmSource>().ToListAsync(ct);
			for (int i = 0; i < ids.Count; i++)
			{
				CrmSource? row = rows.FirstOrDefault(x => x.SourceId == ids[i]);
				if (row != null)
				{
					row.SortOrder = (i + 1) * 10;
				}
			}
			break;
		}
		case "statuses":
		{
			// Open stages only; Won and Lost always stay last.
			List<CrmStatus> rows = await db.Set<CrmStatus>().ToListAsync(ct);
			int order = 10;
			foreach (int id in ids)
			{
				CrmStatus? row = rows.FirstOrDefault(x => x.StatusId == id && !x.IsClosed);
				if (row != null)
				{
					row.SortOrder = order;
					order += 10;
				}
			}
			foreach (CrmStatus closed in rows.Where(x => x.IsClosed).OrderBy(x => x.IsWon ? 0 : 1))
			{
				closed.SortOrder = 900 + order;
				order += 10;
			}
			break;
		}
		default:
			throw new NotFoundException("CRM list", 0);
		}
		await db.SaveChangesAsync(ct);
	}

	public async Task<List<CrmAssignmentRuleDto>> GetRulesAsync(CancellationToken ct = default)
	{
		EnsureStaff();
		return await db.Set<CrmAssignmentRule>().AsNoTracking().OrderBy(r => r.SortOrder).ThenBy(r => r.RuleId)
			.Select(r => new CrmAssignmentRuleDto(r.RuleId, r.Label, (byte)r.ConditionType, r.LocationId, r.MinPeople, r.LookingForId, (byte)r.TargetType, r.TargetUserId, r.IsActive, r.SortOrder))
			.ToListAsync(ct);
	}

	public async Task<CrmAssignmentRuleDto> SaveRuleAsync(int? ruleId, CrmSaveRuleRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		if (!Enum.IsDefined(typeof(CrmRuleCondition), r.ConditionType) || !Enum.IsDefined(typeof(CrmRuleTarget), r.TargetType))
		{
			throw new BusinessRuleException("Choose what the rule checks and who the lead goes to.");
		}
		string label = CrmText.Clip(r.Label, 200) ?? throw new BusinessRuleException("Describe the rule in a few words.");
		if ((CrmRuleTarget)r.TargetType == CrmRuleTarget.NamedUser && !r.TargetUserId.HasValue)
		{
			throw new BusinessRuleException("Choose the person this rule gives leads to.");
		}
		CrmAssignmentRule row = ruleId.HasValue ? await db.Set<CrmAssignmentRule>().FirstOrDefaultAsync(x => x.RuleId == ruleId, ct) ?? throw new NotFoundException("Rule", ruleId.Value) : new CrmAssignmentRule();
		if (!ruleId.HasValue)
		{
			int fallback = await db.Set<CrmAssignmentRule>().Where(x => x.ConditionType == CrmRuleCondition.Fallback).Select(x => (int?)x.SortOrder).MinAsync(ct) ?? 1000;
			row.SortOrder = fallback - 1;
			db.Set<CrmAssignmentRule>().Add(row);
		}
		if (row.ConditionType == CrmRuleCondition.Fallback && ruleId.HasValue && (CrmRuleCondition)r.ConditionType != CrmRuleCondition.Fallback)
		{
			throw new BusinessRuleException("The safety-net rule cannot be changed into another kind of rule.");
		}
		if (row.ConditionType == CrmRuleCondition.Fallback && ruleId.HasValue && !r.IsActive)
		{
			throw new BusinessRuleException("The safety-net rule cannot be switched off: without it a lead could belong to nobody without anyone being told.");
		}
		row.Label = label;
		row.ConditionType = (CrmRuleCondition)r.ConditionType;
		row.LocationId = r.LocationId;
		row.MinPeople = r.MinPeople;
		row.LookingForId = r.LookingForId;
		row.TargetType = (CrmRuleTarget)r.TargetType;
		row.TargetUserId = r.TargetUserId;
		row.IsActive = r.IsActive;
		await db.SaveChangesAsync(ct);
		return new CrmAssignmentRuleDto(row.RuleId, row.Label, (byte)row.ConditionType, row.LocationId, row.MinPeople, row.LookingForId, (byte)row.TargetType, row.TargetUserId, row.IsActive, row.SortOrder);
	}

	public async Task<CrmTeamMemberDto> SaveTeamMemberAsync(long userId, CrmSaveTeamMemberRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		var staff = (await engine.CrmStaffAsync(ct)).FirstOrDefault(s => s.UserId == userId);
		if (staff.UserId == 0)
		{
			throw new BusinessRuleException("Only staff whose role includes CRM can be on the lead team. Give their role CRM access in Roles & Permissions first.");
		}
		CrmTeamMember? row = await db.Set<CrmTeamMember>().IgnoreQueryFilters().FirstOrDefaultAsync(x => x.UserId == userId, ct);
		if (row == null)
		{
			row = new CrmTeamMember { UserId = userId };
			db.Set<CrmTeamMember>().Add(row);
		}
		row.IsActive = r.IsMember;
		row.IsDeleted = false;
		row.IsOnDuty = r.IsMember && r.IsOnDuty;
		row.IsManager = r.IsMember && r.IsManager;
		row.LocationId = r.LocationId;
		await db.SaveChangesAsync(ct);
		return (await TeamAsync(ct)).First(t => t.UserId == userId);
	}
}
