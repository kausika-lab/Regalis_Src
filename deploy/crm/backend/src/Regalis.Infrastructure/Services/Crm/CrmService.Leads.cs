using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Abstractions;
using Regalis.Application.Administration;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Crm;
using Regalis.Domain.Entities.Crm;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Excel;
using Regalis.Infrastructure.Persistence;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.Crm;

/// <summary>
/// CRM admin service (document sections 3-11). Row-level rule: without CRM.LEAD.VIEWALL a user
/// sees their own leads plus the unassigned pool, nothing more (section 15.2).
/// </summary>
public partial class CrmService(RegalisDbContext db, CrmEngine engine, ICurrentUser currentUser, IUserAdminService userAdmin, Regalis.Infrastructure.Reporting.Analytics.IReportMailer mailerProxy, ILogger<CrmService> logger) : ICrmService
{
	private DateTime Now => engine.Now;

	private long Me => currentUser.UserId;

	private bool CanViewAll => currentUser.HasPermission("CRM.LEAD.VIEWALL");

	private IQueryable<CrmLead> Visible()
	{
		IQueryable<CrmLead> q = db.Set<CrmLead>().AsNoTracking();
		if (!CanViewAll)
		{
			long me = Me;
			q = q.Where(l => l.OwnerUserId == me || l.OwnerUserId == null);
		}
		return q;
	}

	private async Task<CrmLead> RequireLeadAsync(long leadId, CancellationToken ct)
	{
		CrmLead lead = await db.Set<CrmLead>().FirstOrDefaultAsync(l => l.LeadId == leadId, ct) ?? throw new NotFoundException("Lead", leadId);
		if (!CanViewAll && lead.OwnerUserId.HasValue && lead.OwnerUserId != Me)
		{
			throw new NotFoundException("Lead", leadId);
		}
		return lead;
	}

	private void EnsureStaff()
	{
		if (currentUser.UserType == UserType.Customer)
		{
			throw new ForbiddenException();
		}
	}

	// ---- options -----------------------------------------------------------------------------

	public async Task<CrmOptionsDto> GetOptionsAsync(CancellationToken ct = default)
	{
		EnsureStaff();
		List<CrmSourceDto> sources = (await db.Set<CrmSource>().AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.SourceId).ToListAsync(ct)).Select(ToDto).ToList();
		List<CrmStatusDto> statuses = (await db.Set<CrmStatus>().AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.StatusId).ToListAsync(ct)).Select(ToDto).ToList();
		List<CrmLostReasonDto> reasons = (await db.Set<CrmLostReason>().AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.LostReasonId).ToListAsync(ct)).Select(ToDto).ToList();
		List<CrmLookingForDto> looking = (await db.Set<CrmLookingFor>().AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.LookingForId).ToListAsync(ct)).Select(ToDto).ToList();
		List<CrmIdName> locations = await db.Locations.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.LocationName).Select(l => new CrmIdName(l.LocationId, l.LocationName)).ToListAsync(ct);
		List<CrmTeamMemberDto> team = await TeamAsync(ct);
		CrmMeDto me = new CrmMeDto(Me, currentUser.FullName ?? "", CanViewAll, currentUser.HasPermission("CRM.LEAD.CREATE"), currentUser.HasPermission("CRM.LEAD.EDIT"),
			currentUser.HasPermission("CRM.LEAD.ASSIGN"), currentUser.HasPermission("CRM.LEAD.CONVERT"), currentUser.HasPermission("CRM.LEAD.DELETE"),
			currentUser.HasPermission("CRM.VISITORS.VIEW"), currentUser.HasPermission("CRM.REPORTS.VIEW"), currentUser.HasPermission("CRM.SETTINGS"));
		return new CrmOptionsDto(sources, statuses, reasons, looking, locations, team, me, (await engine.SettingsAsync(ct)).ToDto());
	}

	private async Task<List<CrmTeamMemberDto>> TeamAsync(CancellationToken ct)
	{
		List<(long UserId, string Name, string Email, string Roles)> staff = await engine.CrmStaffAsync(ct);
		Dictionary<long, CrmTeamMember> members = await db.Set<CrmTeamMember>().AsNoTracking().Where(m => m.IsActive).ToDictionaryAsync(m => m.UserId, ct);
		HashSet<long> onDuty = await engine.OnDutyAsync(ct);
		return staff.Select(s =>
		{
			members.TryGetValue(s.UserId, out CrmTeamMember? m);
			return new CrmTeamMemberDto(s.UserId, s.Name, s.Email, m != null, m?.IsOnDuty ?? false, onDuty.Contains(s.UserId) && !(m?.IsOnDuty ?? false), m?.IsManager ?? false, m?.LocationId, s.Roles);
		}).ToList();
	}

	internal static CrmSourceDto ToDto(CrmSource s) => new CrmSourceDto(s.SourceId, s.Code, s.Name, s.IsWebsite, s.UtmSource, s.UtmMedium, s.ReferrerMatch, s.IsActive, s.SortOrder);

	internal static CrmStatusDto ToDto(CrmStatus s) => new CrmStatusDto(s.StatusId, s.Code, s.Name, s.Description, s.IsClosed, s.IsWon, (byte)s.Requires, s.Colour, s.IsActive, s.SortOrder, BuiltInStatuses.Contains(s.Code));

	internal static CrmLostReasonDto ToDto(CrmLostReason s) => new CrmLostReasonDto(s.LostReasonId, s.Name, s.Hint, s.IsActive, s.SortOrder);

	internal static CrmLookingForDto ToDto(CrmLookingFor s) => new CrmLookingForDto(s.LookingForId, s.Name, s.WorkspaceTypeId, s.IsLarge, s.IsActive, s.SortOrder);

	internal static readonly HashSet<string> BuiltInStatuses = new HashSet<string> { "NEW", "CONTACTED", "VISIT", "QUOTE", "WON", "LOST" };

	// ---- list ----------------------------------------------------------------------------------

	private IQueryable<CrmLead> Filtered(CrmLeadListRequest r, bool withDateAndStatus)
	{
		IQueryable<CrmLead> q = Visible();
		if (!string.IsNullOrWhiteSpace(r.Search))
		{
			string term = r.Search.Trim();
			string digits = new string(term.Where(char.IsAsciiDigit).ToArray());
			string last10 = digits.Length > 10 ? digits.Substring(digits.Length - 10) : digits;
			string lower = term.ToLowerInvariant();
			q = digits.Length >= 4
				? q.Where(l => l.MobileNormalised.Contains(last10) || l.LeadNumber.Contains(term))
				: q.Where(l => l.Name.Contains(term) || (l.Company != null && l.Company.Contains(term)) || (l.EmailNormalised != null && l.EmailNormalised.Contains(lower)) || l.LeadNumber.Contains(term));
		}
		if (r.SourceId.HasValue)
		{
			q = q.Where(l => l.SourceId == r.SourceId);
		}
		if (r.OwnerId.HasValue)
		{
			q = r.OwnerId == 0 ? q.Where(l => l.OwnerUserId == null) : q.Where(l => l.OwnerUserId == r.OwnerId);
		}
		if (r.LookingForId.HasValue)
		{
			q = q.Where(l => l.LookingForId == r.LookingForId);
		}
		if (!withDateAndStatus)
		{
			return q;
		}
		if (r.StatusId.HasValue)
		{
			q = q.Where(l => l.StatusId == r.StatusId);
		}
		int days = r.Days ?? 30;
		string preset = (r.Preset ?? "").ToLowerInvariant();
		if (preset == "new-week")
		{
			DateTime since = Now.AddDays(-7);
			q = q.Where(l => l.CreatedDate >= since);
		}
		else if (preset == "awaiting")
		{
			q = q.Where(l => l.FirstContactUtc == null && l.ClosedUtc == null);
		}
		else if (preset == "won-month")
		{
			DateTime monthStart = MonthStartUtc();
			q = q.Where(l => l.ConvertedUtc != null && l.ConvertedUtc >= monthStart);
		}
		else if (preset == "open")
		{
			// Pipeline: every open lead, plus this month's won and lost.
			DateTime monthStart = MonthStartUtc();
			q = q.Where(l => l.ClosedUtc == null || l.ClosedUtc >= monthStart);
		}
		else if (days > 0)
		{
			DateTime since = Now.AddDays(-days);
			q = q.Where(l => l.CreatedDate >= since || (l.ClosedUtc == null && l.FirstContactUtc == null));
		}
		return q;
	}

	private DateTime MonthStartUtc()
	{
		DateTime ist = IstClock.ToIst(Now);
		return IstClock.ToUtc(new DateTime(ist.Year, ist.Month, 1));
	}

	private (DateTime StartUtc, DateTime EndUtc) TodayUtc()
	{
		DateTime ist = IstClock.ToIst(Now).Date;
		return (IstClock.ToUtc(ist), IstClock.ToUtc(ist.AddDays(1)));
	}

	public async Task<CrmLeadListDto> ListLeadsAsync(CrmLeadListRequest request, CancellationToken ct = default)
	{
		EnsureStaff();
		int pageSize = Math.Clamp(request.PageSize, 1, 500);
		int page = Math.Max(1, request.Page);
		IQueryable<CrmLead> q = Filtered(request, true);
		int total = await q.CountAsync(ct);
		List<CrmLead> leads = await Sort(q, request).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
		List<CrmLeadRowDto> rows = await ShapeRowsAsync(leads, ct);
		if ((request.SortBy ?? "").Equals("followup", StringComparison.OrdinalIgnoreCase))
		{
			rows = (request.IsDescending ? rows.OrderByDescending(r => r.NextFollowUpUtc ?? DateTime.MinValue) : rows.OrderBy(r => r.NextFollowUpUtc ?? DateTime.MaxValue)).ToList();
		}
		return new CrmLeadListDto(PagedResult<CrmLeadRowDto>.Create(rows, page, pageSize, total), await SummaryAsync(Filtered(request, false), ct));
	}

	private static IQueryable<CrmLead> Sort(IQueryable<CrmLead> q, CrmLeadListRequest r)
	{
		bool d = r.IsDescending;
		return (r.SortBy ?? "").ToLowerInvariant() switch
		{
			"name" => d ? q.OrderByDescending(l => l.Name) : q.OrderBy(l => l.Name),
			"source" => d ? q.OrderByDescending(l => l.SourceId) : q.OrderBy(l => l.SourceId),
			"status" => d ? q.OrderByDescending(l => l.StatusId) : q.OrderBy(l => l.StatusId),
			"owner" => d ? q.OrderByDescending(l => l.OwnerUserId) : q.OrderBy(l => l.OwnerUserId),
			"lookingfor" => d ? q.OrderByDescending(l => l.LookingForId) : q.OrderBy(l => l.LookingForId),
			_ => d ? q.OrderByDescending(l => l.CreatedDate).ThenByDescending(l => l.LeadId) : q.OrderBy(l => l.CreatedDate).ThenBy(l => l.LeadId)
		};
	}

	private async Task<CrmLeadSummaryDto> SummaryAsync(IQueryable<CrmLead> q, CancellationToken ct)
	{
		DateTime weekAgo = Now.AddDays(-7);
		DateTime monthStart = MonthStartUtc();
		DateTime ninety = Now.AddDays(-90);
		(DateTime todayStart, DateTime todayEnd) = TodayUtc();
		var newWeek = await q.Where(l => l.CreatedDate >= weekAgo).GroupBy(l => 1).Select(g => new { All = g.Count(), Web = g.Count(l => l.Channel != CrmChannel.Manual || l.VisitorId != null) }).FirstOrDefaultAsync(ct);
		var awaiting = await q.Where(l => l.FirstContactUtc == null && l.ClosedUtc == null).GroupBy(l => 1).Select(g => new { Count = g.Count(), Oldest = g.Min(l => l.CreatedDate) }).FirstOrDefaultAsync(ct);
		IQueryable<long> ids = q.Where(l => l.ClosedUtc == null).Select(l => l.LeadId);
		IQueryable<CrmFollowUp> open = db.Set<CrmFollowUp>().AsNoTracking().Where(f => !f.IsDone && ids.Contains(f.LeadId));
		int dueToday = await open.CountAsync(f => f.DueUtc >= todayStart && f.DueUtc < todayEnd, ct);
		int overdue = await open.CountAsync(f => f.DueUtc < Now, ct);
		List<CrmLead> wonMonth = await q.Where(l => l.ConvertedUtc != null && l.ConvertedUtc >= monthStart).ToListAsync(ct);
		decimal value = await ValueOfAsync(wonMonth, ct);
		var ninetyDays = await q.Where(l => l.CreatedDate >= ninety).GroupBy(l => 1).Select(g => new { All = g.Count(), Won = g.Count(l => l.ConvertedUtc != null) }).FirstOrDefaultAsync(ct);
		decimal? rate = ninetyDays == null || ninetyDays.All == 0 ? null : Math.Round(100m * ninetyDays.Won / ninetyDays.All, 1);
		return new CrmLeadSummaryDto(newWeek?.All ?? 0, newWeek?.Web ?? 0, awaiting?.Count ?? 0,
			awaiting == null ? null : (int)(Now - awaiting.Oldest).TotalMinutes, dueToday, overdue, wonMonth.Count, value, rate, ninetyDays?.Won ?? 0, ninetyDays?.All ?? 0);
	}

	/// <summary>Value of won business: the deal value typed on the lead, else the linked quotation's total.</summary>
	private async Task<decimal> ValueOfAsync(List<CrmLead> leads, CancellationToken ct)
	{
		List<long> qids = leads.Where(l => !l.DealValue.HasValue && l.QuotationId.HasValue).Select(l => l.QuotationId!.Value).ToList();
		Dictionary<long, decimal> totals = qids.Count == 0 ? new Dictionary<long, decimal>() :
			await db.Quotations.AsNoTracking().IgnoreQueryFilters().Where(x => qids.Contains(x.QuotationId)).ToDictionaryAsync(x => x.QuotationId, x => x.TotalAmount, ct);
		return leads.Sum(l => l.DealValue ?? (l.QuotationId.HasValue && totals.TryGetValue(l.QuotationId.Value, out decimal t) ? t : 0m));
	}

	internal async Task<List<CrmLeadRowDto>> ShapeRowsAsync(List<CrmLead> leads, CancellationToken ct)
	{
		if (leads.Count == 0)
		{
			return new List<CrmLeadRowDto>();
		}
		Dictionary<int, CrmStatus> statuses = await db.Set<CrmStatus>().AsNoTracking().ToDictionaryAsync(s => s.StatusId, ct);
		Dictionary<int, string> sources = await db.Set<CrmSource>().AsNoTracking().ToDictionaryAsync(s => s.SourceId, s => s.Name, ct);
		Dictionary<int, string> looking = await db.Set<CrmLookingFor>().AsNoTracking().ToDictionaryAsync(s => s.LookingForId, s => s.Name, ct);
		Dictionary<int, string> reasons = await db.Set<CrmLostReason>().AsNoTracking().ToDictionaryAsync(s => s.LostReasonId, s => s.Name, ct);
		List<long> userIds = leads.Where(l => l.OwnerUserId.HasValue).Select(l => l.OwnerUserId!.Value).Distinct().ToList();
		Dictionary<long, string> names = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => userIds.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
		List<long> leadIds = leads.Select(l => l.LeadId).ToList();
		List<CrmFollowUp> follow = await db.Set<CrmFollowUp>().AsNoTracking().Where(f => !f.IsDone && leadIds.Contains(f.LeadId)).ToListAsync(ct);
		Dictionary<long, CrmFollowUp> next = follow.GroupBy(f => f.LeadId).ToDictionary(g => g.Key, g => g.OrderBy(f => f.DueUtc).First());
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		return leads.Select(l =>
		{
			CrmStatus st = statuses.TryGetValue(l.StatusId, out CrmStatus? x) ? x : new CrmStatus { Name = "?", Colour = "neutral" };
			next.TryGetValue(l.LeadId, out CrmFollowUp? f);
			bool closed = l.ClosedUtc.HasValue;
			bool overdue = !closed && f != null && f.DueUtc < Now;
			int overdueDays = overdue ? Math.Max(1, (int)Math.Ceiling((Now - f!.DueUtc).TotalDays - 0.0001)) : 0;
			if (overdue && (Now - f!.DueUtc).TotalHours < 24)
			{
				overdueDays = IstClock.IstDateOf(Now) > IstClock.IstDateOf(f.DueUtc) ? 1 : 0;
			}
			bool untouched = !closed && l.FirstContactUtc == null;
			int? waiting = untouched ? (int)(Now - l.CreatedDate).TotalMinutes : null;
			string? reason = l.LostReasonId.HasValue && reasons.TryGetValue(l.LostReasonId.Value, out string? rn) ? rn : null;
			string urgency = closed
				? (st.IsWon ? (l.CustomerId.HasValue ? $"now customer #{l.CustomerId}" : "won") : (reason ?? "closed"))
				: untouched ? $"{CrmText.Minutes(waiting ?? 0)} waiting"
				: overdue ? (overdueDays >= 1 ? $"chase {overdueDays} day{(overdueDays == 1 ? "" : "s")} late" : $"chase {CrmText.Minutes((int)(Now - f!.DueUtc).TotalMinutes)} late")
				: f != null ? $"next: {CrmText.Ist(f.DueUtc)}"
				: "no follow-up set";
			return new CrmLeadRowDto(l.LeadId, l.LeadNumber, l.Name, l.Company, l.Mobile, l.Email,
				l.LookingForId, l.LookingForId.HasValue && looking.TryGetValue(l.LookingForId.Value, out string? lf) ? lf : null, l.PeopleCount,
				l.SourceId, sources.TryGetValue(l.SourceId, out string? sn) ? sn : "?", (byte)l.Channel,
				l.StatusId, st.Name, st.Colour, closed, st.IsWon && closed,
				l.OwnerUserId, l.OwnerUserId.HasValue && names.TryGetValue(l.OwnerUserId.Value, out string? on) ? on : null,
				closed ? null : f?.DueUtc, closed ? null : f?.WhatToDo, overdueDays, overdue, untouched, waiting, reason, l.CustomerId, l.DealValue,
				l.CreatedDate, l.ClosedUtc, urgency);
		}).ToList();
	}

	public async Task<byte[]> ExportLeadsAsync(CrmLeadListRequest request, CancellationToken ct = default)
	{
		EnsureStaff();
		List<CrmLead> leads = await Sort(Filtered(request, true), request).Take(5000).ToListAsync(ct);
		List<CrmLeadRowDto> rows = await ShapeRowsAsync(leads, ct);
		Dictionary<long, CrmLead> byId = leads.ToDictionary(l => l.LeadId);
		List<ExcelColumn<CrmLeadRowDto>> columns = new List<ExcelColumn<CrmLeadRowDto>>
		{
			new("Lead", r => r.LeadNumber),
			new("Name", r => r.Name),
			new("Company", r => r.Company),
			new("Mobile", r => r.Mobile),
			new("Email", r => r.Email),
			new("Looking for", r => r.LookingFor),
			new("People", r => r.PeopleCount, ExcelCellKind.Number),
			new("Budget", r => byId[r.LeadId].BudgetText),
			new("Source", r => r.Source),
			new("Source detail", r => byId[r.LeadId].SourceDetail),
			new("Status", r => r.Status),
			new("Owner", r => r.OwnerName ?? "Unassigned"),
			new("Next follow-up", r => r.NextFollowUpUtc.HasValue ? IstClock.ToIst(r.NextFollowUpUtc.Value) : null, ExcelCellKind.DateTime),
			new("Lost reason", r => r.LostReason),
			new("Deal value", r => r.DealValue, ExcelCellKind.Money),
			new("Created", r => IstClock.ToIst(r.CreatedUtc), ExcelCellKind.DateTime),
			new("Closed", r => r.ClosedUtc.HasValue ? IstClock.ToIst(r.ClosedUtc.Value) : null, ExcelCellKind.DateTime)
		};
		return ExcelWorkbookHelper.Write("Leads", columns, rows);
	}

	// ---- one lead ------------------------------------------------------------------------------

	public async Task<CrmLeadDetailDto> GetLeadAsync(long leadId, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await Visible().FirstOrDefaultAsync(l => l.LeadId == leadId, ct) ?? throw new NotFoundException("Lead", leadId);
		CrmLeadRowDto row = (await ShapeRowsAsync(new List<CrmLead> { lead }, ct))[0];
		string? location = lead.LocationId.HasValue ? await db.Locations.AsNoTracking().IgnoreQueryFilters().Where(l => l.LocationId == lead.LocationId).Select(l => l.LocationName).FirstOrDefaultAsync(ct) : null;
		List<long> people = new[] { lead.ReferredByCustomerId, lead.ShownByUserId, lead.CustomerId, lead.CreatedBy }.Where(x => x.HasValue).Select(x => x!.Value).ToList();
		List<CrmLeadActivity> acts = await db.Set<CrmLeadActivity>().AsNoTracking().Where(a => a.LeadId == leadId).OrderBy(a => a.HappenedUtc).ThenBy(a => a.ActivityId).ToListAsync(ct);
		people.AddRange(acts.Where(a => a.ByUserId.HasValue).Select(a => a.ByUserId!.Value));
		List<CrmFollowUp> follow = await db.Set<CrmFollowUp>().AsNoTracking().Where(f => f.LeadId == leadId).OrderBy(f => f.IsDone).ThenBy(f => f.DueUtc).ToListAsync(ct);
		people.AddRange(follow.Where(f => f.OwnerUserId.HasValue).Select(f => f.OwnerUserId!.Value));
		people = people.Distinct().ToList();
		Dictionary<long, string> names = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => people.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
		string N(long? id) => id.HasValue && names.TryGetValue(id.Value, out string? n) ? n : (id.HasValue ? "Unknown" : "System");
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		string sourceCode = await db.Set<CrmSource>().AsNoTracking().Where(x => x.SourceId == lead.SourceId).Select(x => x.Code).FirstOrDefaultAsync(ct) ?? "";
		CrmQuotationLinkDto? quotation = null;
		if (lead.QuotationId.HasValue)
		{
			quotation = await db.Quotations.AsNoTracking().IgnoreQueryFilters().Where(x => x.QuotationId == lead.QuotationId)
				.Select(x => new CrmQuotationLinkDto(x.QuotationId, x.QuotationNumber, x.Status.ToString(), x.TotalAmount, x.SentUtc)).FirstOrDefaultAsync(ct);
		}
		CrmJourneyDto? journey = lead.VisitorId.HasValue ? await JourneyAsync(lead, ct) : null;
		CrmLeadConsent? consent = await db.Set<CrmLeadConsent>().AsNoTracking().Where(c => c.LeadId == leadId).OrderByDescending(c => c.ConsentId).FirstOrDefaultAsync(ct);
		string retention = $"If this lead is lost, it is deleted {s.LostRetentionMonths} months after it closes.";
		CrmConsentDto? consentDto = consent == null ? null : new CrmConsentDto(consent.HowObtained, consent.ConsentTextShown, consent.ContactConsent, consent.MarketingConsent, consent.ConsentedUtc, retention);
		string channelName = lead.Channel switch
		{
			CrmChannel.WebForm => "Website enquiry form",
			CrmChannel.Callback => "Website callback request",
			CrmChannel.QuoteRequest => "Website \"Send me a quote\"",
			CrmChannel.WhatsApp => "WhatsApp (website button)",
			_ => "Entered by hand"
		};
		string entered = lead.Channel == CrmChannel.Manual ? $"{N(lead.CreatedBy)} on {IstClock.ToIst(lead.CreatedDate).ToString("d MMM", CultureInfo.InvariantCulture)}" : "the website";
		return new CrmLeadDetailDto(row, lead.LocationId, location, lead.NeededFrom, lead.BudgetText, lead.SourceDetail, lead.FirstMessage,
			lead.ReferredByCustomerId, lead.ReferredByCustomerId.HasValue ? N(lead.ReferredByCustomerId) : null, lead.WalkInUtc, lead.ShownByUserId, lead.ShownByUserId.HasValue ? N(lead.ShownByUserId) : null,
			lead.LostReasonId, lead.LostNote, lead.ConvertedUtc, lead.CustomerId.HasValue ? N(lead.CustomerId) : null,
			lead.TourUtc, lead.FirstContactUtc, lead.ResponseDueUtc, CrmEngine.TargetFor(lead.Channel, sourceCode, s),
			entered, channelName, quotation, journey, consentDto,
			acts.Select(a => new CrmActivityDto(a.ActivityId, (byte)a.Kind, KindName(a.Kind), a.Detail, a.HappenedUtc, a.IsSystem ? "System" : N(a.ByUserId), a.IsSystem)).ToList(),
			follow.Select(f => FollowDto(f, lead, row.LookingFor, N(f.OwnerUserId))).ToList());
	}

	internal CrmFollowUpDto FollowDto(CrmFollowUp f, CrmLead lead, string? lookingFor, string? ownerName) =>
		new CrmFollowUpDto(f.FollowUpId, lead.LeadId, lead.LeadNumber, lead.Name, lead.Company, lookingFor, f.DueUtc, f.WhatToDo, f.Kind, f.OwnerUserId, ownerName,
			f.IsDone, f.Outcome, f.DoneUtc, !f.IsDone && f.DueUtc < Now, !f.IsDone && f.DueUtc < Now ? (int)(Now - f.DueUtc).TotalMinutes : 0, false);

	public static string KindName(CrmActivityKind kind) => kind switch
	{
		CrmActivityKind.Enquiry => "Enquiry received",
		CrmActivityKind.Call => "Call logged",
		CrmActivityKind.Email => "Email sent",
		CrmActivityKind.WhatsApp => "WhatsApp sent",
		CrmActivityKind.Meeting => "Meeting",
		CrmActivityKind.Note => "Note added",
		CrmActivityKind.StatusChange => "Status changed",
		CrmActivityKind.Assignment => "Assigned",
		CrmActivityKind.Quotation => "Quotation",
		CrmActivityKind.Tour => "Tour booked",
		CrmActivityKind.AutoAck => "Auto-acknowledged",
		CrmActivityKind.FollowUpDone => "Follow-up done",
		CrmActivityKind.Conversion => "Converted",
		CrmActivityKind.Reopened => "Reopened",
		CrmActivityKind.Edited => "Details edited",
		_ => kind.ToString()
	};

	private async Task<CrmJourneyDto?> JourneyAsync(CrmLead lead, CancellationToken ct)
	{
		CrmVisitor? v = await db.Set<CrmVisitor>().AsNoTracking().FirstOrDefaultAsync(x => x.VisitorId == lead.VisitorId, ct);
		if (v == null)
		{
			return null;
		}
		List<CrmPageView> views = await db.Set<CrmPageView>().AsNoTracking().Where(p => p.VisitorId == v.VisitorId).OrderBy(p => p.ViewedUtc).Take(500).ToListAsync(ct);
		string? source = v.SourceId.HasValue ? await db.Set<CrmSource>().AsNoTracking().Where(x => x.SourceId == v.SourceId).Select(x => x.Name).FirstOrDefaultAsync(ct) : "Direct";
		List<CrmPageView> before = views.Where(p => p.ViewedUtc <= lead.CreatedDate.AddMinutes(1)).ToList();
		int visitsBefore = before.Select(p => p.VisitNumber).DefaultIfEmpty(1).Max();
		string? formPage = before.LastOrDefault()?.Path;
		return new CrmJourneyDto(v.VisitorId, "V-" + v.VisitorId, v.FirstSeenUtc, source, v.UtmMedium, v.UtmCampaign, v.Referrer, v.LandingPage, formPage, visitsBefore,
			Math.Round(Math.Max(0, (lead.CreatedDate - v.FirstSeenUtc).TotalDays), 1), v.DeviceType, v.Browser, v.City, lead.CreatedDate,
			views.Select(p => new CrmPageViewDto(p.Path, p.ViewedUtc, p.SecondsOnPage, p.VisitNumber)).ToList());
	}

	// ---- duplicate check -----------------------------------------------------------------------

	public async Task<CrmDuplicateResultDto> CheckDuplicateAsync(CrmCheckDuplicateRequest request, CancellationToken ct = default)
	{
		EnsureStaff();
		return await DuplicatesAsync(CrmText.NormaliseMobile(request.Mobile), CrmText.NormaliseEmail(request.Email), request.ExcludeLeadId, ct);
	}

	/// <summary>Leads and customers on the same last-10 mobile digits, or the same email ignoring capitals.
	/// Staff see every matching lead here (not only their own): the check exists to stop two owners calling one person.</summary>
	private async Task<CrmDuplicateResultDto> DuplicatesAsync(string mobileNorm, string? emailNorm, long? exclude, CancellationToken ct)
	{
		List<CrmDuplicateLeadDto> leads = new List<CrmDuplicateLeadDto>();
		List<CrmDuplicateCustomerDto> customers = new List<CrmDuplicateCustomerDto>();
		if (mobileNorm.Length != 10 && emailNorm == null)
		{
			return new CrmDuplicateResultDto(leads, customers);
		}
		List<CrmLead> found = await db.Set<CrmLead>().AsNoTracking()
			.Where(l => (mobileNorm.Length == 10 && l.MobileNormalised == mobileNorm) || (emailNorm != null && l.EmailNormalised == emailNorm))
			.Where(l => exclude == null || l.LeadId != exclude)
			.OrderByDescending(l => l.ClosedUtc == null).ThenByDescending(l => l.CreatedDate).Take(5).ToListAsync(ct);
		if (found.Count > 0)
		{
			List<CrmLeadRowDto> rows = await ShapeRowsAsync(found, ct);
			Dictionary<int, string> sources = await db.Set<CrmSource>().AsNoTracking().ToDictionaryAsync(s => s.SourceId, s => s.Name, ct);
			foreach (CrmLead l in found)
			{
				CrmLeadRowDto r = rows.First(x => x.LeadId == l.LeadId);
				string how = l.Channel == CrmChannel.Manual ? $"entered by hand ({sources.GetValueOrDefault(l.SourceId, "?")})" : "from a website enquiry";
				leads.Add(new CrmDuplicateLeadDto(l.LeadId, l.LeadNumber, l.Name, l.Company, l.CreatedDate, how, r.OwnerName, r.Status, r.IsClosed,
					mobileNorm.Length == 10 && l.MobileNormalised == mobileNorm ? "mobile" : "email"));
			}
		}
		foreach ((User u, string matched) in await engine.MatchingCustomersAsync(mobileNorm, emailNorm, ct))
		{
			customers.Add(new CrmDuplicateCustomerDto(u.UserId, u.FullName, u.CompanyName, u.MobileNumber, u.Email, matched));
		}
		return new CrmDuplicateResultDto(leads, customers);
	}

	public async Task<List<CrmDuplicateCustomerDto>> SearchCustomersAsync(string? search, CancellationToken ct = default)
	{
		EnsureStaff();
		if (string.IsNullOrWhiteSpace(search) || search.Trim().Length < 2)
		{
			return new List<CrmDuplicateCustomerDto>();
		}
		string term = search.Trim();
		return await db.Users.AsNoTracking().Where(u => u.UserType == UserType.Customer && (u.FullName.Contains(term) || u.MobileNumber.Contains(term) || (u.CompanyName != null && u.CompanyName.Contains(term))))
			.OrderBy(u => u.FullName).Take(15)
			.Select(u => new CrmDuplicateCustomerDto(u.UserId, u.FullName, u.CompanyName, u.MobileNumber, null, "search")).ToListAsync(ct);
	}

	// ---- create --------------------------------------------------------------------------------

	public async Task<(CrmLeadDetailDto? Lead, CrmDuplicateResultDto? Duplicates)> CreateLeadAsync(CrmCreateLeadRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmSource source = r.SourceId.HasValue ? await db.Set<CrmSource>().AsNoTracking().FirstOrDefaultAsync(s => s.SourceId == r.SourceId && s.IsActive, ct) ?? throw new BusinessRuleException("Choose how they reached us.") : throw new BusinessRuleException("Choose how they reached us.");
		string name = CrmText.Clip(r.Name, 150) ?? throw new BusinessRuleException("Enter their name.");
		if (string.IsNullOrWhiteSpace(r.Mobile))
		{
			throw new BusinessRuleException("Enter a mobile number — it is how we find them again.");
		}
		if (!CrmText.IsValidMobile(r.Mobile))
		{
			throw new BusinessRuleException("That does not look like a mobile number. Ten digits, with or without +91.");
		}
		string? email = CrmText.Clip(r.Email, 256);
		if (email != null && !CrmText.IsValidEmail(email))
		{
			throw new BusinessRuleException("Check the email address.");
		}
		ValidateFollowUp(r.FollowUpUtc, r.FollowUpWhat, required: false);
		if (r.PeopleCount is < 0 or > 10000)
		{
			throw new BusinessRuleException("Enter how many people as a number.");
		}
		string mobileNorm = CrmText.NormaliseMobile(r.Mobile);
		string? emailNorm = CrmText.NormaliseEmail(email);
		if (!r.AllowDuplicate)
		{
			CrmDuplicateResultDto dup = await DuplicatesAsync(mobileNorm, emailNorm, null, ct);
			if (dup.Leads.Count > 0 || dup.Customers.Count > 0)
			{
				return (null, dup);
			}
		}
		CrmWhatsAppRef? waRef = null;
		if (!string.IsNullOrWhiteSpace(r.WhatsAppRef))
		{
			string code = r.WhatsAppRef.Trim().ToUpperInvariant();
			if (!code.StartsWith("W-", StringComparison.Ordinal))
			{
				code = "W-" + code;
			}
			waRef = await db.Set<CrmWhatsAppRef>().FirstOrDefaultAsync(x => x.Code == code, ct) ?? throw new BusinessRuleException("That WhatsApp reference was not found. Check the code in their message, or leave it blank.");
		}
		long? assignTo = null;
		bool useRules = false;
		string assign = (r.AssignTo ?? "me").Trim().ToLowerInvariant();
		if (assign == "rules")
		{
			useRules = true;
		}
		else if (assign == "me" || assign == "")
		{
			assignTo = Me;
		}
		else if (long.TryParse(assign, out long uid))
		{
			if (uid != Me && !currentUser.HasPermission("CRM.LEAD.ASSIGN"))
			{
				throw new ForbiddenException("Only a manager can give a lead to someone else. Choose Me or Let the rules decide.");
			}
			if (!(await engine.CrmStaffAsync(ct)).Any(s => s.UserId == uid))
			{
				throw new BusinessRuleException("That person does not handle leads. Pick someone from the list.");
			}
			assignTo = uid;
		}
		bool spokeAlready = source.Code is "PHONE" or "WALKIN";
		CrmStatus status = await engine.StatusByCodeAsync(spokeAlready ? "CONTACTED" : "NEW", ct);
		string? heard = CrmText.Clip(r.HeardFrom, 100);
		CrmLead lead = new CrmLead
		{
			Name = name,
			Company = CrmText.Clip(r.Company, 150),
			Mobile = r.Mobile.Trim(),
			MobileNormalised = mobileNorm,
			Email = email,
			EmailNormalised = emailNorm,
			LookingForId = r.LookingForId,
			PeopleCount = r.PeopleCount,
			BudgetText = CrmText.Clip(r.BudgetText, 100),
			LocationId = r.LocationId,
			NeededFrom = r.NeededFrom,
			StatusId = status.StatusId,
			SourceId = source.SourceId,
			SourceDetail = heard == null ? null : "Heard of us: " + heard,
			Channel = waRef != null ? CrmChannel.WhatsApp : CrmChannel.Manual,
			WalkInUtc = source.Code == "WALKIN" ? (r.WalkInUtc ?? Now) : null,
			ShownByUserId = source.Code == "WALKIN" ? r.ShownByUserId : null,
			ReferredByCustomerId = source.Code == "REFERRAL" ? r.ReferredByCustomerId : null,
			FirstMessage = CrmText.Clip(r.WhatTheySaid, 2000),
			FirstContactUtc = spokeAlready ? Now : null,
			DealValue = r.DealValue is > 0 ? r.DealValue : null,
			VisitorId = waRef?.VisitorId
		};
		await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
		{
			await using var tx = await db.Database.BeginTransactionAsync(ct);
			lead.LeadNumber = await engine.NextLeadNumberAsync(ct);
			db.Set<CrmLead>().Add(lead);
			await db.SaveChangesAsync(ct);
			string by = currentUser.FullName ?? "staff";
			string first = source.Code switch
			{
				"PHONE" => $"Rang the office. Entered by {by} during the call.",
				"WALKIN" => $"Walked in{(lead.ShownByUserId.HasValue ? ", shown around by " + await engine.UserNameAsync(lead.ShownByUserId, ct) : "")}. Entered by {by}.",
				"REFERRAL" => $"Referred{(lead.ReferredByCustomerId.HasValue ? " by " + await engine.UserNameAsync(lead.ReferredByCustomerId, ct) : "")}. Entered by {by}.",
				_ => $"{source.Name}. Entered by {by}."
			};
			engine.Log(lead.LeadId, CrmActivityKind.Enquiry, first + (lead.FirstMessage == null ? "" : " They said: “" + lead.FirstMessage + "”"), Me);
			if (spokeAlready)
			{
				engine.Log(lead.LeadId, source.Code == "PHONE" ? CrmActivityKind.Call : CrmActivityKind.Meeting,
					source.Code == "PHONE" ? "Spoke to them when they rang (this call)." : "Spoke to them in person at the front desk.", Me);
			}
			db.Set<CrmLeadConsent>().Add(new CrmLeadConsent
			{
				LeadId = lead.LeadId,
				HowObtained = $"Entered by {by} after {(source.Code switch { "PHONE" => "a phone call", "WALKIN" => "a walk-in visit", "EMAIL" => "an email", "WHATSAPP" => "a WhatsApp message", "REFERRAL" => "a referral", "EVENT" => "an event", _ => "contact" })}. No consent form was shown, so nothing automatic is sent to them.",
				ContactConsent = false,
				MarketingConsent = false
			});
			if (waRef != null)
			{
				waRef.LeadId = lead.LeadId;
				CrmVisitor? visitor = await db.Set<CrmVisitor>().FirstOrDefaultAsync(v => v.VisitorId == waRef.VisitorId, ct);
				if (visitor != null)
				{
					visitor.LeadId = lead.LeadId;
					visitor.DeleteAfterUtc = null;
					// The lead came through the website: its source is where that visit came from.
					lead.SourceId = visitor.SourceId ?? await engine.SourceIdByCodeAsync("DIRECT", ct);
					lead.SourceDetail = CrmText.Clip($"WhatsApp button {waRef.Code} on {waRef.PagePath ?? "the site"}" + (heard == null ? "" : "; heard of us: " + heard), 300);
				}
			}
			await engine.AfterCreateAsync(lead, source.Code, assignTo, useRules, Me, ct);
			if (r.FollowUpUtc.HasValue && !string.IsNullOrWhiteSpace(r.FollowUpWhat))
			{
				db.Set<CrmFollowUp>().Add(new CrmFollowUp
				{
					LeadId = lead.LeadId,
					DueUtc = r.FollowUpUtc.Value,
					WhatToDo = CrmText.Clip(r.FollowUpWhat, 500)!,
					Kind = source.Code == "EMAIL" ? "email" : "call",
					OwnerUserId = lead.OwnerUserId
				});
			}
			await db.SaveChangesAsync(ct);
			if (useRules && lead.OwnerUserId != Me)
			{
				string sourceName = source.Name;
				await engine.AnnounceNewLeadAsync(lead, sourceName, ct);
			}
			else if (lead.OwnerUserId.HasValue && lead.OwnerUserId != Me)
			{
				engine.InApp(lead.LeadId, "lead.assigned", lead.OwnerUserId.Value, $"Lead {lead.LeadNumber} was given to you", $"{lead.Name} · by {currentUser.FullName}");
			}
			await db.SaveChangesAsync(ct);
			await tx.CommitAsync(ct);
		});
		logger.LogInformation("CRM lead {Lead} created by {UserId} (source {Source})", lead.LeadNumber, Me, source.Code);
		return (await GetLeadAsync(lead.LeadId, ct), null);
	}

	private void ValidateFollowUp(DateTime? due, string? what, bool required)
	{
		if (!due.HasValue)
		{
			if (required)
			{
				throw new BusinessRuleException("Set the date and time of the follow-up.");
			}
			return;
		}
		if (due.Value < Now.AddMinutes(-5))
		{
			throw new BusinessRuleException("The follow-up cannot be in the past.");
		}
		if (string.IsNullOrWhiteSpace(what))
		{
			throw new BusinessRuleException("Say what needs to be done, so whoever picks it up knows.");
		}
	}

	// ---- edit ----------------------------------------------------------------------------------

	public async Task<CrmLeadDetailDto> UpdateLeadAsync(long leadId, CrmUpdateLeadRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		await EnsureOpenAsync(lead, ct);
		string name = CrmText.Clip(r.Name, 150) ?? throw new BusinessRuleException("Enter their name.");
		if (string.IsNullOrWhiteSpace(r.Mobile))
		{
			throw new BusinessRuleException("Enter a mobile number — it is how we find them again.");
		}
		if (!CrmText.IsValidMobile(r.Mobile))
		{
			throw new BusinessRuleException("That does not look like a mobile number. Ten digits, with or without +91.");
		}
		string? email = CrmText.Clip(r.Email, 256);
		if (email != null && !CrmText.IsValidEmail(email))
		{
			throw new BusinessRuleException("Check the email address.");
		}
		List<string> changed = new List<string>();
		void Track<T>(string label, T oldValue, T newValue)
		{
			if (!EqualityComparer<T>.Default.Equals(oldValue, newValue))
			{
				changed.Add(label);
			}
		}
		Track("name", lead.Name, name);
		Track("mobile", lead.Mobile, r.Mobile.Trim());
		Track("email", lead.Email, email);
		Track("company", lead.Company, CrmText.Clip(r.Company, 150));
		Track("looking for", lead.LookingForId, r.LookingForId);
		Track("people", lead.PeopleCount, r.PeopleCount);
		Track("budget", lead.BudgetText, CrmText.Clip(r.BudgetText, 100));
		Track("location", lead.LocationId, r.LocationId);
		Track("needed from", lead.NeededFrom, r.NeededFrom);
		Track("deal value", lead.DealValue, r.DealValue is > 0 ? r.DealValue : null);
		lead.Name = name;
		lead.Mobile = r.Mobile.Trim();
		lead.MobileNormalised = CrmText.NormaliseMobile(r.Mobile);
		lead.Email = email;
		lead.EmailNormalised = CrmText.NormaliseEmail(email);
		lead.Company = CrmText.Clip(r.Company, 150);
		lead.LookingForId = r.LookingForId;
		lead.PeopleCount = r.PeopleCount;
		lead.BudgetText = CrmText.Clip(r.BudgetText, 100);
		lead.LocationId = r.LocationId;
		lead.NeededFrom = r.NeededFrom;
		lead.DealValue = r.DealValue is > 0 ? r.DealValue : null;
		if (changed.Count > 0)
		{
			engine.Log(lead.LeadId, CrmActivityKind.Edited, "Changed " + string.Join(", ", changed) + ".", Me);
		}
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(leadId, ct);
	}

	private async Task EnsureOpenAsync(CrmLead lead, CancellationToken ct)
	{
		if (lead.ClosedUtc.HasValue)
		{
			bool won = await db.Set<CrmStatus>().AsNoTracking().AnyAsync(s => s.StatusId == lead.StatusId && s.IsWon, ct);
			throw new BusinessRuleException(won && lead.CustomerId.HasValue
				? $"This lead was converted on {IstClock.ToIst(lead.ConvertedUtc ?? lead.ClosedUtc.Value).ToString("d MMM", CultureInfo.InvariantCulture)} and is now customer #{lead.CustomerId}."
				: "This lead is closed. Reopen it first — reopening is recorded.", "LEAD_CLOSED");
		}
	}
}
