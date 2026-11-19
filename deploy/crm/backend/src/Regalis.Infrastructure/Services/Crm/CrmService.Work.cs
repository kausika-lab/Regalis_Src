using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regalis.Application.Administration;
using Regalis.Application.Common.Exceptions;
using Regalis.Application.Crm;
using Regalis.Domain.Entities.Crm;
using Regalis.Domain.Entities.Finance;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Shared;

namespace Regalis.Infrastructure.Services.Crm;

public partial class CrmService
{
	private static readonly Dictionary<string, CrmActivityKind> ActivityKinds = new Dictionary<string, CrmActivityKind>(StringComparer.OrdinalIgnoreCase)
	{
		["call"] = CrmActivityKind.Call,
		["email"] = CrmActivityKind.Email,
		["whatsapp"] = CrmActivityKind.WhatsApp,
		["meeting"] = CrmActivityKind.Meeting,
		["note"] = CrmActivityKind.Note
	};

	/// <summary>Logging a call (or an email, WhatsApp, meeting) for the first time moves New to Contacted by itself.</summary>
	private async Task MarkContactedAsync(CrmLead lead, CancellationToken ct)
	{
		if (lead.FirstContactUtc == null)
		{
			lead.FirstContactUtc = Now;
		}
		CrmStatus current = await db.Set<CrmStatus>().AsNoTracking().FirstAsync(s => s.StatusId == lead.StatusId, ct);
		if (current.Code == "NEW")
		{
			CrmStatus contacted = await engine.StatusByCodeAsync("CONTACTED", ct);
			lead.StatusId = contacted.StatusId;
			engine.Log(lead.LeadId, CrmActivityKind.StatusChange, "New → Contacted (set by the first logged contact).", null, true);
		}
	}

	public async Task<CrmLeadDetailDto> AddActivityAsync(long leadId, CrmActivityRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		if (!ActivityKinds.TryGetValue(r.Kind ?? "", out CrmActivityKind kind))
		{
			throw new BusinessRuleException("Choose what happened: a call, an email, a WhatsApp message, a meeting or a note.");
		}
		string detail = CrmText.Clip(r.Detail, 2000) ?? throw new BusinessRuleException(kind == CrmActivityKind.Note ? "Write the note." : "Write one line about what was said.");
		if (lead.ClosedUtc.HasValue && kind != CrmActivityKind.Note)
		{
			await EnsureOpenAsync(lead, ct);
		}
		DateTime at = r.HappenedUtc.HasValue && r.HappenedUtc.Value <= Now.AddMinutes(5) ? r.HappenedUtc.Value : Now;
		engine.Log(lead.LeadId, kind, detail, Me, false, at);
		if (kind != CrmActivityKind.Note && !lead.ClosedUtc.HasValue)
		{
			await MarkContactedAsync(lead, ct);
		}
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(leadId, ct);
	}

	// ---- status ---------------------------------------------------------------------------------

	public async Task<CrmLeadDetailDto> ChangeStatusAsync(long leadId, CrmStatusRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		await EnsureOpenAsync(lead, ct);
		CrmStatus target = await db.Set<CrmStatus>().AsNoTracking().FirstOrDefaultAsync(s => s.StatusId == r.StatusId && s.IsActive, ct) ?? throw new NotFoundException("Status", r.StatusId);
		CrmStatus current = await db.Set<CrmStatus>().AsNoTracking().FirstAsync(s => s.StatusId == lead.StatusId, ct);
		if (target.StatusId == current.StatusId)
		{
			return await GetLeadAsync(leadId, ct);
		}
		switch (target.Requires)
		{
		case CrmStatusRequirement.Activity:
			if (lead.FirstContactUtc == null)
			{
				throw new BusinessRuleException("Log the call or email first — that sets this status by itself.");
			}
			break;
		case CrmStatusRequirement.Visit:
			if (r.TourUtc.HasValue)
			{
				if (r.TourUtc.Value < Now.AddDays(-1))
				{
					throw new BusinessRuleException("Set the date and time of the visit.");
				}
				lead.TourUtc = r.TourUtc;
				engine.Log(lead.LeadId, CrmActivityKind.Tour, $"Tour booked for {CrmText.Ist(r.TourUtc.Value)}.", Me);
			}
			else if (lead.TourUtc == null)
			{
				throw new BusinessRuleException("Set the date and time of the visit.");
			}
			break;
		case CrmStatusRequirement.Quotation:
			if (lead.QuotationId == null)
			{
				throw new BusinessRuleException("Link the quotation first, or send one from this lead.");
			}
			break;
		case CrmStatusRequirement.Customer:
			throw new BusinessRuleException("Use Convert to customer — a won lead needs a customer record.");
		case CrmStatusRequirement.Reason:
			return await LoseAsync(leadId, new CrmLoseRequest { LostReasonId = r.LostReasonId, Note = r.LostNote }, ct);
		}
		if (target.IsClosed)
		{
			throw new BusinessRuleException("Use Convert to customer or Lost to close a lead.");
		}
		lead.StatusId = target.StatusId;
		engine.Log(lead.LeadId, CrmActivityKind.StatusChange, $"{current.Name} → {target.Name}.", Me);
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(leadId, ct);
	}

	/// <summary>Moves the lead forward to a stage (never backwards), with a system log line.</summary>
	internal async Task AdvanceToAsync(CrmLead lead, string code, string why, CancellationToken ct)
	{
		CrmStatus current = await db.Set<CrmStatus>().AsNoTracking().FirstAsync(s => s.StatusId == lead.StatusId, ct);
		CrmStatus target = await engine.StatusByCodeAsync(code, ct);
		if (current.IsClosed || current.SortOrder >= target.SortOrder)
		{
			return;
		}
		lead.StatusId = target.StatusId;
		engine.Log(lead.LeadId, CrmActivityKind.StatusChange, $"{current.Name} → {target.Name} ({why}).", null, true);
	}

	// ---- owner ----------------------------------------------------------------------------------

	public async Task<CrmLeadDetailDto> AssignAsync(long leadId, CrmAssignRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		await EnsureOpenAsync(lead, ct);
		if (r.UserId == lead.OwnerUserId)
		{
			return await GetLeadAsync(leadId, ct);
		}
		string fromName = lead.OwnerUserId.HasValue ? await engine.UserNameAsync(lead.OwnerUserId, ct) : "Unassigned";
		if (r.UserId.HasValue)
		{
			var staff = (await engine.CrmStaffAsync(ct)).FirstOrDefault(s => s.UserId == r.UserId);
			if (staff.UserId == 0)
			{
				throw new BusinessRuleException("That person does not handle leads. Pick someone from the list.");
			}
			if (!r.Force && !(await engine.OnDutyAsync(ct)).Contains(r.UserId.Value))
			{
				string first = staff.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? staff.Name;
				throw new BusinessRuleException($"{first} is not on duty today. Assign anyway?", "NOT_ON_DUTY");
			}
			lead.OwnerUserId = r.UserId;
			engine.Log(lead.LeadId, CrmActivityKind.Assignment, $"Reassigned from {fromName} to {staff.Name} by {currentUser.FullName}.", Me);
			if (r.UserId != Me)
			{
				engine.InApp(lead.LeadId, "lead.assigned", r.UserId.Value, $"Lead {lead.LeadNumber} was given to you", $"{lead.Name} · by {currentUser.FullName}");
			}
		}
		else
		{
			lead.OwnerUserId = null;
			engine.Log(lead.LeadId, CrmActivityKind.Assignment, $"Unassigned (was {fromName}) by {currentUser.FullName}.", Me);
		}
		foreach (CrmFollowUp f in await db.Set<CrmFollowUp>().Where(f => f.LeadId == leadId && !f.IsDone).ToListAsync(ct))
		{
			f.OwnerUserId = lead.OwnerUserId;
		}
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(leadId, ct);
	}

	/// <summary>"Take it": anyone who works leads may take an unassigned one for themselves.</summary>
	public async Task<CrmLeadDetailDto> TakeAsync(long leadId, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		await EnsureOpenAsync(lead, ct);
		if (lead.OwnerUserId.HasValue)
		{
			throw new BusinessRuleException(lead.OwnerUserId == Me ? "This lead is already yours." : "Somebody has already taken this lead.");
		}
		lead.OwnerUserId = Me;
		engine.Log(lead.LeadId, CrmActivityKind.Assignment, $"Taken from the unassigned pool by {currentUser.FullName}.", Me);
		foreach (CrmFollowUp f in await db.Set<CrmFollowUp>().Where(f => f.LeadId == leadId && !f.IsDone).ToListAsync(ct))
		{
			f.OwnerUserId = Me;
		}
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(leadId, ct);
	}

	// ---- tour and quotation ---------------------------------------------------------------------

	public async Task<CrmLeadDetailDto> BookTourAsync(long leadId, CrmTourRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		await EnsureOpenAsync(lead, ct);
		if (!r.TourUtc.HasValue || r.TourUtc.Value < Now.AddMinutes(-5))
		{
			throw new BusinessRuleException("Set the date and time of the visit.");
		}
		lead.TourUtc = r.TourUtc;
		string note = CrmText.Clip(r.Note, 300) ?? "";
		engine.Log(lead.LeadId, CrmActivityKind.Tour, $"Tour booked for {CrmText.Ist(r.TourUtc.Value)} IST." + (note == "" ? "" : " " + note), Me);
		await AdvanceToAsync(lead, "VISIT", "a tour date was set", ct);
		db.Set<CrmFollowUp>().Add(new CrmFollowUp
		{
			LeadId = lead.LeadId,
			DueUtc = r.TourUtc.Value,
			WhatToDo = CrmText.Clip("Show them around" + (note == "" ? "" : ": " + note), 500)!,
			Kind = "call",
			OwnerUserId = lead.OwnerUserId ?? Me
		});
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(leadId, ct);
	}

	public async Task<CrmLeadDetailDto> LinkQuotationAsync(long leadId, CrmLinkQuotationRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		await EnsureOpenAsync(lead, ct);
		Quotation? q = null;
		if (r.QuotationId.HasValue)
		{
			q = await db.Quotations.AsNoTracking().FirstOrDefaultAsync(x => x.QuotationId == r.QuotationId, ct);
		}
		else if (!string.IsNullOrWhiteSpace(r.QuotationNumber))
		{
			string number = r.QuotationNumber.Trim();
			q = await db.Quotations.AsNoTracking().Where(x => x.QuotationNumber == number).OrderByDescending(x => x.QuotationId).FirstOrDefaultAsync(ct);
		}
		if (q == null)
		{
			throw new BusinessRuleException("No quotation with that number was found in Billing & Finance.");
		}
		await LinkAsync(lead, q, $"Quotation {q.QuotationNumber} linked by {currentUser.FullName}.", ct);
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(leadId, ct);
	}

	/// <summary>Links a Billing quotation; moves the lead to Quotation sent once it has gone out.</summary>
	internal async Task LinkAsync(CrmLead lead, Quotation q, string detail, CancellationToken ct)
	{
		lead.QuotationId = q.QuotationId;
		engine.Log(lead.LeadId, CrmActivityKind.Quotation, detail + $" Total ₹{q.TotalAmount:N0}, {q.Status}.", null, true);
		await AdvanceToAsync(lead, "QUOTE", "a quotation is linked", ct);
	}

	// ---- follow-ups ----------------------------------------------------------------------------

	public async Task<CrmLeadDetailDto> AddFollowUpAsync(long leadId, CrmFollowUpRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		await EnsureOpenAsync(lead, ct);
		ValidateFollowUp(r.DueUtc, r.WhatToDo, required: true);
		long? owner = r.OwnerUserId ?? lead.OwnerUserId ?? Me;
		if (owner != Me && owner != lead.OwnerUserId && !currentUser.HasPermission("CRM.LEAD.ASSIGN"))
		{
			throw new ForbiddenException("Only a manager can set a follow-up for someone else.");
		}
		db.Set<CrmFollowUp>().Add(new CrmFollowUp
		{
			LeadId = lead.LeadId,
			DueUtc = r.DueUtc!.Value,
			WhatToDo = CrmText.Clip(r.WhatToDo, 500)!,
			Kind = string.Equals(r.Kind, "email", StringComparison.OrdinalIgnoreCase) ? "email" : "call",
			OwnerUserId = owner
		});
		engine.Log(lead.LeadId, CrmActivityKind.Note, $"Follow-up set for {CrmText.Ist(r.DueUtc.Value)}: {CrmText.Clip(r.WhatToDo, 400)}", Me);
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(leadId, ct);
	}

	private static readonly Dictionary<string, string> Outcomes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
	{
		["spoke"] = "Spoke to them",
		["no-answer"] = "No answer",
		["left-message"] = "Left a message",
		["sent-email"] = "Sent an email",
		["other"] = "Other"
	};

	public async Task<CrmLeadDetailDto> CompleteFollowUpAsync(long followUpId, CrmFollowUpDoneRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmFollowUp f = await db.Set<CrmFollowUp>().FirstOrDefaultAsync(x => x.FollowUpId == followUpId, ct) ?? throw new NotFoundException("Follow-up", followUpId);
		CrmLead lead = await RequireLeadAsync(f.LeadId, ct);
		await EnsureOpenAsync(lead, ct);
		if (f.IsDone)
		{
			throw new BusinessRuleException("This follow-up is already done.");
		}
		if (!Outcomes.TryGetValue(r.Outcome ?? "", out string? outcome))
		{
			throw new BusinessRuleException("Choose the outcome: spoke to them, no answer, left a message or sent an email.");
		}
		bool otherOpen = await db.Set<CrmFollowUp>().AnyAsync(x => x.LeadId == lead.LeadId && !x.IsDone && x.FollowUpId != followUpId, ct);
		if (r.Next == null && !otherOpen)
		{
			throw new BusinessRuleException("Set the next follow-up, or close the lead as lost. A lead is never left without a next step.");
		}
		if (r.Next != null)
		{
			ValidateFollowUp(r.Next.DueUtc, r.Next.WhatToDo, required: true);
		}
		string note = CrmText.Clip(r.Note, 400) ?? "";
		f.IsDone = true;
		f.Outcome = CrmText.Clip(outcome + (note == "" ? "" : ": " + note), 500);
		f.DoneUtc = Now;
		f.DoneByUserId = Me;
		CrmActivityKind kind = r.Outcome == "sent-email" ? CrmActivityKind.Email : CrmActivityKind.Call;
		engine.Log(lead.LeadId, kind, $"Follow-up “{f.WhatToDo}” done — {outcome}{(note == "" ? "" : ": " + note)}.", Me);
		await MarkContactedAsync(lead, ct);
		if (r.Next != null)
		{
			db.Set<CrmFollowUp>().Add(new CrmFollowUp
			{
				LeadId = lead.LeadId,
				DueUtc = r.Next.DueUtc!.Value,
				WhatToDo = CrmText.Clip(r.Next.WhatToDo, 500)!,
				Kind = string.Equals(r.Next.Kind, "email", StringComparison.OrdinalIgnoreCase) ? "email" : "call",
				OwnerUserId = lead.OwnerUserId ?? Me
			});
			engine.Log(lead.LeadId, CrmActivityKind.Note, $"Next follow-up set for {CrmText.Ist(r.Next.DueUtc.Value)}: {CrmText.Clip(r.Next.WhatToDo, 400)}", Me);
		}
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(lead.LeadId, ct);
	}

	public async Task<CrmFollowUpBoardDto> GetFollowUpsAsync(string? scope, long? ownerId, CancellationToken ct = default)
	{
		EnsureStaff();
		bool all = string.Equals(scope, "all", StringComparison.OrdinalIgnoreCase) && CanViewAll;
		IQueryable<CrmLead> leads = Visible().Where(l => l.ClosedUtc == null);
		if (ownerId.HasValue && CanViewAll)
		{
			leads = ownerId == 0 ? leads.Where(l => l.OwnerUserId == null) : leads.Where(l => l.OwnerUserId == ownerId);
		}
		else if (!all)
		{
			long me = Me;
			leads = leads.Where(l => l.OwnerUserId == me || l.OwnerUserId == null);
		}
		List<CrmLead> open = await leads.ToListAsync(ct);
		List<long> ids = open.Select(l => l.LeadId).ToList();
		Dictionary<long, CrmLead> byId = open.ToDictionary(l => l.LeadId);
		DateTime weekEnd = IstClock.ToUtc(IstClock.ToIst(Now).Date.AddDays(8));
		List<CrmFollowUp> follow = await db.Set<CrmFollowUp>().AsNoTracking().Where(f => !f.IsDone && ids.Contains(f.LeadId) && f.DueUtc < weekEnd).OrderBy(f => f.DueUtc).ToListAsync(ct);
		Dictionary<int, string> looking = await db.Set<CrmLookingFor>().AsNoTracking().ToDictionaryAsync(x => x.LookingForId, x => x.Name, ct);
		List<long> owners = follow.Where(f => f.OwnerUserId.HasValue).Select(f => f.OwnerUserId!.Value).Concat(open.Where(l => l.OwnerUserId.HasValue).Select(l => l.OwnerUserId!.Value)).Distinct().ToList();
		Dictionary<long, string> names = await db.Users.AsNoTracking().IgnoreQueryFilters().Where(u => owners.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.FullName, ct);
		string? LF(CrmLead l) => l.LookingForId.HasValue && looking.TryGetValue(l.LookingForId.Value, out string? n) ? n : null;
		string? Name(long? id) => id.HasValue && names.TryGetValue(id.Value, out string? n) ? n : null;
		(DateTime todayStart, DateTime todayEnd) = TodayUtc();
		List<CrmFollowUpDto> overdue = follow.Where(f => f.DueUtc < Now).Select(f => FollowDto(f, byId[f.LeadId], LF(byId[f.LeadId]), Name(f.OwnerUserId))).ToList();
		// Leads never contacted at all, labelled "never contacted" rather than a date.
		foreach (CrmLead l in open.Where(l => l.FirstContactUtc == null && (l.ResponseDueUtc == null || l.ResponseDueUtc < Now)).OrderBy(l => l.CreatedDate))
		{
			overdue.Add(new CrmFollowUpDto(0, l.LeadId, l.LeadNumber, l.Name, l.Company, LF(l), l.ResponseDueUtc ?? l.CreatedDate,
				"Make first contact — they have not been called yet.", "call", l.OwnerUserId, Name(l.OwnerUserId), false, null, null, true,
				(int)(Now - (l.ResponseDueUtc ?? l.CreatedDate)).TotalMinutes, true));
		}
		overdue = overdue.OrderBy(o => o.DueUtc).ToList();
		List<CrmFollowUpDto> today = follow.Where(f => f.DueUtc >= Now && f.DueUtc < todayEnd).Select(f => FollowDto(f, byId[f.LeadId], LF(byId[f.LeadId]), Name(f.OwnerUserId))).ToList();
		List<CrmFollowUpDto> week = follow.Where(f => f.DueUtc >= todayEnd).Select(f => FollowDto(f, byId[f.LeadId], LF(byId[f.LeadId]), Name(f.OwnerUserId))).ToList();
		List<CrmFollowUp> dueTodayAll = follow.Where(f => f.DueUtc >= todayStart && f.DueUtc < todayEnd).ToList();
		CrmSettingsSnapshot s = await engine.SettingsAsync(ct);
		// Average first reply, last 30 days, in working minutes, over leads that had a response clock.
		DateTime since = Now.AddDays(-30);
		List<CrmLead> replied = await Visible().Where(l => l.CreatedDate >= since && l.FirstContactUtc != null && l.ResponseDueUtc != null).ToListAsync(ct);
		int? avg = replied.Count == 0 ? null : (int)Math.Round(replied.Average(l => (double)s.WorkingMinutesBetween(l.CreatedDate, l.FirstContactUtc!.Value)));
		return new CrmFollowUpBoardDto(overdue, today, week, overdue.Count, overdue.Count == 0 ? null : overdue.Max(o => o.LateMinutes),
			dueTodayAll.Count, dueTodayAll.Count(f => f.Kind != "email"), dueTodayAll.Count(f => f.Kind == "email"),
			week.Count + today.Count, follow.Where(f => f.DueUtc >= Now).Select(f => f.OwnerUserId).Distinct().Count(), avg, s.TargetWeb,
			await GetMyNotificationsAsync(ct));
	}

	public async Task<List<CrmNotificationDto>> GetMyNotificationsAsync(CancellationToken ct = default)
	{
		long me = Me;
		DateTime since = Now.AddDays(-14);
		return await db.Set<CrmNotification>().AsNoTracking().Where(n => n.RecipientUserId == me && n.Channel == "inapp" && n.CreatedUtc >= since)
			.OrderByDescending(n => n.CreatedUtc).Take(30)
			.Select(n => new CrmNotificationDto(n.NotificationId, n.LeadId, n.Kind, n.Title, n.Body, n.CreatedUtc, n.ReadUtc != null)).ToListAsync(ct);
	}

	public async Task MarkNotificationsReadAsync(CancellationToken ct = default)
	{
		long me = Me;
		DateTime now = Now;
		await db.Set<CrmNotification>().Where(n => n.RecipientUserId == me && n.ReadUtc == null).ExecuteUpdateAsync(u => u.SetProperty(n => n.ReadUtc, now), ct);
	}

	// ---- conversion -----------------------------------------------------------------------------

	public async Task<CrmConvertPreviewDto> PreviewConvertAsync(long leadId, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		string? already = lead.CustomerId.HasValue && lead.ConvertedUtc.HasValue
			? $"This lead was converted on {IstClock.ToIst(lead.ConvertedUtc.Value).ToString("d MMM", CultureInfo.InvariantCulture)} and is now customer #{lead.CustomerId}."
			: null;
		var match = (await engine.MatchingCustomersAsync(lead.MobileNormalised, lead.EmailNormalised, ct)).FirstOrDefault();
		CrmQuotationLinkDto? quotation = lead.QuotationId.HasValue
			? await db.Quotations.AsNoTracking().Where(x => x.QuotationId == lead.QuotationId).Select(x => new CrmQuotationLinkDto(x.QuotationId, x.QuotationNumber, x.Status.ToString(), x.TotalAmount, x.SentUtc)).FirstOrDefaultAsync(ct)
			: null;
		return new CrmConvertPreviewDto(match.User == null ? null : new CrmDuplicateCustomerDto(match.User.UserId, match.User.FullName, match.User.CompanyName, match.User.MobileNumber, match.User.Email, match.MatchedOn),
			already, quotation);
	}

	/// <summary>
	/// Document section 9.2-9.3: check for an existing customer first (mobile, then email); otherwise the
	/// Customers module (IUserAdminService) creates the customer and issues the id. CRM never writes a customer
	/// row itself. The lead closes as Won with the link, in one transaction; if Customers refuses, nothing changes.
	/// </summary>
	public async Task<CrmConvertResultDto> ConvertAsync(long leadId, CrmConvertRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		if (lead.CustomerId.HasValue && lead.ConvertedUtc.HasValue)
		{
			throw new BusinessRuleException($"This lead was converted on {IstClock.ToIst(lead.ConvertedUtc.Value).ToString("d MMM", CultureInfo.InvariantCulture)} and is now customer #{lead.CustomerId}.");
		}
		await EnsureOpenAsync(lead, ct);
		string name = CrmText.Clip(r.Name, 150) ?? lead.Name;
		string mobile = string.IsNullOrWhiteSpace(r.Mobile) ? lead.Mobile : r.Mobile.Trim();
		if (string.IsNullOrWhiteSpace(name) || !CrmText.IsValidMobile(mobile))
		{
			throw new BusinessRuleException("A customer needs at least a name and a mobile number. Fill them in on the lead first.");
		}
		string? email = CrmText.Clip(r.Email, 256) ?? lead.Email;
		string mobileNorm = CrmText.NormaliseMobile(mobile);
		var match = (await engine.MatchingCustomersAsync(mobileNorm, CrmText.NormaliseEmail(email), ct)).FirstOrDefault();
		bool existing = match.User != null;
		if (!existing && !r.CreateCustomer)
		{
			throw new BusinessRuleException("No existing customer matches this mobile or email, so a won lead needs a new customer record. Tick “Create the customer record now”.");
		}
		if (!existing && (email == null || !CrmText.IsValidEmail(email)))
		{
			throw new BusinessRuleException("The customer could not be created: the Customers module needs an email address for the sign-in. Add their email and try again. The lead has not been changed.");
		}
		string company = (CrmText.Clip(r.Company, 150) ?? lead.Company) ?? "";
		bool asCompany = string.Equals(r.CustomerType, "company", StringComparison.OrdinalIgnoreCase);
		long customerId = 0;
		string customerName = "";
		bool carried = false;
		await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
		{
			await using var tx = await db.Database.BeginTransactionAsync(ct);
			if (existing)
			{
				customerId = match.User!.UserId;
				customerName = match.User.FullName;
			}
			else
			{
				int customerRole = await db.Roles.AsNoTracking().Where(x => x.RoleCode == "CUSTOMER").Select(x => x.RoleId).FirstAsync(ct);
				string storedMobile = mobile.StartsWith("+", StringComparison.Ordinal) ? new string(mobile.Where(c => c == '+' || char.IsAsciiDigit(c)).ToArray()) : "+91" + mobileNorm;
				try
				{
					UserDetailDto created = await userAdmin.CreateUserAsync(new CreateUserRequest(name, email!.Trim(), storedMobile, TemporaryPassword(), 1, new[] { customerRole },
						asCompany || company != "" ? company : null, lead.LocationId), ct);
					customerId = created.UserId;
					customerName = created.FullName;
				}
				catch (AppException ex)
				{
					throw new BusinessRuleException($"The customer could not be created: {ex.Message} The lead has not been changed.");
				}
			}
			if (r.CarryQuotation && lead.QuotationId.HasValue)
			{
				Quotation? q = await db.Quotations.FirstOrDefaultAsync(x => x.QuotationId == lead.QuotationId, ct);
				if (q != null && q.CustomerId == null)
				{
					q.CustomerId = customerId;
					carried = true;
				}
				else if (q != null && q.CustomerId == customerId)
				{
					carried = true;
				}
			}
			CrmStatus won = await engine.StatusByCodeAsync("WON", ct);
			CrmStatus from = await db.Set<CrmStatus>().AsNoTracking().FirstAsync(s => s.StatusId == lead.StatusId, ct);
			lead.StatusId = won.StatusId;
			lead.CustomerId = customerId;
			lead.ConvertedUtc = Now;
			lead.ClosedUtc = Now;
			engine.Log(lead.LeadId, CrmActivityKind.Conversion,
				(existing ? $"Attached to existing customer {customerName} (#{customerId}) — no second record created." : $"Customer {customerName} (#{customerId}) created by the Customers module.") +
				(carried ? " The quotation was carried across to the customer." : "") + $" {from.Name} → {won.Name}.", Me);
			await CloseFollowUpsAsync(lead.LeadId, "Closed — the lead was won.", ct);
			await db.SaveChangesAsync(ct);
			await tx.CommitAsync(ct);
		});
		logger.LogInformation("CRM lead {Lead} converted to customer {CustomerId} (existing: {Existing})", lead.LeadNumber, customerId, existing);
		string message = existing
			? $"{customerName} is already a customer (#{customerId}). This lead is attached to that customer — no second record was created. The lead is closed as Won and its history stays readable."
			: $"Customer #{customerId} ({customerName}) now exists. They sign in with “Forgot password” or Google using {email}. The lead is closed as Won, keeps a link to the customer, and its history stays readable.";
		return new CrmConvertResultDto(customerId, customerName, existing, carried, message);
	}

	private static string TemporaryPassword()
	{
		const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
		Span<byte> bytes = stackalloc byte[14];
		RandomNumberGenerator.Fill(bytes);
		char[] pw = new char[14];
		for (int i = 0; i < pw.Length; i++)
		{
			pw[i] = chars[bytes[i] % chars.Length];
		}
		return new string(pw) + "#7a";
	}

	private async Task CloseFollowUpsAsync(long leadId, string outcome, CancellationToken ct)
	{
		foreach (CrmFollowUp f in await db.Set<CrmFollowUp>().Where(f => f.LeadId == leadId && !f.IsDone).ToListAsync(ct))
		{
			f.IsDone = true;
			f.Outcome = outcome;
			f.DoneUtc = Now;
			f.DoneByUserId = Me;
		}
	}

	public async Task<CrmLeadDetailDto> LoseAsync(long leadId, CrmLoseRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		await EnsureOpenAsync(lead, ct);
		CrmLostReason reason = r.LostReasonId.HasValue
			? await db.Set<CrmLostReason>().AsNoTracking().FirstOrDefaultAsync(x => x.LostReasonId == r.LostReasonId && x.IsActive, ct) ?? throw new BusinessRuleException("Choose why this lead was lost. It is the only thing that makes the lost report useful.")
			: throw new BusinessRuleException("Choose why this lead was lost. It is the only thing that makes the lost report useful.");
		CrmStatus lost = await engine.StatusByCodeAsync("LOST", ct);
		CrmStatus from = await db.Set<CrmStatus>().AsNoTracking().FirstAsync(s => s.StatusId == lead.StatusId, ct);
		lead.StatusId = lost.StatusId;
		lead.LostReasonId = reason.LostReasonId;
		lead.LostNote = CrmText.Clip(r.Note, 500);
		lead.ClosedUtc = Now;
		engine.Log(lead.LeadId, CrmActivityKind.StatusChange, $"{from.Name} → {lost.Name}: {reason.Name}{(lead.LostNote == null ? "" : " — " + lead.LostNote)}.", Me);
		await CloseFollowUpsAsync(lead.LeadId, "Closed — the lead was lost.", ct);
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(leadId, ct);
	}

	public async Task<CrmLeadDetailDto> ReopenAsync(long leadId, CrmReopenRequest r, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		if (!lead.ClosedUtc.HasValue)
		{
			throw new BusinessRuleException("Only a closed lead can be reopened.");
		}
		if (lead.CustomerId.HasValue && lead.ConvertedUtc.HasValue)
		{
			throw new BusinessRuleException("Converting cannot be undone. If it was wrong, handle the customer record in Customers; the lead stays closed.");
		}
		CrmStatus target = await engine.StatusByCodeAsync(lead.FirstContactUtc.HasValue ? "CONTACTED" : "NEW", ct);
		string reasonName = lead.LostReasonId.HasValue ? await db.Set<CrmLostReason>().AsNoTracking().Where(x => x.LostReasonId == lead.LostReasonId).Select(x => x.Name).FirstOrDefaultAsync(ct) ?? "" : "";
		lead.StatusId = target.StatusId;
		lead.ClosedUtc = null;
		lead.LostReasonId = null;
		lead.LostNote = null;
		string why = CrmText.Clip(r.Reason, 300) ?? "";
		engine.Log(lead.LeadId, CrmActivityKind.Reopened, $"Reopened by {currentUser.FullName} (was Lost{(reasonName == "" ? "" : ": " + reasonName)}) → {target.Name}." + (why == "" ? "" : " " + why), Me);
		await db.SaveChangesAsync(ct);
		return await GetLeadAsync(leadId, ct);
	}

	/// <summary>Delete on request (DPDP): the lead, its activities, follow-ups, consent, notifications and its visitor
	/// session go together, in one transaction. A hard delete; the CRM tables are not copied into the audit log.</summary>
	public async Task DeleteLeadAsync(long leadId, CancellationToken ct = default)
	{
		EnsureStaff();
		CrmLead lead = await RequireLeadAsync(leadId, ct);
		string number = lead.LeadNumber;
		await HardDeleteAsync(db, new List<long> { leadId }, ct);
		logger.LogWarning("CRM lead {Lead} deleted on request by user {UserId}", number, Me);
	}

	internal static async Task HardDeleteAsync(Regalis.Infrastructure.Persistence.RegalisDbContext db, List<long> leadIds, CancellationToken ct)
	{
		await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
		{
			await using var tx = await db.Database.BeginTransactionAsync(ct);
			List<long> visitors = await db.Set<CrmLead>().Where(l => leadIds.Contains(l.LeadId) && l.VisitorId != null).Select(l => l.VisitorId!.Value).ToListAsync(ct);
			visitors.AddRange(await db.Set<CrmVisitor>().Where(v => v.LeadId != null && leadIds.Contains(v.LeadId.Value)).Select(v => v.VisitorId).ToListAsync(ct));
			visitors = visitors.Distinct().ToList();
			await db.Set<CrmLeadActivity>().Where(x => leadIds.Contains(x.LeadId)).ExecuteDeleteAsync(ct);
			await db.Set<CrmFollowUp>().Where(x => leadIds.Contains(x.LeadId)).ExecuteDeleteAsync(ct);
			await db.Set<CrmLeadConsent>().Where(x => leadIds.Contains(x.LeadId)).ExecuteDeleteAsync(ct);
			await db.Set<CrmNotification>().Where(x => x.LeadId != null && leadIds.Contains(x.LeadId.Value)).ExecuteDeleteAsync(ct);
			await db.Set<CrmWhatsAppRef>().Where(x => visitors.Contains(x.VisitorId) || (x.LeadId != null && leadIds.Contains(x.LeadId.Value))).ExecuteDeleteAsync(ct);
			await db.Set<CrmPageView>().Where(x => visitors.Contains(x.VisitorId)).ExecuteDeleteAsync(ct);
			await db.Set<CrmVisitor>().Where(x => visitors.Contains(x.VisitorId)).ExecuteDeleteAsync(ct);
			await db.Set<CrmLead>().Where(x => leadIds.Contains(x.LeadId)).ExecuteDeleteAsync(ct);
			await tx.CommitAsync(ct);
		});
	}
}
