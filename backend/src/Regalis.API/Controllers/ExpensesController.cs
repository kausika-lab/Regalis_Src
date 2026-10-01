using Microsoft.AspNetCore.Mvc;
using Regalis.API.Authorization;
using Regalis.Application.Finance;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.API.Controllers;

/// <summary>Daily company expenses (document §5). Admin-only throughout: no customer
/// role holds EXPENSE.*, so the customer portal cannot reach any of these routes.</summary>
[Route("api/expenses")]
public class ExpensesController(IExpenseService expenseService) : ApiControllerBase
{
    [HttpGet]
    [HasPermission(PermissionCodes.Expense.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ExpenseDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll([FromQuery] ExpenseListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await expenseService.GetExpensesAsync(request, ct));

    [HttpGet("{id:long}")]
    [HasPermission(PermissionCodes.Expense.View)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(long id, CancellationToken ct) =>
        OkEnvelope(await expenseService.GetExpenseAsync(id, ct));

    /// <summary>Totals for the list header and the finance dashboard card.</summary>
    [HttpGet("summary")]
    [HasPermission(PermissionCodes.Expense.View)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary([FromQuery] ExpenseListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await expenseService.GetSummaryAsync(request, ct));

    /// <summary>Categories already in use, for the entry screen's combo.</summary>
    [HttpGet("categories")]
    [HasPermission(PermissionCodes.Expense.View)]
    [ProducesResponseType(typeof(ApiResponse<List<string>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(CancellationToken ct) =>
        OkEnvelope(await expenseService.GetCategoriesAsync(ct));

    [HttpPost]
    [HasPermission(PermissionCodes.Expense.Create)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(SaveExpenseRequest request, CancellationToken ct)
    {
        var expense = await expenseService.CreateAsync(request, ct);
        return CreatedEnvelope($"/api/expenses/{expense.ExpenseId}", expense, "Expense recorded.");
    }

    [HttpPut("{id:long}")]
    [HasPermission(PermissionCodes.Expense.Edit)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update(long id, SaveExpenseRequest request,
        CancellationToken ct) =>
        OkEnvelope(await expenseService.UpdateAsync(id, request, ct), "Expense updated.");

    [HttpDelete("{id:long}")]
    [HasPermission(PermissionCodes.Expense.Delete)]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await expenseService.DeleteAsync(id, ct);
        return OkEnvelope("Expense deleted.");
    }

    [HttpGet("export")]
    [HasPermission(PermissionCodes.Expense.Export)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export([FromQuery] ExpenseListRequest request,
        CancellationToken ct)
    {
        var export = await expenseService.ExportAsync(request, ct);
        return File(export.Content, export.ContentType, export.FileName);
    }

    /// <summary>Net totals for each month, January to December, of <c>year</c>
    /// (default: the current year), under the same filters as the list.</summary>
    [HttpGet("monthly")]
    [HasPermission(PermissionCodes.Expense.View)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseMonthlyDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonthly([FromQuery] ExpenseMonthlyRequest request,
        CancellationToken ct) =>
        OkEnvelope(await expenseService.GetMonthlyAsync(request, ct));

    // ── Adjustments: corrections recorded against an existing expense ────────
    // They reuse EXPENSE.* rather than new codes, so every role that can edit an
    // expense can adjust one, with nothing to seed.

    [HttpGet("adjustments")]
    [HasPermission(PermissionCodes.Expense.View)]
    [ProducesResponseType(typeof(ApiResponse<PagedResult<ExpenseAdjustmentDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdjustments([FromQuery] ExpenseAdjustmentListRequest request,
        CancellationToken ct) =>
        OkEnvelope(await expenseService.GetAdjustmentsAsync(request, ct));

    [HttpGet("adjustments/{adjustmentId:long}")]
    [HasPermission(PermissionCodes.Expense.View)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseAdjustmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdjustment(long adjustmentId, CancellationToken ct) =>
        OkEnvelope(await expenseService.GetAdjustmentAsync(adjustmentId, ct));

    [HttpPost("adjustments")]
    [HasPermission(PermissionCodes.Expense.Edit)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseAdjustmentDto>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateAdjustment(SaveExpenseAdjustmentRequest request,
        CancellationToken ct)
    {
        var adjustment = await expenseService.CreateAdjustmentAsync(request, ct);
        return CreatedEnvelope($"/api/expenses/adjustments/{adjustment.ExpenseAdjustmentId}",
            adjustment, "Adjustment recorded.");
    }

    [HttpPut("adjustments/{adjustmentId:long}")]
    [HasPermission(PermissionCodes.Expense.Edit)]
    [ProducesResponseType(typeof(ApiResponse<ExpenseAdjustmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateAdjustment(long adjustmentId,
        SaveExpenseAdjustmentRequest request, CancellationToken ct) =>
        OkEnvelope(await expenseService.UpdateAdjustmentAsync(adjustmentId, request, ct),
            "Adjustment updated.");

    [HttpDelete("adjustments/{adjustmentId:long}")]
    [HasPermission(PermissionCodes.Expense.Delete)]
    public async Task<IActionResult> DeleteAdjustment(long adjustmentId, CancellationToken ct)
    {
        await expenseService.DeleteAdjustmentAsync(adjustmentId, ct);
        return OkEnvelope("Adjustment deleted.");
    }
}
