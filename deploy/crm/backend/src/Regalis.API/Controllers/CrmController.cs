using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Regalis.API.Authorization;
using Regalis.Application.Crm;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>
/// CRM — Leads and Web Enquiries, admin portal (document section 14.1). Permissions (section 15):
/// CRM.VIEW opens the module; CREATE / EDIT / ASSIGN / CONVERT / DELETE gate the actions;
/// CRM.LEAD.VIEWALL widens "own + unassigned" to every lead (checked in the service).
/// </summary>
[Route("api/crm")]
public class CrmController(ICrmService crm) : ApiControllerBase
{
	private const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

	[HttpGet("options")]
	[HasPermission("CRM.VIEW")]
	public async Task<IActionResult> Options(CancellationToken ct) => OkEnvelope(await crm.GetOptionsAsync(ct));

	// ---- leads ------------------------------------------------------------------------------

	[HttpGet("leads")]
	[HasPermission("CRM.VIEW")]
	public async Task<IActionResult> Leads([FromQuery] CrmLeadListRequest request, CancellationToken ct) => OkEnvelope(await crm.ListLeadsAsync(request, ct));

	[HttpGet("leads/export")]
	[HasPermission("CRM.VIEW")]
	public async Task<IActionResult> Export([FromQuery] CrmLeadListRequest request, CancellationToken ct) =>
		File(await crm.ExportLeadsAsync(request, ct), Xlsx, $"crm_leads_{DateTime.UtcNow.AddMinutes(330):yyyy-MM-dd_HHmm}.xlsx");

	[HttpPost("leads")]
	[HasPermission("CRM.LEAD.CREATE")]
	public async Task<IActionResult> Create(CrmCreateLeadRequest request, CancellationToken ct)
	{
		var (lead, duplicates) = await crm.CreateLeadAsync(request, ct);
		if (duplicates != null)
		{
			return StatusCode(409, new ApiResponse<CrmDuplicateResultDto>
			{
				Success = false,
				Message = duplicates.Leads.Count > 0 ? "We already have this number or email on a lead." : "This is already a customer.",
				Data = duplicates,
				Errors = new[] { new ApiError("DUPLICATE_LEAD", "A matching lead or customer exists.") }
			});
		}
		return OkEnvelope(lead!, $"Lead {lead!.Row.LeadNumber} created.");
	}

	[HttpPost("leads/check-duplicate")]
	[HasPermission("CRM.VIEW")]
	public async Task<IActionResult> CheckDuplicate(CrmCheckDuplicateRequest request, CancellationToken ct) => OkEnvelope(await crm.CheckDuplicateAsync(request, ct));

	[HttpGet("leads/{id:long}")]
	[HasPermission("CRM.VIEW")]
	public async Task<IActionResult> Get(long id, CancellationToken ct) => OkEnvelope(await crm.GetLeadAsync(id, ct));

	[HttpPut("leads/{id:long}")]
	[HasPermission("CRM.LEAD.EDIT")]
	public async Task<IActionResult> Update(long id, CrmUpdateLeadRequest request, CancellationToken ct) => OkEnvelope(await crm.UpdateLeadAsync(id, request, ct), "Lead saved.");

	[HttpPost("leads/{id:long}/activities")]
	[HasPermission("CRM.LEAD.EDIT")]
	public async Task<IActionResult> Activity(long id, CrmActivityRequest request, CancellationToken ct) => OkEnvelope(await crm.AddActivityAsync(id, request, ct), "Added to the activity log.");

	[HttpPut("leads/{id:long}/activities/{activityId:long}")]
	[HttpDelete("leads/{id:long}/activities/{activityId:long}")]
	[HasPermission("CRM.VIEW")]
	public IActionResult ActivityEdit(long id, long activityId) =>
		UnprocessableEntity(ApiResponse.Fail("The activity log cannot be edited. Add a new entry with the correction.", "ACTIVITY_APPEND_ONLY", "The activity log is append-only."));

	[HttpPost("leads/{id:long}/status")]
	[HasPermission("CRM.LEAD.EDIT")]
	public async Task<IActionResult> Status(long id, CrmStatusRequest request, CancellationToken ct) => OkEnvelope(await crm.ChangeStatusAsync(id, request, ct), "Status changed.");

	[HttpPost("leads/{id:long}/assign")]
	[HasPermission("CRM.LEAD.ASSIGN")]
	public async Task<IActionResult> Assign(long id, CrmAssignRequest request, CancellationToken ct) => OkEnvelope(await crm.AssignAsync(id, request, ct), "Owner changed.");

	[HttpPost("leads/{id:long}/take")]
	[HasPermission("CRM.LEAD.EDIT")]
	public async Task<IActionResult> Take(long id, CancellationToken ct) => OkEnvelope(await crm.TakeAsync(id, ct), "The lead is yours.");

	[HttpPost("leads/{id:long}/tour")]
	[HasPermission("CRM.LEAD.EDIT")]
	public async Task<IActionResult> Tour(long id, CrmTourRequest request, CancellationToken ct) => OkEnvelope(await crm.BookTourAsync(id, request, ct), "Tour booked.");

	[HttpPost("leads/{id:long}/quotation")]
	[HasPermission("CRM.LEAD.EDIT")]
	public async Task<IActionResult> Quotation(long id, CrmLinkQuotationRequest request, CancellationToken ct) => OkEnvelope(await crm.LinkQuotationAsync(id, request, ct), "Quotation linked.");

	[HttpPost("leads/{id:long}/follow-ups")]
	[HasPermission("CRM.LEAD.EDIT")]
	public async Task<IActionResult> FollowUp(long id, CrmFollowUpRequest request, CancellationToken ct) => OkEnvelope(await crm.AddFollowUpAsync(id, request, ct), "Follow-up set.");

	[HttpGet("leads/{id:long}/convert")]
	[HasPermission("CRM.LEAD.CONVERT")]
	public async Task<IActionResult> ConvertPreview(long id, CancellationToken ct) => OkEnvelope(await crm.PreviewConvertAsync(id, ct));

	[HttpPost("leads/{id:long}/convert")]
	[HasPermission("CRM.LEAD.CONVERT")]
	public async Task<IActionResult> Convert(long id, CrmConvertRequest request, CancellationToken ct)
	{
		CrmConvertResultDto result = await crm.ConvertAsync(id, request, ct);
		return OkEnvelope(result, result.Message);
	}

	[HttpPost("leads/{id:long}/lose")]
	[HasPermission("CRM.LEAD.EDIT")]
	public async Task<IActionResult> Lose(long id, CrmLoseRequest request, CancellationToken ct) => OkEnvelope(await crm.LoseAsync(id, request, ct), "Closed as lost.");

	[HttpPost("leads/{id:long}/reopen")]
	[HasPermission("CRM.LEAD.EDIT")]
	public async Task<IActionResult> Reopen(long id, CrmReopenRequest request, CancellationToken ct) => OkEnvelope(await crm.ReopenAsync(id, request, ct), "Lead reopened.");

	[HttpDelete("leads/{id:long}")]
	[HasPermission("CRM.LEAD.DELETE")]
	public async Task<IActionResult> Delete(long id, CancellationToken ct)
	{
		await crm.DeleteLeadAsync(id, ct);
		return OkEnvelope("The lead and everything attached to it have been deleted.");
	}

	[HttpGet("customers")]
	[HasPermission("CRM.VIEW")]
	public async Task<IActionResult> Customers([FromQuery] string? search, CancellationToken ct) => OkEnvelope(await crm.SearchCustomersAsync(search, ct));

	// ---- follow-ups and alerts -----------------------------------------------------------------

	[HttpGet("follow-ups")]
	[HasPermission("CRM.VIEW")]
	public async Task<IActionResult> FollowUps([FromQuery] string? scope, [FromQuery] long? ownerId, CancellationToken ct) => OkEnvelope(await crm.GetFollowUpsAsync(scope, ownerId, ct));

	[HttpPost("follow-ups/{id:long}/done")]
	[HasPermission("CRM.LEAD.EDIT")]
	public async Task<IActionResult> Done(long id, CrmFollowUpDoneRequest request, CancellationToken ct) => OkEnvelope(await crm.CompleteFollowUpAsync(id, request, ct), "Follow-up done.");

	[HttpGet("notifications")]
	[HasPermission("CRM.VIEW")]
	public async Task<IActionResult> Notifications(CancellationToken ct) => OkEnvelope(await crm.GetMyNotificationsAsync(ct));

	[HttpPost("notifications/read")]
	[HasPermission("CRM.VIEW")]
	public async Task<IActionResult> NotificationsRead(CancellationToken ct)
	{
		await crm.MarkNotificationsReadAsync(ct);
		return OkEnvelope();
	}

	// ---- visitors ---------------------------------------------------------------------------

	[HttpGet("visitors")]
	[HasPermission("CRM.VISITORS.VIEW")]
	public async Task<IActionResult> Visitors([FromQuery] CrmVisitorListRequest request, CancellationToken ct) => OkEnvelope(await crm.ListVisitorsAsync(request, ct));

	[HttpGet("visitors/{id:long}")]
	[HasPermission("CRM.VISITORS.VIEW")]
	public async Task<IActionResult> Visitor(long id, CancellationToken ct) => OkEnvelope(await crm.GetVisitorAsync(id, ct));

	// ---- reports ----------------------------------------------------------------------------

	[HttpGet("reports/sources")]
	[HasPermission("CRM.REPORTS.VIEW")]
	public async Task<IActionResult> Sources([FromQuery] CrmReportRequest request, CancellationToken ct) => OkEnvelope(await crm.SourceReportAsync(request, ct));

	[HttpGet("reports/funnel")]
	[HasPermission("CRM.REPORTS.VIEW")]
	public async Task<IActionResult> Funnel([FromQuery] CrmReportRequest request, CancellationToken ct) => OkEnvelope(await crm.FunnelReportAsync(request, ct));

	[HttpGet("reports/response-times")]
	[HasPermission("CRM.REPORTS.VIEW")]
	public async Task<IActionResult> ResponseTimes([FromQuery] CrmReportRequest request, CancellationToken ct) => OkEnvelope(await crm.ResponseReportAsync(request, ct));

	[HttpGet("reports/owners")]
	[HasPermission("CRM.REPORTS.VIEW")]
	public async Task<IActionResult> Owners([FromQuery] CrmReportRequest request, CancellationToken ct) => OkEnvelope(await crm.OwnerReportAsync(request, ct));

	[HttpGet("reports/export")]
	[HasPermission("CRM.REPORTS.VIEW")]
	public async Task<IActionResult> ReportsExport([FromQuery] CrmReportRequest request, CancellationToken ct) =>
		File(await crm.ExportReportsAsync(request, ct), Xlsx, $"crm_reports_{DateTime.UtcNow.AddMinutes(330):yyyy-MM-dd_HHmm}.xlsx");

	// ---- settings ---------------------------------------------------------------------------

	[HttpGet("settings")]
	[HasPermission("CRM.SETTINGS")]
	public async Task<IActionResult> Settings(CancellationToken ct) => OkEnvelope(await crm.GetSettingsAsync(ct));

	[HttpPut("settings")]
	[HasPermission("CRM.SETTINGS")]
	public async Task<IActionResult> SaveSettings(CrmSettingsDto settings, CancellationToken ct) => OkEnvelope(await crm.SaveSettingsAsync(settings, ct), "CRM settings saved.");

	[HttpPost("settings/test-email")]
	[HasPermission("CRM.SETTINGS")]
	public async Task<IActionResult> TestEmail(CrmTestEmailRequest request, CancellationToken ct) => OkEnvelope(await crm.SendTestEmailAsync(request, ct));

	[HttpGet("assignment-rules")]
	[HasPermission("CRM.SETTINGS")]
	public async Task<IActionResult> Rules(CancellationToken ct) => OkEnvelope(await crm.GetRulesAsync(ct));

	[HttpPost("assignment-rules")]
	[HasPermission("CRM.SETTINGS")]
	public async Task<IActionResult> AddRule(CrmSaveRuleRequest request, CancellationToken ct) => OkEnvelope(await crm.SaveRuleAsync(null, request, ct), "Rule added.");

	[HttpPut("assignment-rules/{id:int}")]
	[HasPermission("CRM.SETTINGS")]
	public async Task<IActionResult> SaveRule(int id, CrmSaveRuleRequest request, CancellationToken ct) => OkEnvelope(await crm.SaveRuleAsync(id, request, ct), "Rule saved.");

	[HttpPut("team/{userId:long}")]
	[HasPermission("CRM.SETTINGS")]
	public async Task<IActionResult> SaveTeam(long userId, CrmSaveTeamMemberRequest request, CancellationToken ct) => OkEnvelope(await crm.SaveTeamMemberAsync(userId, request, ct), "Saved.");

	/// <summary>sources, statuses, lost-reasons, looking-for, assignment-rules.</summary>
	[HttpPut("{list}/order")]
	[HasPermission("CRM.SETTINGS")]
	public async Task<IActionResult> Reorder(string list, [FromBody] List<int> ids, CancellationToken ct)
	{
		await crm.ReorderAsync(list, ids, ct);
		return OkEnvelope("Order saved.");
	}

	[HttpPost("{list:regex(^(sources|statuses|lost-reasons|looking-for)$)}")]
	[HasPermission("CRM.SETTINGS")]
	public async Task<IActionResult> AddItem(string list, CrmSaveListItemRequest request, CancellationToken ct) => OkEnvelope(await crm.SaveListItemAsync(list, null, request, ct), "Added.");

	[HttpPut("{list:regex(^(sources|statuses|lost-reasons|looking-for)$)}/{id:int}")]
	[HasPermission("CRM.SETTINGS")]
	public async Task<IActionResult> SaveItem(string list, int id, CrmSaveListItemRequest request, CancellationToken ct) => OkEnvelope(await crm.SaveListItemAsync(list, id, request, ct), "Saved.");
}

/// <summary>
/// The CRM endpoints myregalis.com calls without a login (document section 14.2). Rate-limited
/// silently in the service; the global per-address write limit still applies on top.
/// </summary>
[Route("api/public")]
[AllowAnonymous]
public class CrmPublicController(ICrmPublicService crm, Microsoft.Extensions.Logging.ILogger<CrmPublicController> logger) : ApiControllerBase
{
	private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();

	private string? Agent => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? (ua.Length > 400 ? ua.Substring(0, 400) : ua) : null;

	/// <summary>A city only when the hosting edge supplies one in a header; never guessed.</summary>
	private string? City => FirstHeader("CF-IPCity", "X-Geo-City", "X-AppEngine-City");

	private string? Country => FirstHeader("CF-IPCountry", "X-Geo-Country", "X-AppEngine-Country");

	private string? FirstHeader(params string[] names)
	{
		foreach (string n in names)
		{
			string v = Request.Headers[n].ToString();
			if (!string.IsNullOrWhiteSpace(v))
			{
				return v.Length > 100 ? v.Substring(0, 100) : v;
			}
		}
		return null;
	}

	[HttpGet("crm-config")]
	public async Task<IActionResult> Config(CancellationToken ct) => OkEnvelope(await crm.GetConfigAsync(ct));

	[HttpPost("visits")]
	public async Task<IActionResult> Visit(CrmPublicVisitRequest request, CancellationToken ct) => OkEnvelope(await crm.RecordVisitAsync(request, Ip, Agent, City, Country, ct));

	[HttpPost("enquiries")]
	public async Task<IActionResult> Enquiry(CrmPublicEnquiryRequest request, CancellationToken ct)
	{
		try
		{
			await crm.SubmitEnquiryAsync(request, Ip, Agent, City, Country, ct);
		}
		catch (Regalis.Application.Common.Exceptions.BusinessRuleException)
		{
			throw;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			logger.LogError(ex, "CRM public enquiry could not be saved");
			return StatusCode(500, ApiResponse.Fail("Something went wrong at our end. Please call us — we would rather you reached us than tried again.", "SERVER_ERROR", "The enquiry could not be saved."));
		}
		return OkEnvelope("Thanks — we have your enquiry and we will call you back today.");
	}

	[HttpPost("whatsapp-ref")]
	public async Task<IActionResult> WhatsAppRef(CrmPublicWhatsAppRequest request, CancellationToken ct) => OkEnvelope(await crm.IssueWhatsAppRefAsync(request, Ip, Agent, ct));
}
