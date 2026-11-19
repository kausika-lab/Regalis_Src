using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regalis.Domain.Entities.Crm;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.Infrastructure.Services.Crm;
using Regalis.Shared;

namespace Regalis.Infrastructure.BackgroundJobs;

/// <summary>
/// CRM background work, once a minute (document sections 8.3, 8.5, 9.1 and 17.1):
/// response nudges (owner at the target, manager at the escalation time, working hours only),
/// follow-up reminders, the 09:00 working-day digest, quotations linked by their enquiry
/// reference, and the retention clean-up (anonymous visits, lost leads).
/// </summary>
public class CrmJobs(RegalisDbContext db, CrmEngine engine, ILogger<CrmJobs> logger)
{
	private static DateTime _lastRetentionUtc = DateTime.MinValue;

	public async Task RunAsync(CancellationToken ct)
	{
		await ResponseClockAsync(ct);
		await FollowUpRemindersAsync(ct);
		await LinkQuotationsAsync(ct);
		await DigestAsync(ct);
		await RetentionAsync(ct);
	}

	private async Task ResponseClockAsync(CancellationToken ct)
	{
		DateTime now = engine.Now;
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		List<CrmLead> waiting = await db.Set<CrmLead>()
			.Where(l => l.ClosedUtc == null && l.FirstContactUtc == null && l.ResponseDueUtc != null && l.ResponseDueUtc <= now && l.ManagerEscalatedUtc == null)
			.Take(200).ToListAsync(ct);
		if (waiting.Count == 0)
		{
			return;
		}
		List<long> managers = await engine.ManagersAsync(ct);
		foreach (CrmLead lead in waiting)
		{
			int late = s.WorkingMinutesBetween(lead.CreatedDate, now);
			if (lead.OwnerNudgedUtc == null && lead.OwnerUserId.HasValue)
			{
				engine.InApp(lead.LeadId, "response.nudge", lead.OwnerUserId.Value, $"{lead.LeadNumber} is waiting for a first reply",
					$"{lead.Name} has waited {CrmText.Minutes(late)} (working time). Call them now.");
			}
			lead.OwnerNudgedUtc ??= now;
			if (late >= s.Escalate)
			{
				string owner = lead.OwnerUserId.HasValue ? await engine.UserNameAsync(lead.OwnerUserId, ct) : "nobody";
				foreach (long m in managers)
				{
					engine.InApp(lead.LeadId, "response.escalated", m, $"{lead.LeadNumber}: no reply after {CrmText.Minutes(late)}", $"{lead.Name} · owner: {owner}.");
					await engine.EmailAsync(lead.LeadId, "response.escalated", m, null, $"No reply yet: lead {lead.LeadNumber} ({lead.Name})",
						$"<p>Lead <b>{CrmText.Html(lead.LeadNumber)}</b> ({CrmText.Html(lead.Name)}) has had no reply after {CrmText.Minutes(late)} of working time.</p><p>Owner: {CrmText.Html(owner)}.</p>", ct);
				}
				lead.ManagerEscalatedUtc = now;
				engine.Log(lead.LeadId, CrmActivityKind.Note, $"No first reply after {CrmText.Minutes(late)} of working time — the manager was told.", null, true);
			}
		}
		await db.SaveChangesAsync(ct);
	}

	private async Task FollowUpRemindersAsync(CancellationToken ct)
	{
		DateTime now = engine.Now;
		List<CrmFollowUp> due = await db.Set<CrmFollowUp>().Where(f => !f.IsDone && f.RemindedUtc == null && f.DueUtc <= now && f.OwnerUserId != null).Take(200).ToListAsync(ct);
		if (due.Count == 0)
		{
			return;
		}
		List<long> ids = due.Select(f => f.LeadId).Distinct().ToList();
		Dictionary<long, CrmLead> leads = await db.Set<CrmLead>().AsNoTracking().Where(l => ids.Contains(l.LeadId)).ToDictionaryAsync(l => l.LeadId, ct);
		foreach (CrmFollowUp f in due)
		{
			if (leads.TryGetValue(f.LeadId, out CrmLead? lead) && lead.ClosedUtc == null)
			{
				engine.InApp(lead.LeadId, "followup.due", f.OwnerUserId!.Value, $"Follow-up due: {lead.Name} ({lead.LeadNumber})", f.WhatToDo);
			}
			f.RemindedUtc = now;
		}
		await db.SaveChangesAsync(ct);
	}

	/// <summary>A quotation made from a lead carries the lead number as its enquiry reference; link it.</summary>
	private async Task LinkQuotationsAsync(CancellationToken ct)
	{
		DateTime since = engine.Now.AddDays(-2);
		List<Quotation> recent = await db.Quotations.AsNoTracking().Where(q => q.ReferenceNumber != null && q.ReferenceNumber.StartsWith("L-") && (q.CreatedDate >= since || (q.SentUtc != null && q.SentUtc >= since))).ToListAsync(ct);
		if (recent.Count == 0)
		{
			return;
		}
		List<string> numbers = recent.Select(q => q.ReferenceNumber!).Distinct().ToList();
		List<CrmLead> leads = await db.Set<CrmLead>().Where(l => numbers.Contains(l.LeadNumber) && l.ClosedUtc == null).ToListAsync(ct);
		if (leads.Count == 0)
		{
			return;
		}
		Dictionary<int, CrmStatus> statuses = await db.Set<CrmStatus>().AsNoTracking().ToDictionaryAsync(x => x.StatusId, ct);
		CrmStatus quote = statuses.Values.First(x => x.Code == "QUOTE");
		bool changed = false;
		foreach (CrmLead lead in leads)
		{
			Quotation? q = recent.Where(x => x.ReferenceNumber == lead.LeadNumber && x.Status != QuotationStatus.Cancelled).OrderByDescending(x => x.QuotationId).FirstOrDefault();
			if (q == null)
			{
				continue;
			}
			if (lead.QuotationId != q.QuotationId)
			{
				lead.QuotationId = q.QuotationId;
				engine.Log(lead.LeadId, CrmActivityKind.Quotation, $"Quotation {q.QuotationNumber} created from this lead in Billing & Finance (₹{q.TotalAmount:N0}, {q.Status}).", null, true);
				changed = true;
			}
			bool sent = q.SentUtc != null || q.Status is QuotationStatus.Sent or QuotationStatus.Viewed or QuotationStatus.Approved;
			if (sent && statuses.TryGetValue(lead.StatusId, out CrmStatus? cur) && !cur.IsClosed && cur.SortOrder < quote.SortOrder)
			{
				lead.StatusId = quote.StatusId;
				engine.Log(lead.LeadId, CrmActivityKind.StatusChange, $"{cur.Name} → {quote.Name} (quotation {q.QuotationNumber} went out).", null, true);
				changed = true;
			}
		}
		if (changed)
		{
			await db.SaveChangesAsync(ct);
		}
	}

	private async Task DigestAsync(CancellationToken ct)
	{
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		DateTime ist = IstClock.ToIst(engine.Now);
		string today = ist.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
		if (!s.EmailEnabled || !s.DigestEnabled || s.DigestLastDate == today || ist.TimeOfDay < new TimeSpan(9, 0, 0) || ist.TimeOfDay > new TimeSpan(11, 0, 0)
			|| !s.WorkDays.Contains(CrmSettingsSnapshot.DayNo(ist.DayOfWeek)))
		{
			return;
		}
		await engine.SetSettingAsync("digest.lastDate", today, null, ct);
		await db.SaveChangesAsync(ct);
		DateTime endOfToday = IstClock.ToUtc(ist.Date.AddDays(1));
		List<(long UserId, string Name, string Email, string Roles)> staff = await engine.CrmStaffAsync(ct);
		HashSet<long> team = (await db.Set<CrmTeamMember>().AsNoTracking().Where(m => m.IsActive).Select(m => m.UserId).ToListAsync(ct)).ToHashSet();
		int unassigned = await db.Set<CrmLead>().CountAsync(l => l.ClosedUtc == null && l.OwnerUserId == null, ct);
		foreach (var person in staff.Where(p => team.Count == 0 || team.Contains(p.UserId)))
		{
			var mine = await (from f in db.Set<CrmFollowUp>().AsNoTracking()
				join l in db.Set<CrmLead>().AsNoTracking() on f.LeadId equals l.LeadId
				where !f.IsDone && f.OwnerUserId == person.UserId && f.DueUtc < endOfToday && l.ClosedUtc == null
				orderby f.DueUtc
				select new { l.LeadNumber, l.Name, f.DueUtc, f.WhatToDo }).Take(50).ToListAsync(ct);
			int waiting = await db.Set<CrmLead>().CountAsync(l => l.ClosedUtc == null && l.FirstContactUtc == null && l.OwnerUserId == person.UserId, ct);
			if (mine.Count == 0 && waiting == 0 && unassigned == 0)
			{
				continue;
			}
			StringBuilder html = new StringBuilder($"<p>Good morning {CrmText.Html(person.Name)}. Your CRM day:</p><ul>");
			foreach (var m in mine)
			{
				html.Append($"<li>{(m.DueUtc < engine.Now ? "<b style=\"color:#b42318\">OVERDUE</b> " : "")}{CrmText.Ist(m.DueUtc)} — {CrmText.Html(m.Name)} ({CrmText.Html(m.LeadNumber)}): {CrmText.Html(m.WhatToDo)}</li>");
			}
			html.Append("</ul>");
			if (waiting > 0)
			{
				html.Append($"<p><b>{waiting}</b> of your leads are still waiting for a first call.</p>");
			}
			if (unassigned > 0)
			{
				html.Append($"<p>{unassigned} lead(s) have no owner yet.</p>");
			}
			await engine.EmailAsync(null, "digest", person.UserId, person.Email, $"Your CRM day — {ist:d MMM}", html.ToString(), ct);
		}
		await db.SaveChangesAsync(ct);
	}

	private async Task RetentionAsync(CancellationToken ct)
	{
		DateTime now = engine.Now;
		if ((now - _lastRetentionUtc).TotalMinutes < 30)
		{
			return;
		}
		_lastRetentionUtc = now;
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		List<long> stale = await db.Set<CrmVisitor>().Where(v => v.LeadId == null && v.DeleteAfterUtc != null && v.DeleteAfterUtc < now).Select(v => v.VisitorId).Take(1000).ToListAsync(ct);
		if (stale.Count > 0)
		{
			await db.Set<CrmWhatsAppRef>().Where(x => stale.Contains(x.VisitorId)).ExecuteDeleteAsync(ct);
			await db.Set<CrmPageView>().Where(x => stale.Contains(x.VisitorId)).ExecuteDeleteAsync(ct);
			await db.Set<CrmVisitor>().Where(x => stale.Contains(x.VisitorId)).ExecuteDeleteAsync(ct);
			logger.LogInformation("CRM retention: {Count} anonymous visitor session(s) deleted", stale.Count);
		}
		DateTime cutoff = now.AddMonths(-s.LostRetentionMonths);
		List<long> oldLost = await db.Set<CrmLead>().Where(l => l.LostReasonId != null && l.ClosedUtc != null && l.ClosedUtc < cutoff && l.CustomerId == null).Select(l => l.LeadId).Take(200).ToListAsync(ct);
		if (oldLost.Count > 0)
		{
			await CrmService.HardDeleteAsync(db, oldLost, ct);
			logger.LogInformation("CRM retention: {Count} lost lead(s) older than {Months} months deleted", oldLost.Count, s.LostRetentionMonths);
		}
		await db.Set<CrmNotification>().Where(n => n.CreatedUtc < now.AddDays(-180)).ExecuteDeleteAsync(ct);
	}
}

public class CrmWorker(IServiceScopeFactory scopes, ILogger<CrmWorker> logger) : BackgroundService
{
	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);
		while (!stoppingToken.IsCancellationRequested)
		{
			try
			{
				using IServiceScope scope = scopes.CreateScope();
				await scope.ServiceProvider.GetRequiredService<CrmJobs>().RunAsync(stoppingToken);
			}
			catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
			{
				break;
			}
			catch (Exception ex)
			{
				logger.LogError(ex, "CRM worker run failed");
			}
			await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
		}
	}
}
