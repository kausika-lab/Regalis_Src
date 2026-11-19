using System;
using Regalis.Domain.Common;

namespace Regalis.Domain.Entities.Crm;

// CRM - Leads and Web Enquiries (build document, Nov 2026; kit deploy/crm).
//
// Tables that hold a person's details (lead, activity, follow-up, consent, visitor, page view,
// notification) implement IAuditableEntity only, NOT AuditableEntity: the audit interceptor
// copies whole rows of AuditableEntity classes into TRN_AuditLog, and a lead deleted on request
// (DPDP, document section 17) must leave no copy behind. The lead's own activity log is its
// history. Lists edited in CRM Settings derive from AuditableEntity, so their changes are audited.

/// <summary>How a lead reached us. Stored as tinyint on TRN_CrmLead.Channel.</summary>
public enum CrmChannel : byte
{
	/// <summary>Typed in by staff (phone call, walk-in, email, WhatsApp, referral, event, other).</summary>
	Manual = 1,
	/// <summary>The public "Book a tour" enquiry form.</summary>
	WebForm = 2,
	/// <summary>The public "Busy? We will call you" bar.</summary>
	Callback = 3,
	/// <summary>"Send me a quote" (the enquiry form opened with Looking for filled in).</summary>
	QuoteRequest = 4,
	/// <summary>WhatsApp message carrying a W- reference code, entered by staff.</summary>
	WhatsApp = 5
}

/// <summary>What a status needs before it can be set (document section 11.2).</summary>
public enum CrmStatusRequirement : byte
{
	None = 0,
	Activity = 1,
	Visit = 2,
	Quotation = 3,
	Customer = 4,
	Reason = 5
}

public enum CrmActivityKind : byte
{
	Enquiry = 1,
	Call = 2,
	Email = 3,
	WhatsApp = 4,
	Meeting = 5,
	Note = 6,
	StatusChange = 7,
	Assignment = 8,
	Quotation = 9,
	Tour = 10,
	AutoAck = 11,
	FollowUpDone = 12,
	Conversion = 13,
	Reopened = 14,
	Edited = 15
}

public enum CrmRuleCondition : byte
{
	ExistingCustomer = 1,
	Location = 2,
	Size = 3,
	RoundRobin = 4,
	Fallback = 5
}

public enum CrmRuleTarget : byte
{
	/// <summary>The staff member who already looks after the customer (rule 1).</summary>
	CustomerOwner = 1,
	Manager = 2,
	OnDuty = 3,
	RoundRobin = 4,
	Unassigned = 5,
	NamedUser = 6
}

/// <summary>TRN_CrmLead: somebody who is not yet a customer (document sections 3-5, 13.1).</summary>
public class CrmLead : IAuditableEntity
{
	public long LeadId { get; set; }

	/// <summary>L-000241, from MST_NumberSequence key CrmLead.</summary>
	public string LeadNumber { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string? Company { get; set; }

	/// <summary>As typed.</summary>
	public string Mobile { get; set; } = string.Empty;

	/// <summary>Last 10 digits, written on every insert and update. The duplicate key.</summary>
	public string MobileNormalised { get; set; } = string.Empty;

	public string? Email { get; set; }

	/// <summary>Lower-cased email, or null.</summary>
	public string? EmailNormalised { get; set; }

	public int? LookingForId { get; set; }

	public int? PeopleCount { get; set; }

	/// <summary>Free text: people say "around 45k", not a number.</summary>
	public string? BudgetText { get; set; }

	public int? LocationId { get; set; }

	public DateOnly? NeededFrom { get; set; }

	public int StatusId { get; set; }

	public int SourceId { get; set; }

	/// <summary>"How did they hear of us" on a hand-entered lead, or the referring site / campaign on a web lead.</summary>
	public string? SourceDetail { get; set; }

	public CrmChannel Channel { get; set; } = CrmChannel.Manual;

	public long? OwnerUserId { get; set; }

	public long? ReferredByCustomerId { get; set; }

	public DateTime? WalkInUtc { get; set; }

	public long? ShownByUserId { get; set; }

	public long? VisitorId { get; set; }

	/// <summary>The person's own words, never edited.</summary>
	public string? FirstMessage { get; set; }

	public int? LostReasonId { get; set; }

	public string? LostNote { get; set; }

	public long? CustomerId { get; set; }

	public DateTime? ConvertedUtc { get; set; }

	/// <summary>The quotation (Billing &amp; Finance) this lead was sent. CRM only links to it.</summary>
	public long? QuotationId { get; set; }

	/// <summary>The tour date and time (Visit booked).</summary>
	public DateTime? TourUtc { get; set; }

	/// <summary>Known value of the deal, a month, in rupees (optional; shown on the pipeline).</summary>
	public decimal? DealValue { get; set; }

	/// <summary>The first logged call / email / WhatsApp / meeting. Null = waiting for first contact.</summary>
	public DateTime? FirstContactUtc { get; set; }

	/// <summary>When the first reply is due (working hours only). Null for walk-ins and phone calls.</summary>
	public DateTime? ResponseDueUtc { get; set; }

	public DateTime? OwnerNudgedUtc { get; set; }

	public DateTime? ManagerEscalatedUtc { get; set; }

	public DateTime? ClosedUtc { get; set; }

	public long? CreatedBy { get; set; }

	public DateTime CreatedDate { get; set; }

	public long? ModifiedBy { get; set; }

	public DateTime? ModifiedDate { get; set; }
}

/// <summary>TRN_CrmLeadActivity: append-only. No update path, no delete path (except delete on request).</summary>
public class CrmLeadActivity : IAuditableEntity
{
	public long ActivityId { get; set; }

	public long LeadId { get; set; }

	public CrmActivityKind Kind { get; set; }

	public string Detail { get; set; } = string.Empty;

	public DateTime HappenedUtc { get; set; }

	public long? ByUserId { get; set; }

	public bool IsSystem { get; set; }

	public long? CreatedBy { get; set; }

	public DateTime CreatedDate { get; set; }

	public long? ModifiedBy { get; set; }

	public DateTime? ModifiedDate { get; set; }
}

/// <summary>TRN_CrmFollowUp: a date, a time, an owner and a sentence saying what to do.</summary>
public class CrmFollowUp : IAuditableEntity
{
	public long FollowUpId { get; set; }

	public long LeadId { get; set; }

	public DateTime DueUtc { get; set; }

	public string WhatToDo { get; set; } = string.Empty;

	/// <summary>"call" or "email": splits Due today on the Follow-ups screen.</summary>
	public string Kind { get; set; } = "call";

	public long? OwnerUserId { get; set; }

	public bool IsDone { get; set; }

	public string? Outcome { get; set; }

	public DateTime? DoneUtc { get; set; }

	public long? DoneByUserId { get; set; }

	public DateTime? RemindedUtc { get; set; }

	public long? CreatedBy { get; set; }

	public DateTime CreatedDate { get; set; }

	public long? ModifiedBy { get; set; }

	public DateTime? ModifiedDate { get; set; }
}

/// <summary>TRN_CrmLeadConsent: the wording shown is copied, not referenced.</summary>
public class CrmLeadConsent : IAuditableEntity
{
	public long ConsentId { get; set; }

	public long LeadId { get; set; }

	/// <summary>"Website enquiry form" / "Entered by staff after a phone call" ...</summary>
	public string HowObtained { get; set; } = string.Empty;

	public string? ConsentTextShown { get; set; }

	public bool ContactConsent { get; set; }

	public bool MarketingConsent { get; set; }

	public DateTime? ConsentedUtc { get; set; }

	public string? SourceIpHash { get; set; }

	public long? CreatedBy { get; set; }

	public DateTime CreatedDate { get; set; }

	public long? ModifiedBy { get; set; }

	public DateTime? ModifiedDate { get; set; }
}

/// <summary>TRN_CrmVisitor: an anonymous browser with a first-party cookie. Never a person.</summary>
public class CrmVisitor : IAuditableEntity
{
	public long VisitorId { get; set; }

	/// <summary>The random cookie value (unique). Shown to staff as V-{VisitorId}.</summary>
	public string VisitorKey { get; set; } = string.Empty;

	public DateTime FirstSeenUtc { get; set; }

	public DateTime LastSeenUtc { get; set; }

	/// <summary>The ORIGINAL source; a later direct visit does not overwrite it.</summary>
	public int? SourceId { get; set; }

	public string? UtmSource { get; set; }

	public string? UtmMedium { get; set; }

	public string? UtmCampaign { get; set; }

	/// <summary>When the campaign tag was picked up; kept 90 days.</summary>
	public DateTime? CampaignSeenUtc { get; set; }

	public string? Referrer { get; set; }

	public string? LandingPage { get; set; }

	public string? City { get; set; }

	public string? Country { get; set; }

	public string? DeviceType { get; set; }

	public string? Browser { get; set; }

	public int VisitCount { get; set; }

	public int PageViewCount { get; set; }

	public long? LeadId { get; set; }

	/// <summary>Anonymous sessions are deleted after this (Settings, default 90 days). Null once it became a lead.</summary>
	public DateTime? DeleteAfterUtc { get; set; }

	public long? CreatedBy { get; set; }

	public DateTime CreatedDate { get; set; }

	public long? ModifiedBy { get; set; }

	public DateTime? ModifiedDate { get; set; }
}

/// <summary>TRN_CrmPageView: one row per page shown.</summary>
public class CrmPageView
{
	public long PageViewId { get; set; }

	public long VisitorId { get; set; }

	public string Path { get; set; } = string.Empty;

	public DateTime ViewedUtc { get; set; }

	public int? SecondsOnPage { get; set; }

	public int VisitNumber { get; set; }
}

/// <summary>TRN_CrmWhatsAppRef: the W-1234 code written into the pre-filled WhatsApp message.</summary>
public class CrmWhatsAppRef
{
	public long WhatsAppRefId { get; set; }

	public string Code { get; set; } = string.Empty;

	public long VisitorId { get; set; }

	public string? PagePath { get; set; }

	public DateTime CreatedUtc { get; set; }

	public long? LeadId { get; set; }
}

/// <summary>TRN_CrmNotification: in-app CRM alerts and the log of every email CRM tried to send.</summary>
public class CrmNotification
{
	public long NotificationId { get; set; }

	public long? LeadId { get; set; }

	/// <summary>lead.new, lead.assigned, response.nudge, response.escalated, followup.due, digest, ack, lead.unassigned</summary>
	public string Kind { get; set; } = string.Empty;

	public long? RecipientUserId { get; set; }

	public string? RecipientAddress { get; set; }

	/// <summary>inapp or email.</summary>
	public string Channel { get; set; } = "inapp";

	public string Title { get; set; } = string.Empty;

	public string? Body { get; set; }

	public DateTime CreatedUtc { get; set; }

	public DateTime? SentUtc { get; set; }

	public bool Delivered { get; set; }

	public string? Error { get; set; }

	public DateTime? ReadUtc { get; set; }
}

/// <summary>MST_CrmSource. A source is never deleted, only switched off.</summary>
public class CrmSource : AuditableEntity
{
	public int SourceId { get; set; }

	public string Code { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	/// <summary>Set by the website (true) or chosen by hand (false).</summary>
	public bool IsWebsite { get; set; }

	/// <summary>Matching utm_source values, comma separated (e.g. "instagram,ig").</summary>
	public string? UtmSource { get; set; }

	public string? UtmMedium { get; set; }

	/// <summary>Matches the referring site's host (e.g. "google." for Google search), comma separated.</summary>
	public string? ReferrerMatch { get; set; }

	public int SortOrder { get; set; }
}

/// <summary>MST_CrmStatus: the pipeline stages are rows, not values in the code.</summary>
public class CrmStatus : AuditableEntity
{
	public int StatusId { get; set; }

	/// <summary>NEW, CONTACTED, VISIT, QUOTE, WON, LOST for the six built-in rows; CUSTOM_n for added ones.</summary>
	public string Code { get; set; } = string.Empty;

	public string Name { get; set; } = string.Empty;

	public string? Description { get; set; }

	public bool IsClosed { get; set; }

	public bool IsWon { get; set; }

	public CrmStatusRequirement Requires { get; set; }

	/// <summary>A Badge tone: neutral, info, warning, success, danger, gold.</summary>
	public string Colour { get; set; } = "neutral";

	public int SortOrder { get; set; }
}

public class CrmLostReason : AuditableEntity
{
	public int LostReasonId { get; set; }

	public string Name { get; set; } = string.Empty;

	/// <summary>What the reason tells the business (shown on the Lost dialog and report).</summary>
	public string? Hint { get; set; }

	public int SortOrder { get; set; }
}

public class CrmLookingFor : AuditableEntity
{
	public int LookingForId { get; set; }

	public string Name { get; set; } = string.Empty;

	public int? WorkspaceTypeId { get; set; }

	/// <summary>Counts as "a cabin" for assignment rule 3.</summary>
	public bool IsLarge { get; set; }

	public int SortOrder { get; set; }
}

/// <summary>MST_CrmAssignmentRule: tried in SortOrder; the first match wins.</summary>
public class CrmAssignmentRule : AuditableEntity
{
	public int RuleId { get; set; }

	public string Label { get; set; } = string.Empty;

	public CrmRuleCondition ConditionType { get; set; }

	public int? LocationId { get; set; }

	public int? MinPeople { get; set; }

	public int? LookingForId { get; set; }

	public CrmRuleTarget TargetType { get; set; }

	public long? TargetUserId { get; set; }

	public int SortOrder { get; set; }
}

/// <summary>MST_CrmTeamMember: who handles leads, who is on duty, who is a manager.</summary>
public class CrmTeamMember : AuditableEntity
{
	public long UserId { get; set; }

	public bool IsOnDuty { get; set; }

	public bool IsManager { get; set; }

	public int? LocationId { get; set; }

	/// <summary>Round robin: the member assigned longest ago goes next.</summary>
	public DateTime? LastAssignedUtc { get; set; }
}

/// <summary>MST_CrmSetting: key / value.</summary>
public class CrmSetting
{
	public int SettingId { get; set; }

	public string SettingKey { get; set; } = string.Empty;

	public string? SettingValue { get; set; }

	public long? UpdatedBy { get; set; }

	public DateTime UpdatedUtc { get; set; }
}
