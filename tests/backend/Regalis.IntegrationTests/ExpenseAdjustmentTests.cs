using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Finance;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests;

/// <summary>
/// Expense adjustments and the charts' endpoints: an adjustment moves the expense's net
/// total and every summary built on it, never the voucher amount, and can never take an
/// expense below zero. Each test uses its own category (and a far-off year for the
/// monthly chart) so the shared database's other expenses cannot move its totals.
/// </summary>
[Collection(ApiCollection.Name)]
public class ExpenseAdjustmentTests(RegalisApiFactory factory)
{
    private async Task<HttpClient> AdminClientAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private async Task<int> LocationIdAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        return await db.Locations.OrderByDescending(l => l.IsDefault)
            .Select(l => l.LocationId).FirstAsync();
    }

    private static string NewCategory() => $"Test {Guid.NewGuid():N}"[..20];

    private async Task<ExpenseDto> CreateExpenseAsync(HttpClient admin, string category,
        decimal amount, DateOnly date)
    {
        var response = await admin.PostAsJsonAsync("/api/expenses", new SaveExpenseRequest(
            date, category, "Printer toner", amount, 6, "Office supplies", null,
            await LocationIdAsync(), null, null));
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<ApiResponse<ExpenseDto>>())!.Data!;
    }

    private static async Task<ExpenseSummaryDto> SummaryAsync(HttpClient admin, string category) =>
        (await admin.GetFromJsonAsync<ApiResponse<ExpenseSummaryDto>>(
            $"/api/expenses/summary?category={Uri.EscapeDataString(category)}"))!.Data!;

    [Fact]
    public async Task Adjustments_MoveTheNetTotalAndTheSummary()
    {
        var admin = await AdminClientAsync();
        var category = NewCategory();
        var expense = await CreateExpenseAsync(admin, category, 1000m, new DateOnly(2031, 3, 10));
        expense.NetAmount.Should().Be(1000m);
        expense.AdjustmentCount.Should().Be(0);

        // +250 then −100: net 1,150, voucher amount untouched.
        var increase = await admin.PostAsJsonAsync("/api/expenses/adjustments",
            new SaveExpenseAdjustmentRequest(expense.ExpenseId, 1, 250m,
                new DateOnly(2031, 3, 12), "Delivery charge billed later", null));
        increase.StatusCode.Should().Be(HttpStatusCode.Created);
        var decrease = (await (await admin.PostAsJsonAsync("/api/expenses/adjustments",
                new SaveExpenseAdjustmentRequest(expense.ExpenseId, 2, 100m, null,
                    "Supplier refund", "Credit note 44")))
            .Content.ReadFromJsonAsync<ApiResponse<ExpenseAdjustmentDto>>())!.Data!;
        decrease.SignedAmount.Should().Be(-100m);
        decrease.ExpenseNetAmount.Should().Be(1150m);

        var reloaded = (await admin.GetFromJsonAsync<ApiResponse<ExpenseDto>>(
            $"/api/expenses/{expense.ExpenseId}"))!.Data!;
        reloaded.Amount.Should().Be(1000m);
        reloaded.AdjustmentTotal.Should().Be(150m);
        reloaded.NetAmount.Should().Be(1150m);
        reloaded.AdjustmentCount.Should().Be(2);

        var summary = await SummaryAsync(admin, category);
        summary.GrossAmount.Should().Be(1000m);
        summary.AdjustmentTotal.Should().Be(150m);
        summary.TotalAmount.Should().Be(1150m);
        summary.ByCategory.Should().ContainSingle().Which.Amount.Should().Be(1150m);

        // Editing the refund to 300 → net 950; deleting it → net 1,250.
        var edit = await admin.PutAsJsonAsync($"/api/expenses/adjustments/{decrease.ExpenseAdjustmentId}",
            new SaveExpenseAdjustmentRequest(expense.ExpenseId, 2, 300m, null, "Supplier refund", null));
        edit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await SummaryAsync(admin, category)).TotalAmount.Should().Be(950m);

        (await admin.DeleteAsync($"/api/expenses/adjustments/{decrease.ExpenseAdjustmentId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);
        (await SummaryAsync(admin, category)).TotalAmount.Should().Be(1250m);

        var history = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<ExpenseAdjustmentDto>>>(
            $"/api/expenses/adjustments?expenseId={expense.ExpenseId}"))!.Data!;
        history.Items.Should().ContainSingle().Which.Reason.Should().Be("Delivery charge billed later");
    }

    [Fact]
    public async Task Adjustments_NeverTakeAnExpenseBelowZero()
    {
        var admin = await AdminClientAsync();
        var expense = await CreateExpenseAsync(admin, NewCategory(), 500m, new DateOnly(2031, 4, 1));

        var tooMuch = await admin.PostAsJsonAsync("/api/expenses/adjustments",
            new SaveExpenseAdjustmentRequest(expense.ExpenseId, 2, 500.01m, null, "Refund", null));
        tooMuch.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        // A full refund to nil is allowed.
        var full = await admin.PostAsJsonAsync("/api/expenses/adjustments",
            new SaveExpenseAdjustmentRequest(expense.ExpenseId, 2, 500m, null, "Full refund", null));
        full.StatusCode.Should().Be(HttpStatusCode.Created);

        // Lowering the voucher under the refund is refused too.
        var lower = await admin.PutAsJsonAsync($"/api/expenses/{expense.ExpenseId}",
            new SaveExpenseRequest(expense.ExpenseDate, expense.Category, expense.Description,
                400m, 6, expense.Remarks, null, expense.LocationId, null, null));
        lower.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);

        var invalid = await admin.PostAsJsonAsync("/api/expenses/adjustments",
            new SaveExpenseAdjustmentRequest(expense.ExpenseId, 3, 10m, null, "", null));
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task DeletingAnExpense_HidesItsAdjustments()
    {
        var admin = await AdminClientAsync();
        var expense = await CreateExpenseAsync(admin, NewCategory(), 800m, new DateOnly(2031, 5, 1));
        var adjustment = (await (await admin.PostAsJsonAsync("/api/expenses/adjustments",
                new SaveExpenseAdjustmentRequest(expense.ExpenseId, 1, 50m, null, "Late fee", null)))
            .Content.ReadFromJsonAsync<ApiResponse<ExpenseAdjustmentDto>>())!.Data!;

        (await admin.DeleteAsync($"/api/expenses/{expense.ExpenseId}"))
            .StatusCode.Should().Be(HttpStatusCode.OK);

        (await admin.GetAsync($"/api/expenses/adjustments/{adjustment.ExpenseAdjustmentId}"))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        var history = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<ExpenseAdjustmentDto>>>(
            $"/api/expenses/adjustments?expenseId={expense.ExpenseId}"))!.Data!;
        history.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Monthly_ReturnsTwelveNetMonthsOfTheYear()
    {
        var admin = await AdminClientAsync();
        var category = NewCategory();
        var march = await CreateExpenseAsync(admin, category, 1000m, new DateOnly(2032, 3, 5));
        await CreateExpenseAsync(admin, category, 200m, new DateOnly(2032, 3, 25));
        await CreateExpenseAsync(admin, category, 300m, new DateOnly(2032, 11, 2));
        await CreateExpenseAsync(admin, category, 999m, new DateOnly(2033, 1, 1)); // other year
        await admin.PostAsJsonAsync("/api/expenses/adjustments",
            new SaveExpenseAdjustmentRequest(march.ExpenseId, 2, 100m, null, "Refund", null));

        var monthly = (await admin.GetFromJsonAsync<ApiResponse<ExpenseMonthlyDto>>(
            $"/api/expenses/monthly?year=2032&category={Uri.EscapeDataString(category)}"))!.Data!;
        monthly.Year.Should().Be(2032);
        monthly.Months.Should().HaveCount(12);
        monthly.Months.Select(m => m.Month).Should().Equal(Enumerable.Range(1, 12));
        monthly.Months[2].Amount.Should().Be(1100m);
        monthly.Months[2].Count.Should().Be(2);
        monthly.Months[10].Amount.Should().Be(300m);
        monthly.TotalAmount.Should().Be(1400m);

        // From/To still apply inside the year.
        var ranged = (await admin.GetFromJsonAsync<ApiResponse<ExpenseMonthlyDto>>(
            $"/api/expenses/monthly?year=2032&from=2032-04-01&category={Uri.EscapeDataString(category)}"))!.Data!;
        ranged.TotalAmount.Should().Be(300m);
    }
}
