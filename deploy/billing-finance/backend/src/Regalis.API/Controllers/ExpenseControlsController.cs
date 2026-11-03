using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance.Billing;
using Regalis.Shared.Api;

namespace Regalis.API.Controllers;

/// <summary>Approval, receipts, department, recurring and reimbursement for expenses (build doc §12).</summary>
[Route("api/expense-controls")]
[StaffOnly]
public class ExpenseControlsController(IExpenseControlService controls) : ApiControllerBase
{
    /// <summary>ids = comma-separated expense ids (the rows on screen).</summary>
    [HttpGet]
    [HasPermission("EXPENSE.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseControlDto[]>), 200)]
    public async Task<IActionResult> GetControls([FromQuery] string ids, CancellationToken ct)
    {
        var list = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => long.TryParse(s, out long v) ? v : 0).Where(v => v > 0).ToList();
        return OkEnvelope(await controls.GetControlsAsync(list, ct));
    }

    /// <summary>The expense register filtered by status and department (doc §12.1).</summary>
    [HttpGet("register")]
    [HasPermission("EXPENSE.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<Regalis.Application.Finance.ExpenseDto>>), 200)]
    public async Task<IActionResult> GetRegister([FromQuery] ExpenseRegisterRequest request, CancellationToken ct) => OkEnvelope(await controls.GetRegisterAsync(request, ct));

    [HttpGet("rules")]
    [HasPermission("EXPENSE.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseRulesDto>), 200)]
    public async Task<IActionResult> GetRules(CancellationToken ct) => OkEnvelope(await controls.GetRulesAsync(ct));

    /// <summary>queue = approval, reimbursement or drafts.</summary>
    [HttpGet("queue")]
    [HasPermission("EXPENSE.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ExpenseQueueItemDto>>), 200)]
    public async Task<IActionResult> GetQueue([FromQuery] ExpenseQueueRequest request, CancellationToken ct) => OkEnvelope(await controls.GetQueueAsync(request, ct));

    [HttpPut("{expenseId:long}")]
    [HasPermission("EXPENSE.EDIT")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseControlDto>), 200)]
    public async Task<IActionResult> Save(long expenseId, SaveExpenseControlRequest request, CancellationToken ct) => OkEnvelope(await controls.SaveControlAsync(expenseId, request, ct), "Saved.");

    [HttpPost("{expenseId:long}/approve")]
    [HasPermission("EXPENSE.APPROVE")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseControlDto>), 200)]
    public async Task<IActionResult> Approve(long expenseId, ExpenseDecisionRequest request, CancellationToken ct) => OkEnvelope(await controls.ApproveAsync(expenseId, request, ct), "Expense approved.");

    [HttpPost("{expenseId:long}/reject")]
    [HasPermission("EXPENSE.APPROVE")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseControlDto>), 200)]
    public async Task<IActionResult> Reject(long expenseId, ExpenseDecisionRequest request, CancellationToken ct) => OkEnvelope(await controls.RejectAsync(expenseId, request, ct), "Expense rejected.");

    [HttpPost("{expenseId:long}/resubmit")]
    [HasPermission("EXPENSE.EDIT")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseControlDto>), 200)]
    public async Task<IActionResult> Resubmit(long expenseId, CancellationToken ct) => OkEnvelope(await controls.ResubmitAsync(expenseId, ct), "Expense resubmitted.");

    [HttpPost("{expenseId:long}/confirm-recurring")]
    [HasPermission("EXPENSE.CREATE")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseControlDto>), 200)]
    public async Task<IActionResult> ConfirmRecurring(long expenseId, ConfirmRecurringRequest request, CancellationToken ct) =>
        OkEnvelope(await controls.ConfirmRecurringAsync(expenseId, request, ct), "Recurring expense confirmed.");

    [HttpPost("{expenseId:long}/reimburse")]
    [HasPermission("EXPENSE.REIMBURSE")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseControlDto>), 200)]
    public async Task<IActionResult> Reimburse(long expenseId, ReimburseRequest request, CancellationToken ct) => OkEnvelope(await controls.ReimburseAsync(expenseId, request, ct), "Reimbursed.");

    /// <summary>PDF, JPG or PNG, up to 10 MB each, five per expense.</summary>
    [HttpPost("{expenseId:long}/receipts")]
    [HasPermission("EXPENSE.EDIT")]
    [RequestSizeLimit(12582912L)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseControlDto>), 200)]
    public async Task<IActionResult> AddReceipt(long expenseId, IFormFile file, CancellationToken ct)
    {
        await using Stream stream = file.OpenReadStream();
        return OkEnvelope(await controls.AddReceiptAsync(expenseId, new AttachmentUploadInput(file.FileName, file.ContentType, file.Length, stream), ct), "Receipt attached.");
    }

    [HttpDelete("{expenseId:long}/receipts/{receiptId:long}")]
    [HasPermission("EXPENSE.EDIT")]
    [ProducesResponseType(typeof(ApiResponse<ExpenseControlDto>), 200)]
    public async Task<IActionResult> RemoveReceipt(long expenseId, long receiptId, CancellationToken ct) => OkEnvelope(await controls.RemoveReceiptAsync(expenseId, receiptId, ct), "Receipt removed.");

    [HttpGet("{expenseId:long}/receipts/{receiptId:long}")]
    [HasPermission("EXPENSE.VIEW")]
    public async Task<IActionResult> GetReceipt(long expenseId, long receiptId, CancellationToken ct)
    {
        AttachmentFileDto file = await controls.GetReceiptAsync(expenseId, receiptId, ct);
        return File(file.Content, file.ContentType, file.FileName);
    }

    [HttpGet("departments")]
    [HasPermission("EXPENSE.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDto[]>), 200)]
    public async Task<IActionResult> GetDepartments([FromQuery] bool includeInactive, CancellationToken ct) => OkEnvelope(await controls.GetDepartmentsAsync(includeInactive, ct));

    [HttpPost("departments")]
    [HasPermission("SETTING.EDIT")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDto>), 200)]
    public async Task<IActionResult> CreateDepartment(SaveDepartmentRequest request, CancellationToken ct) => OkEnvelope(await controls.SaveDepartmentAsync(null, request, ct), "Saved.");

    [HttpPut("departments/{id:int}")]
    [HasPermission("SETTING.EDIT")]
    [ProducesResponseType(typeof(ApiResponse<DepartmentDto>), 200)]
    public async Task<IActionResult> UpdateDepartment(int id, SaveDepartmentRequest request, CancellationToken ct) => OkEnvelope(await controls.SaveDepartmentAsync(id, request, ct), "Saved.");

    [HttpGet("recurring")]
    [HasPermission("EXPENSE.VIEW")]
    [ProducesResponseType(typeof(ApiResponse<RecurringExpenseDto[]>), 200)]
    public async Task<IActionResult> GetRecurring(CancellationToken ct) => OkEnvelope(await controls.GetRecurringAsync(ct));

    [HttpPost("recurring")]
    [HasPermission("EXPENSE.CREATE")]
    [ProducesResponseType(typeof(ApiResponse<RecurringExpenseDto>), 200)]
    public async Task<IActionResult> CreateRecurring(SaveRecurringExpenseRequest request, CancellationToken ct) => OkEnvelope(await controls.SaveRecurringAsync(null, request, ct), "Saved.");

    [HttpPut("recurring/{id:long}")]
    [HasPermission("EXPENSE.CREATE")]
    [ProducesResponseType(typeof(ApiResponse<RecurringExpenseDto>), 200)]
    public async Task<IActionResult> UpdateRecurring(long id, SaveRecurringExpenseRequest request, CancellationToken ct) => OkEnvelope(await controls.SaveRecurringAsync(id, request, ct), "Saved.");

    /// <summary>Raises any recurring drafts due within three days now, instead of waiting for the daily job.</summary>
    [HttpPost("recurring/raise")]
    [HasPermission("EXPENSE.CREATE")]
    [ProducesResponseType(typeof(ApiResponse<int>), 200)]
    public async Task<IActionResult> RaiseRecurring(CancellationToken ct)
    {
        int n = await controls.RaiseRecurringDraftsAsync(ct);
        return OkEnvelope(n, n == 0 ? "Nothing is due yet." : $"{n} draft{(n == 1 ? "" : "s")} raised.");
    }
}

/// <summary>The petty cash float (build doc §12.3).</summary>
[Route("api/petty-cash")]
[StaffOnly]
public class PettyCashController(IPettyCashService pettyCash) : ApiControllerBase
{
    [HttpGet]
    [HasPermission("PETTYCASH.MANAGE")]
    [ProducesResponseType(typeof(ApiResponse<PettyCashOverviewDto>), 200)]
    public async Task<IActionResult> Get([FromQuery] PagedRequest request, CancellationToken ct) => OkEnvelope(await pettyCash.GetOverviewAsync(request, ct));

    [HttpPost("top-up")]
    [HasPermission("PETTYCASH.MANAGE")]
    [ProducesResponseType(typeof(ApiResponse<PettyCashEntryDto>), 200)]
    public async Task<IActionResult> TopUp(PettyCashTopUpRequest request, CancellationToken ct) => OkEnvelope(await pettyCash.TopUpAsync(request, ct), "Top-up recorded.");

    [HttpPost("count")]
    [HasPermission("PETTYCASH.MANAGE")]
    [ProducesResponseType(typeof(ApiResponse<PettyCashEntryDto>), 200)]
    public async Task<IActionResult> Count(PettyCashCountRequest request, CancellationToken ct) => OkEnvelope(await pettyCash.CountAsync(request, ct), "Count recorded.");
}
