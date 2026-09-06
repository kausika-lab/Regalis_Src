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
}
