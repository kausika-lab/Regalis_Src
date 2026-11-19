using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Regalis.Application.Crm;

/// <summary>The admin side of CRM: leads, follow-ups, visitors, reports and settings.</summary>
public interface ICrmService
{
	Task<CrmOptionsDto> GetOptionsAsync(CancellationToken ct = default);

	Task<CrmLeadListDto> ListLeadsAsync(CrmLeadListRequest request, CancellationToken ct = default);

	Task<byte[]> ExportLeadsAsync(CrmLeadListRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> GetLeadAsync(long leadId, CancellationToken ct = default);

	Task<CrmDuplicateResultDto> CheckDuplicateAsync(CrmCheckDuplicateRequest request, CancellationToken ct = default);

	/// <summary>Returns the duplicates instead of creating when one matches and AllowDuplicate is false.</summary>
	Task<(CrmLeadDetailDto? Lead, CrmDuplicateResultDto? Duplicates)> CreateLeadAsync(CrmCreateLeadRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> UpdateLeadAsync(long leadId, CrmUpdateLeadRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> AddActivityAsync(long leadId, CrmActivityRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> ChangeStatusAsync(long leadId, CrmStatusRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> AssignAsync(long leadId, CrmAssignRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> TakeAsync(long leadId, CancellationToken ct = default);

	Task<CrmLeadDetailDto> BookTourAsync(long leadId, CrmTourRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> LinkQuotationAsync(long leadId, CrmLinkQuotationRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> AddFollowUpAsync(long leadId, CrmFollowUpRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> CompleteFollowUpAsync(long followUpId, CrmFollowUpDoneRequest request, CancellationToken ct = default);

	Task<CrmFollowUpBoardDto> GetFollowUpsAsync(string? scope, long? ownerId, CancellationToken ct = default);

	Task<CrmConvertPreviewDto> PreviewConvertAsync(long leadId, CancellationToken ct = default);

	Task<CrmConvertResultDto> ConvertAsync(long leadId, CrmConvertRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> LoseAsync(long leadId, CrmLoseRequest request, CancellationToken ct = default);

	Task<CrmLeadDetailDto> ReopenAsync(long leadId, CrmReopenRequest request, CancellationToken ct = default);

	Task DeleteLeadAsync(long leadId, CancellationToken ct = default);

	Task<List<CrmNotificationDto>> GetMyNotificationsAsync(CancellationToken ct = default);

	Task MarkNotificationsReadAsync(CancellationToken ct = default);

	Task<List<CrmDuplicateCustomerDto>> SearchCustomersAsync(string? search, CancellationToken ct = default);

	Task<CrmVisitorListDto> ListVisitorsAsync(CrmVisitorListRequest request, CancellationToken ct = default);

	Task<CrmVisitorDetailDto> GetVisitorAsync(long visitorId, CancellationToken ct = default);

	Task<CrmReportDto<CrmSourceReportRow>> SourceReportAsync(CrmReportRequest request, CancellationToken ct = default);

	Task<CrmReportDto<CrmFunnelRow>> FunnelReportAsync(CrmReportRequest request, CancellationToken ct = default);

	Task<CrmReportDto<CrmResponseRow>> ResponseReportAsync(CrmReportRequest request, CancellationToken ct = default);

	Task<CrmReportDto<CrmOwnerRow>> OwnerReportAsync(CrmReportRequest request, CancellationToken ct = default);

	Task<byte[]> ExportReportsAsync(CrmReportRequest request, CancellationToken ct = default);

	Task<CrmSettingsDto> GetSettingsAsync(CancellationToken ct = default);

	Task<CrmSettingsDto> SaveSettingsAsync(CrmSettingsDto settings, CancellationToken ct = default);

	Task<string> SendTestEmailAsync(CrmTestEmailRequest request, CancellationToken ct = default);

	Task<object> SaveListItemAsync(string list, int? id, CrmSaveListItemRequest request, CancellationToken ct = default);

	Task<CrmAssignmentRuleDto> SaveRuleAsync(int? ruleId, CrmSaveRuleRequest request, CancellationToken ct = default);

	Task ReorderAsync(string list, List<int> ids, CancellationToken ct = default);

	Task<CrmTeamMemberDto> SaveTeamMemberAsync(long userId, CrmSaveTeamMemberRequest request, CancellationToken ct = default);

	Task<List<CrmAssignmentRuleDto>> GetRulesAsync(CancellationToken ct = default);
}

/// <summary>The three endpoints myregalis.com calls without a login. Treat every input as hostile.</summary>
public interface ICrmPublicService
{
	Task<CrmPublicConfigDto> GetConfigAsync(CancellationToken ct = default);

	Task<CrmPublicVisitResultDto> RecordVisitAsync(CrmPublicVisitRequest request, string? ipAddress, string? userAgent, string? cityHint, string? countryHint, CancellationToken ct = default);

	/// <summary>Never says whether the number exists, and never says a bot was detected.</summary>
	Task SubmitEnquiryAsync(CrmPublicEnquiryRequest request, string? ipAddress, string? userAgent, string? cityHint, string? countryHint, CancellationToken ct = default);

	Task<CrmPublicWhatsAppDto> IssueWhatsAppRefAsync(CrmPublicWhatsAppRequest request, string? ipAddress, string? userAgent, CancellationToken ct = default);
}
