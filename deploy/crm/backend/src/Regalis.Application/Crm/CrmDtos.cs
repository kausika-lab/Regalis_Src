using System;
using System.Collections.Generic;
using Regalis.Shared.Api;

namespace Regalis.Application.Crm;

// ---- lookups ----------------------------------------------------------------------------

public record CrmSourceDto(int SourceId, string Code, string Name, bool IsWebsite, string? UtmSource, string? UtmMedium, string? ReferrerMatch, bool IsActive, int SortOrder);

public record CrmStatusDto(int StatusId, string Code, string Name, string? Description, bool IsClosed, bool IsWon, byte Requires, string Colour, bool IsActive, int SortOrder, bool IsBuiltIn);

public record CrmLostReasonDto(int LostReasonId, string Name, string? Hint, bool IsActive, int SortOrder);

public record CrmLookingForDto(int LookingForId, string Name, int? WorkspaceTypeId, bool IsLarge, bool IsActive, int SortOrder);

public record CrmIdName(long Id, string Name);

public record CrmTeamMemberDto(long UserId, string Name, string? Email, bool IsMember, bool IsOnDuty, bool CheckedInToday, bool IsManager, int? LocationId, string Roles);

public record CrmAssignmentRuleDto(int RuleId, string Label, byte ConditionType, int? LocationId, int? MinPeople, int? LookingForId, byte TargetType, long? TargetUserId, bool IsActive, int SortOrder);

public record CrmMeDto(long UserId, string Name, bool CanViewAll, bool CanCreate, bool CanEdit, bool CanAssign, bool CanConvert, bool CanDelete, bool CanSeeVisitors, bool CanSeeReports, bool CanChangeSettings);

public record CrmOptionsDto(
	List<CrmSourceDto> Sources,
	List<CrmStatusDto> Statuses,
	List<CrmLostReasonDto> LostReasons,
	List<CrmLookingForDto> LookingFor,
	List<CrmIdName> Locations,
	List<CrmTeamMemberDto> Team,
	CrmMeDto Me,
	CrmSettingsDto Settings);

// ---- leads list -------------------------------------------------------------------------

public class CrmLeadListRequest
{
	public string? Search { get; set; }
	public int? SourceId { get; set; }
	public int? StatusId { get; set; }
	/// <summary>A user id, or 0 for Unassigned.</summary>
	public long? OwnerId { get; set; }
	public int? LookingForId { get; set; }
	/// <summary>Created in the last N days; 0 = any time. Default 30.</summary>
	public int? Days { get; set; }
	/// <summary>new-week, awaiting, won-month, open (pipeline).</summary>
	public string? Preset { get; set; }
	public string? SortBy { get; set; }
	public bool IsDescending { get; set; } = true;
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 50;
}

public record CrmLeadRowDto(
	long LeadId, string LeadNumber, string Name, string? Company, string Mobile, string? Email,
	int? LookingForId, string? LookingFor, int? PeopleCount, int SourceId, string Source, byte Channel,
	int StatusId, string Status, string StatusColour, bool IsClosed, bool IsWon,
	long? OwnerUserId, string? OwnerName,
	DateTime? NextFollowUpUtc, string? NextFollowUpWhat, int OverdueDays, bool IsOverdue,
	bool Untouched, int? WaitingMinutes, string? LostReason, long? CustomerId, decimal? DealValue,
	DateTime CreatedUtc, DateTime? ClosedUtc, string Urgency);

public record CrmLeadSummaryDto(
	int NewThisWeek, int NewThisWeekWebsite,
	int AwaitingFirstContact, int? OldestAwaitingMinutes,
	int FollowUpDueToday, int FollowUpOverdue,
	int ConvertedThisMonth, decimal ConvertedValue,
	decimal? ConversionRate90, int Won90, int Closed90Leads);

public record CrmLeadListDto(PagedResult<CrmLeadRowDto> Page, CrmLeadSummaryDto Summary);

// ---- one lead ---------------------------------------------------------------------------

public record CrmActivityDto(long ActivityId, byte Kind, string KindName, string Detail, DateTime HappenedUtc, string By, bool IsSystem);

public record CrmFollowUpDto(long FollowUpId, long LeadId, string LeadNumber, string LeadName, string? Company, string? LookingFor,
	DateTime DueUtc, string WhatToDo, string Kind, long? OwnerUserId, string? OwnerName, bool IsDone, string? Outcome, DateTime? DoneUtc,
	bool IsOverdue, int LateMinutes, bool NeverContacted);

public record CrmPageViewDto(string Path, DateTime ViewedUtc, int? SecondsOnPage, int VisitNumber);

public record CrmJourneyDto(long VisitorId, string VisitorCode, DateTime FirstSeenUtc, string? Source, string? Medium, string? Campaign,
	string? Referrer, string? LandingPage, string? FormPage, int VisitsBeforeEnquiring, double DaysConsidering, string? Device, string? Browser, string? City,
	DateTime EnquiredUtc, List<CrmPageViewDto> PageViews);

public record CrmConsentDto(string HowObtained, string? ConsentTextShown, bool ContactConsent, bool MarketingConsent, DateTime? ConsentedUtc, string RetentionText);

public record CrmQuotationLinkDto(long QuotationId, string QuotationNumber, string Status, decimal TotalAmount, DateTime? SentUtc);

public record CrmLeadDetailDto(
	CrmLeadRowDto Row,
	int? LocationId, string? LocationName, DateOnly? NeededFrom, string? BudgetText, string? SourceDetail,
	string? FirstMessage, long? ReferredByCustomerId, string? ReferredByName, DateTime? WalkInUtc, long? ShownByUserId, string? ShownByName,
	int? LostReasonId, string? LostNote, DateTime? ConvertedUtc, string? CustomerName,
	DateTime? TourUtc, DateTime? FirstContactUtc, DateTime? ResponseDueUtc, int? TargetMinutes,
	string EnteredBy, string ChannelName,
	CrmQuotationLinkDto? Quotation, CrmJourneyDto? Journey, CrmConsentDto? Consent,
	List<CrmActivityDto> Activities, List<CrmFollowUpDto> FollowUps);

public class CrmCreateLeadRequest
{
	public int? SourceId { get; set; }
	public string? Name { get; set; }
	public string? Mobile { get; set; }
	public string? Email { get; set; }
	public string? Company { get; set; }
	public DateTime? WalkInUtc { get; set; }
	public long? ShownByUserId { get; set; }
	public long? ReferredByCustomerId { get; set; }
	public int? LookingForId { get; set; }
	public int? PeopleCount { get; set; }
	public string? BudgetText { get; set; }
	public int? LocationId { get; set; }
	public DateOnly? NeededFrom { get; set; }
	public string? HeardFrom { get; set; }
	public string? WhatTheySaid { get; set; }
	/// <summary>"me", "rules", or a user id as text.</summary>
	public string? AssignTo { get; set; }
	public DateTime? FollowUpUtc { get; set; }
	public string? FollowUpWhat { get; set; }
	/// <summary>Staff pressed "It is a different person" on the duplicate panel.</summary>
	public bool AllowDuplicate { get; set; }
	/// <summary>The W-1234 code from a WhatsApp message: attaches that visitor's history.</summary>
	public string? WhatsAppRef { get; set; }
	public decimal? DealValue { get; set; }
}

public class CrmUpdateLeadRequest
{
	public string? Name { get; set; }
	public string? Mobile { get; set; }
	public string? Email { get; set; }
	public string? Company { get; set; }
	public int? LookingForId { get; set; }
	public int? PeopleCount { get; set; }
	public string? BudgetText { get; set; }
	public int? LocationId { get; set; }
	public DateOnly? NeededFrom { get; set; }
	public string? SourceDetail { get; set; }
	public decimal? DealValue { get; set; }
}

public record CrmDuplicateLeadDto(long LeadId, string LeadNumber, string Name, string? Company, DateTime CreatedUtc, string HowCreated, string? OwnerName, string Status, bool IsClosed, string MatchedOn);

public record CrmDuplicateCustomerDto(long CustomerId, string Name, string? Company, string? Mobile, string? Email, string MatchedOn);

public record CrmDuplicateResultDto(List<CrmDuplicateLeadDto> Leads, List<CrmDuplicateCustomerDto> Customers);

public class CrmCheckDuplicateRequest
{
	public string? Mobile { get; set; }
	public string? Email { get; set; }
	public long? ExcludeLeadId { get; set; }
}

public class CrmActivityRequest
{
	/// <summary>call, email, whatsapp, meeting, note.</summary>
	public string? Kind { get; set; }
	public string? Detail { get; set; }
	public DateTime? HappenedUtc { get; set; }
}

public class CrmStatusRequest
{
	public int StatusId { get; set; }
	public int? LostReasonId { get; set; }
	public string? LostNote { get; set; }
	public DateTime? TourUtc { get; set; }
}

public class CrmAssignRequest
{
	public long? UserId { get; set; }
	/// <summary>Assign even though the person is not on duty.</summary>
	public bool Force { get; set; }
}

public class CrmTourRequest
{
	public DateTime? TourUtc { get; set; }
	public string? Note { get; set; }
}

public class CrmLinkQuotationRequest
{
	public long? QuotationId { get; set; }
	public string? QuotationNumber { get; set; }
}

public class CrmFollowUpRequest
{
	public DateTime? DueUtc { get; set; }
	public string? WhatToDo { get; set; }
	public string? Kind { get; set; }
	public long? OwnerUserId { get; set; }
}

public class CrmFollowUpDoneRequest
{
	/// <summary>spoke, no-answer, left-message, sent-email, other.</summary>
	public string? Outcome { get; set; }
	public string? Note { get; set; }
	public CrmFollowUpRequest? Next { get; set; }
}

public class CrmLoseRequest
{
	public int? LostReasonId { get; set; }
	public string? Note { get; set; }
}

public class CrmReopenRequest
{
	public string? Reason { get; set; }
}

public class CrmConvertRequest
{
	public string? Name { get; set; }
	public string? Company { get; set; }
	public string? Mobile { get; set; }
	public string? Email { get; set; }
	/// <summary>individual or company.</summary>
	public string? CustomerType { get; set; }
	public bool CreateCustomer { get; set; } = true;
	public bool CarryQuotation { get; set; } = true;
}

public record CrmConvertPreviewDto(CrmDuplicateCustomerDto? ExistingCustomer, string? AlreadyConvertedMessage, CrmQuotationLinkDto? Quotation);

public record CrmConvertResultDto(long CustomerId, string CustomerName, bool UsedExistingCustomer, bool QuotationCarried, string Message);

// ---- follow-ups ------------------------------------------------------------------------

public record CrmFollowUpBoardDto(
	List<CrmFollowUpDto> Overdue, List<CrmFollowUpDto> Today, List<CrmFollowUpDto> Week,
	int OverdueCount, int? OldestOverdueMinutes, int DueToday, int DueTodayCalls, int DueTodayEmails,
	int Next7, int Next7Owners, int? AverageFirstReplyMinutes, int TargetMinutes,
	List<CrmNotificationDto> Alerts);

public record CrmNotificationDto(long NotificationId, long? LeadId, string Kind, string Title, string? Body, DateTime CreatedUtc, bool IsRead);

// ---- visitors --------------------------------------------------------------------------

public class CrmVisitorListRequest
{
	public int? Days { get; set; }
	public int? SourceId { get; set; }
	/// <summary>yes, no or empty.</summary>
	public string? Enquired { get; set; }
	public int Page { get; set; } = 1;
	public int PageSize { get; set; } = 50;
}

public record CrmVisitorRowDto(long VisitorId, string VisitorCode, DateTime FirstSeenUtc, DateTime LastSeenUtc, string CameFrom, string? LandingPage,
	int Pages, string? City, string? Device, int Visits, long? LeadId, string? LeadName, bool Highlight);

public record CrmSourceVisitsDto(int? SourceId, string Source, int Visits, int Enquiries);

public record CrmVisitorSummaryDto(int Visits, int Devices, int BecameLeads, decimal? EnquiryRate, string? TopPage, int TopPageViews, int Returning);

public record CrmVisitorListDto(CrmVisitorSummaryDto Summary, List<CrmSourceVisitsDto> Sources, PagedResult<CrmVisitorRowDto> Page);

public record CrmVisitorDetailDto(CrmVisitorRowDto Row, string? UtmSource, string? UtmMedium, string? UtmCampaign, string? Referrer, string? Browser, List<CrmPageViewDto> PageViews);

// ---- reports ---------------------------------------------------------------------------

public class CrmReportRequest
{
	public DateOnly? From { get; set; }
	public DateOnly? To { get; set; }
}

public record CrmSourceReportRow(string Source, int Leads, int Won, decimal Conversion, decimal RevenueWon, string Advice);

public record CrmFunnelRow(string Step, string Basis, int Count, decimal? LostPercent);

public record CrmResponseRow(string Band, int Leads, int Won, decimal WonPercent);

public record CrmOwnerRow(long? UserId, string Owner, int Leads, int RepliedInTime, decimal? RepliedInTimePercent, int Visits, int Won, int Lost, double? AvgDaysToDecision);

public record CrmReportDto<T>(DateOnly From, DateOnly To, List<T> Rows);

// ---- settings --------------------------------------------------------------------------

public record CrmSettingsDto(
	int TargetWebMinutes, int TargetWhatsAppMinutes, int TargetCallbackMinutes, int TargetEmailMinutes, int EscalateMinutes,
	string WorkStart, string WorkEnd, List<int> WorkDays,
	string ConsentText, string MarketingText, int VisitRetentionDays, int LostRetentionMonths,
	bool EmailEnabled, bool AckEmailEnabled, bool DigestEnabled, string? WhatsAppNumber, string? PublicPhone, string? PrivacyUrl,
	DateTime? EmailTestedUtc, string? EmailTestedTo);

public class CrmSaveListItemRequest
{
	public string? Name { get; set; }
	public string? Code { get; set; }
	public string? Hint { get; set; }
	public string? Description { get; set; }
	public string? Colour { get; set; }
	public bool? IsActive { get; set; }
	public bool? IsWebsite { get; set; }
	public string? UtmSource { get; set; }
	public string? UtmMedium { get; set; }
	public string? ReferrerMatch { get; set; }
	public int? WorkspaceTypeId { get; set; }
	public bool? IsLarge { get; set; }
	public int? SortOrder { get; set; }
}

public class CrmSaveRuleRequest
{
	public string? Label { get; set; }
	public byte ConditionType { get; set; }
	public int? LocationId { get; set; }
	public int? MinPeople { get; set; }
	public int? LookingForId { get; set; }
	public byte TargetType { get; set; }
	public long? TargetUserId { get; set; }
	public bool IsActive { get; set; } = true;
}

public class CrmSaveTeamMemberRequest
{
	public bool IsMember { get; set; }
	public bool IsOnDuty { get; set; }
	public bool IsManager { get; set; }
	public int? LocationId { get; set; }
}

public class CrmTestEmailRequest
{
	public string? To { get; set; }
}

// ---- public website --------------------------------------------------------------------

public record CrmPublicConfigDto(string ConsentText, string MarketingText, string? WhatsAppNumber, string? PublicPhone, string? PrivacyUrl, List<CrmIdName> LookingFor);

public class CrmPublicVisitRequest
{
	public string? VisitorKey { get; set; }
	public string? Path { get; set; }
	public string? Referrer { get; set; }
	public string? UtmSource { get; set; }
	public string? UtmMedium { get; set; }
	public string? UtmCampaign { get; set; }
	/// <summary>True on the first page of a browser session (sessionStorage empty).</summary>
	public bool NewVisit { get; set; }
	/// <summary>When set, only records the seconds spent on that earlier page view.</summary>
	public long? PageViewId { get; set; }
	public int? Seconds { get; set; }
}

public record CrmPublicVisitResultDto(string VisitorKey, long? PageViewId);

public class CrmPublicEnquiryRequest
{
	/// <summary>form, callback or quote.</summary>
	public string? Kind { get; set; }
	public string? Name { get; set; }
	public string? Mobile { get; set; }
	public string? Email { get; set; }
	public int? LookingForId { get; set; }
	public int? People { get; set; }
	public DateOnly? FromWhen { get; set; }
	public string? Message { get; set; }
	public bool ContactConsent { get; set; }
	public bool MarketingConsent { get; set; }
	public string? VisitorKey { get; set; }
	public string? PagePath { get; set; }
	public string? Referrer { get; set; }
	public string? UtmSource { get; set; }
	public string? UtmMedium { get; set; }
	public string? UtmCampaign { get; set; }
	/// <summary>Honeypot: a hidden field people never see. Filled in = a bot.</summary>
	public string? Website { get; set; }
	/// <summary>Milliseconds between the form appearing and being sent. Under 2.5 s = a bot.</summary>
	public int? ElapsedMs { get; set; }
}

public class CrmPublicWhatsAppRequest
{
	public string? VisitorKey { get; set; }
	public string? Path { get; set; }
}

public record CrmPublicWhatsAppDto(string Code, string Message, string? Url);
