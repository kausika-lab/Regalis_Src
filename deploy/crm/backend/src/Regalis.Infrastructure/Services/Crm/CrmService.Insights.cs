using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Crm;
using Regalis.Domain.Entities.Crm;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Excel;
using Regalis.Shared;
using Regalis.Shared.Api;

namespace Regalis.Infrastructure.Services.Crm;

public partial class CrmService
{
	// ---- website visitors (section 7) ---------------------------------------------------------

	public async Task<CrmVisitorListDto> ListVisitorsAsync(CrmVisitorListRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		int days = r.Days is > 0 and <= 366 ? r.Days.Value : 7;
		DateTime since = Now.AddDays(-days);
		Dictionary<int, string> sourceNames = await db.Set<CrmSource>().AsNoTracking().ToDictionaryAsync(s => s.SourceId, s => s.Name, ct);
		// Visits = distinct (visitor, visit number) with a page view in the period.
		var visitsInPeriod = await (from p in db.Set<CrmPageView>().AsNoTracking()
			join v in db.Set<CrmVisitor>().AsNoTracking() on p.VisitorId equals v.VisitorId
			where p.ViewedUtc >= since
			group p by new { p.VisitorId, p.VisitNumber, v.SourceId } into g
			select new { g.Key.VisitorId, g.Key.VisitNumber, g.Key.SourceId }).ToListAsync(ct);
		int visits = visitsInPeriod.Count;
		int devices = visitsInPeriod.Select(x => x.VisitorId).Distinct().Count();
		int returning = visitsInPeriod.Where(x => x.VisitNumber > 1).Select(x => x.VisitorId).Distinct().Count();
		var webLeads = await db.Set<CrmLead>().AsNoTracking().Where(l => l.CreatedDate >= since && l.VisitorId != null)
			.Select(l => new { l.LeadId, l.VisitorId }).ToListAsync(ct);
		List<long> leadVisitorIds = webLeads.Select(l => l.VisitorId!.Value).ToList();
		Dictionary<long, int?> leadVisitorSource = await db.Set<CrmVisitor>().AsNoTracking().Where(v => leadVisitorIds.Contains(v.VisitorId)).ToDictionaryAsync(v => v.VisitorId, v => v.SourceId, ct);
		var top = await db.Set<CrmPageView>().AsNoTracking().Where(p => p.ViewedUtc >= since).GroupBy(p => p.Path)
			.Select(g => new { Path = g.Key, Views = g.Count() }).OrderByDescending(g => g.Views).FirstOrDefaultAsync(ct);
		CrmVisitorSummaryDto summary = new CrmVisitorSummaryDto(visits, devices, webLeads.Count, visits == 0 ? null : Math.Round(100m * webLeads.Count / visits, 1),
			top?.Path, top?.Views ?? 0, returning);
		List<CrmSourceVisitsDto> sources = visitsInPeriod.GroupBy(x => x.SourceId)
			.Select(g => new CrmSourceVisitsDto(g.Key, g.Key.HasValue && sourceNames.TryGetValue(g.Key.Value, out string? n) ? n : "Direct", g.Count(),
				webLeads.Count(l => leadVisitorSource.TryGetValue(l.VisitorId!.Value, out int? sid) && sid == g.Key)))
			.OrderByDescending(s => s.Visits).ToList();
		IQueryable<CrmVisitor> q = db.Set<CrmVisitor>().AsNoTracking().Where(v => v.LastSeenUtc >= since);
		if (r.SourceId.HasValue)
		{
			q = r.SourceId == 0 ? q.Where(v => v.SourceId == null) : q.Where(v => v.SourceId == r.SourceId);
		}
		if (string.Equals(r.Enquired, "yes", StringComparison.OrdinalIgnoreCase))
		{
			q = q.Where(v => v.LeadId != null);
		}
		else if (string.Equals(r.Enquired, "no", StringComparison.OrdinalIgnoreCase))
		{
			q = q.Where(v => v.LeadId == null);
		}
		int pageSize = Math.Clamp(r.PageSize, 1, 200);
		int page = Math.Max(1, r.Page);
		int total = await q.CountAsync(ct);
		List<CrmVisitor> rows = await q.OrderByDescending(v => v.LastSeenUtc).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
		List<CrmVisitorRowDto> shaped = await ShapeVisitorsAsync(rows, sourceNames, ct);
		return new CrmVisitorListDto(summary, sources, PagedResult<CrmVisitorRowDto>.Create(shaped, page, pageSize, total));
	}

	private async Task<List<CrmVisitorRowDto>> ShapeVisitorsAsync(List<CrmVisitor> rows, Dictionary<int, string> sourceNames, CancellationToken ct)
	{
		List<long> leadIds = rows.Where(v => v.LeadId.HasValue).Select(v => v.LeadId!.Value).ToList();
		IQueryable<CrmLead> visible = Visible();
		Dictionary<long, string> leadNames = await visible.Where(l => leadIds.Contains(l.LeadId)).ToDictionaryAsync(l => l.LeadId, l => l.Name, ct);
		return rows.Select(v =>
		{
			string came = v.SourceId.HasValue && sourceNames.TryGetValue(v.SourceId.Value, out string? n) ? n : "Direct";
			if (!string.IsNullOrWhiteSpace(v.UtmMedium))
			{
				came += " · " + v.UtmMedium;
			}
			else if (came is "Referral" or "Other website" && !string.IsNullOrWhiteSpace(v.Referrer))
			{
				came = HostOf(v.Referrer) ?? came;
			}
			string? leadName = v.LeadId.HasValue ? (leadNames.TryGetValue(v.LeadId.Value, out string? ln) ? ln : "a lead you cannot open") : null;
			return new CrmVisitorRowDto(v.VisitorId, "V-" + v.VisitorId, v.FirstSeenUtc, v.LastSeenUtc, came, v.LandingPage, v.PageViewCount, v.City, v.DeviceType,
				v.VisitCount, v.LeadId, leadName, v.LeadId == null && v.VisitCount >= 3);
		}).ToList();
	}

	internal static string? HostOf(string? url)
	{
		return Uri.TryCreate(url, UriKind.Absolute, out Uri? u) ? u.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? u.Host.Substring(4) : u.Host : null;
	}

	public async Task<CrmVisitorDetailDto> GetVisitorAsync(long visitorId, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmVisitor v = await db.Set<CrmVisitor>().AsNoTracking().FirstOrDefaultAsync(x => x.VisitorId == visitorId, ct) ?? throw new NotFoundException("Visitor", visitorId);
		Dictionary<int, string> sourceNames = await db.Set<CrmSource>().AsNoTracking().ToDictionaryAsync(s => s.SourceId, s => s.Name, ct);
		CrmVisitorRowDto row = (await ShapeVisitorsAsync(new List<CrmVisitor> { v }, sourceNames, ct))[0];
		List<CrmPageViewDto> views = await db.Set<CrmPageView>().AsNoTracking().Where(p => p.VisitorId == visitorId).OrderBy(p => p.ViewedUtc).Take(500)
			.Select(p => new CrmPageViewDto(p.Path, p.ViewedUtc, p.SecondsOnPage, p.VisitNumber)).ToListAsync(ct);
		return new CrmVisitorDetailDto(row, v.UtmSource, v.UtmMedium, v.UtmCampaign, v.Referrer, v.Browser, views);
	}

	// ---- reports (section 10) ------------------------------------------------------------------

	private (DateOnly From, DateOnly To, DateTime FromUtc, DateTime ToUtc) Range(CrmReportRequest r)
	{
		DateOnly today = IstClock.IstDateOf(Now);
		DateOnly to = r.To ?? today;
		DateOnly from = r.From ?? to.AddDays(-89);
		if (from > to)
		{
			throw new BusinessRuleException("The start date must be on or before the end date.");
		}
		return (from, to, IstClock.ToUtc(from.ToDateTime(TimeOnly.MinValue)), IstClock.ToUtc(to.AddDays(1).ToDateTime(TimeOnly.MinValue)));
	}

	/// <summary>Reports count every lead (not only the caller's): CRM.REPORTS.VIEW is a manager permission.</summary>
	private Task<List<CrmLead>> LeadsCreatedAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct) =>
		db.Set<CrmLead>().AsNoTracking().Where(l => l.CreatedDate >= fromUtc && l.CreatedDate < toUtc).ToListAsync(ct);

	public async Task<CrmReportDto<CrmSourceReportRow>> SourceReportAsync(CrmReportRequest request, CancellationToken ct = default)
	{
		EnsureStaff();
		var range = Range(request);
		List<CrmLead> leads = await LeadsCreatedAsync(range.FromUtc, range.ToUtc, ct);
		Dictionary<int, string> sources = await db.Set<CrmSource>().AsNoTracking().ToDictionaryAsync(s => s.SourceId, s => s.Name, ct);
		// Revenue won: what the converted customers have been invoiced since conversion (cancelled invoices excluded).
		List<long> customerIds = leads.Where(l => l.CustomerId.HasValue && l.ConvertedUtc.HasValue).Select(l => l.CustomerId!.Value).Distinct().ToList();
		List<(long CustomerId, DateOnly Date, decimal Total)> invoices = customerIds.Count == 0 ? new List<(long CustomerId, DateOnly Date, decimal Total)>() :
			(await db.Invoices.AsNoTracking().Where(i => customerIds.Contains(i.CustomerId) && i.Status != InvoiceStatus.Cancelled)
				.Select(i => new { i.CustomerId, i.InvoiceDate, i.TotalAmount }).ToListAsync(ct)).Select(i => (i.CustomerId, i.InvoiceDate, i.TotalAmount)).ToList();
		decimal RevenueOf(CrmLead l)
		{
			if (!l.CustomerId.HasValue || !l.ConvertedUtc.HasValue)
			{
				return 0m;
			}
			DateOnly converted = IstClock.IstDateOf(l.ConvertedUtc.Value);
			decimal invoiced = invoices.Where(i => i.CustomerId == l.CustomerId && i.Date >= converted).Sum(i => i.Total);
			return invoiced > 0 ? invoiced : l.DealValue ?? 0m;
		}
		List<CrmSourceReportRow> rows = leads.GroupBy(l => l.SourceId).Select(g =>
		{
			int n = g.Count();
			int won = g.Count(l => l.ConvertedUtc.HasValue);
			decimal conv = n == 0 ? 0 : Math.Round(100m * won / n, 1);
			decimal revenue = g.Where(l => l.ConvertedUtc.HasValue).Sum(RevenueOf);
			return new CrmSourceReportRow(sources.GetValueOrDefault(g.Key, "?"), n, won, conv, revenue, "");
		}).OrderByDescending(x => x.RevenueWon).ThenByDescending(x => x.Leads).ToList();
		decimal totalLeads = Math.Max(1, rows.Sum(x => x.Leads));
		decimal avgConv = rows.Sum(x => x.Leads) == 0 ? 0 : 100m * rows.Sum(x => x.Won) / rows.Sum(x => x.Leads);
		rows = rows.Select(x => x with { Advice = Advice(x, totalLeads, avgConv) }).ToList();
		return new CrmReportDto<CrmSourceReportRow>(range.From, range.To, rows);
	}

	private static string Advice(CrmSourceReportRow r, decimal totalLeads, decimal avgConv)
	{
		decimal share = 100m * r.Leads / totalLeads;
		if (r.Leads < 3)
		{
			return "Too few leads to judge yet.";
		}
		if (r.Won == 0)
		{
			return share >= 20 ? "Many leads, no wins — check what these people want before spending more here." : "No wins yet — watch it before spending more.";
		}
		if (r.Conversion >= avgConv * 1.5m)
		{
			return "Converts well above average — worth more effort.";
		}
		if (r.Conversion <= avgConv * 0.5m && share >= 20)
		{
			return "Brings volume but converts poorly — look at the quality, not the count.";
		}
		return "Steady — keep as it is.";
	}

	public async Task<CrmReportDto<CrmFunnelRow>> FunnelReportAsync(CrmReportRequest request, CancellationToken ct = default)
	{
		EnsureStaff();
		var range = Range(request);
		int visits = await db.Set<CrmPageView>().AsNoTracking().Where(p => p.ViewedUtc >= range.FromUtc && p.ViewedUtc < range.ToUtc)
			.Select(p => new { p.VisitorId, p.VisitNumber }).Distinct().CountAsync(ct);
		List<CrmLead> web = (await LeadsCreatedAsync(range.FromUtc, range.ToUtc, ct)).Where(l => l.Channel != CrmChannel.Manual || l.VisitorId != null).ToList();
		int visitStage = (await engine.StatusByCodeAsync("VISIT", ct)).SortOrder;
		Dictionary<int, int> order = await db.Set<CrmStatus>().AsNoTracking().ToDictionaryAsync(s => s.StatusId, s => s.SortOrder, ct);
		int enquired = web.Count;
		int replied = web.Count(l => l.FirstContactUtc.HasValue);
		int toured = web.Count(l => l.TourUtc.HasValue || (order.GetValueOrDefault(l.StatusId) >= visitStage && !l.LostReasonId.HasValue && l.ClosedUtc == null) || l.ConvertedUtc.HasValue);
		int quoted = web.Count(l => l.QuotationId.HasValue);
		int won = web.Count(l => l.ConvertedUtc.HasValue);
		List<(string Step, string Basis, int Count)> steps = new()
		{
			("Website visits", "Visitor sessions", visits),
			("Enquired", "Leads created from the website", enquired),
			("We replied", "Leads with at least one logged contact", replied),
			("Came to see it", "Leads that reached Visit booked", toured),
			("Got a quotation", "Leads with a linked quotation", quoted),
			("Became a customer", "Leads closed as Won", won)
		};
		List<CrmFunnelRow> rows = new List<CrmFunnelRow>();
		for (int i = 0; i < steps.Count; i++)
		{
			int prev = i == 0 ? 0 : steps[i - 1].Count;
			decimal? lost = i == 0 || prev == 0 ? null : Math.Round(100m * (prev - steps[i].Count) / prev, 1);
			rows.Add(new CrmFunnelRow(steps[i].Step, steps[i].Basis, steps[i].Count, lost));
		}
		return new CrmReportDto<CrmFunnelRow>(range.From, range.To, rows);
	}

	public async Task<CrmReportDto<CrmResponseRow>> ResponseReportAsync(CrmReportRequest request, CancellationToken ct = default)
	{
		EnsureStaff();
		var range = Range(request);
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		// Only leads that had a first-reply clock (website, callback, WhatsApp, email); walk-ins and calls were answered on the spot.
		List<CrmLead> leads = (await LeadsCreatedAsync(range.FromUtc, range.ToUtc, ct)).Where(l => l.ResponseDueUtc.HasValue).ToList();
		string Band(CrmLead l)
		{
			if (!l.FirstContactUtc.HasValue)
			{
				return "Never replied";
			}
			int m = s.WorkingMinutesBetween(l.CreatedDate, l.FirstContactUtc.Value);
			return m < 30 ? "Under 30 minutes" : m < 240 ? "30 min – 4 hours" : m < 1440 ? "4 – 24 hours" : "Over 24 hours";
		}
		string[] bands = { "Under 30 minutes", "30 min – 4 hours", "4 – 24 hours", "Over 24 hours", "Never replied" };
		List<CrmResponseRow> rows = bands.Select(b =>
		{
			List<CrmLead> g = leads.Where(l => Band(l) == b).ToList();
			int won = g.Count(l => l.ConvertedUtc.HasValue);
			return new CrmResponseRow(b, g.Count, won, g.Count == 0 ? 0 : Math.Round(100m * won / g.Count, 1));
		}).ToList();
		return new CrmReportDto<CrmResponseRow>(range.From, range.To, rows);
	}

	public async Task<CrmReportDto<CrmOwnerRow>> OwnerReportAsync(CrmReportRequest request, CancellationToken ct = default)
	{
		EnsureStaff();
		var range = Range(request);
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		List<CrmLead> leads = await LeadsCreatedAsync(range.FromUtc, range.ToUtc, ct);
		List<long> owners = leads.Where(l => l.OwnerUserId.HasValue).Select(l => l.OwnerUserId!.Value).Distinct().ToList();
		Dictionary<long, string> names = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => owners.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
		Dictionary<string, int> codes = await db.Set<CrmSource>().AsNoTracking().ToDictionaryAsync(x => x.Code, x => x.SourceId, ct);
		List<CrmOwnerRow> rows = leads.GroupBy(l => l.OwnerUserId).Select(g =>
		{
			List<CrmLead> clocked = g.Where(l => l.ResponseDueUtc.HasValue).ToList();
			int inTime = clocked.Count(l => l.FirstContactUtc.HasValue && l.FirstContactUtc <= l.ResponseDueUtc);
			List<double> decisions = g.Where(l => l.ClosedUtc.HasValue && l.FirstContactUtc.HasValue).Select(l => (l.ClosedUtc!.Value - l.FirstContactUtc!.Value).TotalDays).ToList();
			return new CrmOwnerRow(g.Key, g.Key.HasValue ? names.GetValueOrDefault(g.Key.Value, "Unknown") : "Unassigned", g.Count(), inTime,
				clocked.Count == 0 ? null : Math.Round(100m * inTime / clocked.Count, 1), g.Count(l => l.TourUtc.HasValue), g.Count(l => l.ConvertedUtc.HasValue),
				g.Count(l => l.LostReasonId.HasValue), decisions.Count == 0 ? null : Math.Round(decisions.Average(), 1));
		}).OrderBy(r => r.UserId == null).ThenBy(r => r.Owner).ToList();
		return new CrmReportDto<CrmOwnerRow>(range.From, range.To, rows);
	}

	public async Task<byte[]> ExportReportsAsync(CrmReportRequest request, CancellationToken ct = default)
	{
		var sources = await SourceReportAsync(request, ct);
		var funnel = await FunnelReportAsync(request, ct);
		var response = await ResponseReportAsync(request, ct);
		var owners = await OwnerReportAsync(request, ct);
		using XLWorkbook wb = new XLWorkbook();
		ExcelWorkbookHelper.AddSheet(wb, "Sources", new List<ExcelColumn<CrmSourceReportRow>>
		{
			new("Source", r => r.Source), new("Leads", r => r.Leads, ExcelCellKind.Number), new("Won", r => r.Won, ExcelCellKind.Number),
			new("Conversion %", r => r.Conversion, ExcelCellKind.Number), new("Revenue won", r => r.RevenueWon, ExcelCellKind.Money), new("What to do", r => r.Advice)
		}, sources.Rows);
		ExcelWorkbookHelper.AddSheet(wb, "Funnel", new List<ExcelColumn<CrmFunnelRow>>
		{
			new("Step", r => r.Step), new("Where the number comes from", r => r.Basis), new("Count", r => r.Count, ExcelCellKind.Number), new("Lost at this step %", r => r.LostPercent, ExcelCellKind.Number)
		}, funnel.Rows);
		ExcelWorkbookHelper.AddSheet(wb, "Response time", new List<ExcelColumn<CrmResponseRow>>
		{
			new("Replied within", r => r.Band), new("Leads", r => r.Leads, ExcelCellKind.Number), new("Won", r => r.Won, ExcelCellKind.Number), new("Won %", r => r.WonPercent, ExcelCellKind.Number)
		}, response.Rows);
		ExcelWorkbookHelper.AddSheet(wb, "Owners", new List<ExcelColumn<CrmOwnerRow>>
		{
			new("Owner", r => r.Owner), new("Leads held", r => r.Leads, ExcelCellKind.Number), new("Replied in time", r => r.RepliedInTime, ExcelCellKind.Number),
			new("Replied in time %", r => r.RepliedInTimePercent, ExcelCellKind.Number), new("Visits arranged", r => r.Visits, ExcelCellKind.Number),
			new("Won", r => r.Won, ExcelCellKind.Number), new("Lost", r => r.Lost, ExcelCellKind.Number), new("Avg days to decision", r => r.AvgDaysToDecision, ExcelCellKind.Number)
		}, owners.Rows);
		using System.IO.MemoryStream ms = new System.IO.MemoryStream();
		wb.SaveAs(ms);
		return ms.ToArray();
	}
}
