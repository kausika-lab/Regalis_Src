using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Finance;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;

namespace Regalis.IntegrationTests;

/// <summary>/api/admin/wallets/{customerId}: the customer ledger, Add money, Adjust and statements.</summary>
[Collection(ApiCollection.Name)]
public class WalletDeskTests(RegalisApiFactory factory)
{
    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private async Task<long> CustomerAsync()
    {
        var client = factory.CreateClient();
        var (_, _, session) = await client.RegisterCustomerAsync(factory);
        return session.User.UserId;
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<T>>())!.Data!;
    }

    private static DateOnly TodayIst() => DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));

    [Fact]
    public async Task AddMoney_WritesOnePaymentAndOneLinkedRecharge_AndOnlyThatRowHasAReceipt()
    {
        var admin = await AdminAsync();
        var customerId = await CustomerAsync();
        var before = await ReadAsync<WalletDetailDto>(await admin.GetAsync($"/api/admin/wallets/{customerId}"));

        var added = await ReadAsync<AdminAddMoneyResponse>(await admin.PostAsJsonAsync(
            $"/api/admin/wallets/{customerId}/add-money",
            new AdminAddMoneyRequest(500m, 2, "UTR-4455", TodayIst(), null, false, before.NextPaymentNumber)));

        added.PaymentNumber.Should().StartWith("PAY-");
        added.Balance.Should().Be(before.Balance + 500m);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
            var payment = await db.Payments.SingleAsync(p => p.PaymentId == added.PaymentId);
            payment.Status.Should().Be(PaymentStatus.Success);
            payment.PaymentMethod.Should().Be(PaymentMethod.Upi);
            payment.Gateway.Should().Be("Desk");
            payment.GatewayPaymentId.Should().Be("UTR-4455");
            var credit = await db.WalletTransactions.SingleAsync(t => t.PaymentId == added.PaymentId);
            credit.Category.Should().Be(WalletCategory.Recharge);
            credit.Direction.Should().Be(WalletDirection.Credit);
            credit.Amount.Should().Be(500m);
            credit.Narration.Should().Be($"Wallet recharge {added.PaymentNumber} · UPI");
            (await db.AuditLogs.AnyAsync(a => a.Action == "WALLET_ADD_MONEY" && a.EntityId == customerId.ToString()))
                .Should().BeTrue();
        }

        await admin.PostAsJsonAsync($"/api/admin/wallets/{customerId}/adjust",
            new AdminAdjustRequest("debit", 45m, "Front-desk correction", "Admin adjustment: FRONT-DESK correction"));

        var detail = await ReadAsync<WalletDetailDto>(await admin.GetAsync($"/api/admin/wallets/{customerId}"));
        detail.TotalCredit.Should().Be(500m);
        detail.TotalDebit.Should().Be(45m);
        detail.Balance.Should().Be(455m);
        detail.LedgerVerified.Should().BeTrue();
        var recharge = detail.Transactions.Items.Single(t => t.Category == (byte)WalletCategory.Recharge);
        recharge.ReceiptPaymentId.Should().Be(added.PaymentId);
        var adjustment = detail.Transactions.Items.Single(t => t.Category == (byte)WalletCategory.AdminAdjustment);
        adjustment.ReceiptPaymentId.Should().BeNull();
        adjustment.PerformedByName.Should().NotBeNullOrWhiteSpace();
        detail.Transactions.Items[0].TransactionNumber.Should().Be(adjustment.TransactionNumber, "newest first");
    }

    [Fact]
    public async Task AddMoney_RejectsAFutureDate_AndWritesNothing()
    {
        var admin = await AdminAsync();
        var customerId = await CustomerAsync();

        var response = await admin.PostAsJsonAsync($"/api/admin/wallets/{customerId}/add-money",
            new AdminAddMoneyRequest(100m, 6, null, TodayIst().AddDays(3), null, false));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        (await db.Payments.AnyAsync(p => p.CustomerId == customerId)).Should().BeFalse();
        (await db.WalletTransactions.AnyAsync(t => t.CustomerId == customerId)).Should().BeFalse();
    }

    [Fact]
    public async Task DebitAdjustment_CannotTakeTheBalanceBelowZero_AndLeavesNoEntry()
    {
        var admin = await AdminAsync();
        var customerId = await CustomerAsync();
        await ReadAsync<WalletDetailDto>(await admin.PostAsJsonAsync($"/api/admin/wallets/{customerId}/adjust",
            new AdminAdjustRequest("credit", 100m, "Goodwill", "Goodwill credit")));

        var response = await admin.PostAsJsonAsync($"/api/admin/wallets/{customerId}/adjust",
            new AdminAdjustRequest("debit", 100.01m, "Booking correction", "Booking correction"));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var detail = await ReadAsync<WalletDetailDto>(await admin.GetAsync($"/api/admin/wallets/{customerId}"));
        detail.Balance.Should().Be(100m);
        detail.Transactions.TotalCount.Should().Be(1);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var audit = await db.AuditLogs.Where(a => a.Action == "WALLET_ADJUST" && a.EntityId == customerId.ToString()).ToListAsync();
        audit.Should().ContainSingle().Which.NewValue.Should().Contain("Goodwill");
    }

    [Fact]
    public async Task Filters_NarrowTheLedger_ButTotalsStayTheWholeWallet()
    {
        var admin = await AdminAsync();
        var customerId = await CustomerAsync();
        await admin.PostAsJsonAsync($"/api/admin/wallets/{customerId}/add-money",
            new AdminAddMoneyRequest(2000m, 3, null, TodayIst(), "Opening top-up", false));
        await admin.PostAsJsonAsync($"/api/admin/wallets/{customerId}/adjust",
            new AdminAdjustRequest("debit", 177m, "Booking", "Booking REG-000047 — Hot desk"));

        var debits = await ReadAsync<WalletDetailDto>(await admin.GetAsync($"/api/admin/wallets/{customerId}?direction=debit"));
        debits.Transactions.Items.Should().OnlyContain(t => t.Direction == 2).And.HaveCount(1);
        debits.Balance.Should().Be(1823m);

        var recharges = await ReadAsync<WalletDetailDto>(await admin.GetAsync($"/api/admin/wallets/{customerId}?category=recharge"));
        recharges.Transactions.Items.Should().ContainSingle().Which.Narration.Should().Be("Opening top-up");

        var search = await ReadAsync<WalletDetailDto>(await admin.GetAsync($"/api/admin/wallets/{customerId}?search=Hot%20desk"));
        search.Transactions.Items.Should().ContainSingle().Which.Amount.Should().Be(177m);

        var future = TodayIst().AddDays(10).ToString("yyyy-MM-dd");
        var none = await ReadAsync<WalletDetailDto>(await admin.GetAsync($"/api/admin/wallets/{customerId}?from={future}"));
        none.Transactions.Items.Should().BeEmpty();
        none.TotalCredit.Should().Be(2000m);
    }

    [Fact]
    public async Task Statement_IsAPdfOrACsvWithOpeningAndClosingBalances()
    {
        var admin = await AdminAsync();
        var customerId = await CustomerAsync();
        await admin.PostAsJsonAsync($"/api/admin/wallets/{customerId}/add-money",
            new AdminAddMoneyRequest(750m, 6, null, TodayIst(), null, false));

        var pdf = await admin.GetAsync($"/api/admin/wallets/{customerId}/statement");
        pdf.EnsureSuccessStatusCode();
        pdf.Content.Headers.ContentType!.MediaType.Should().Be("application/pdf");
        (await pdf.Content.ReadAsByteArrayAsync())[..4].Should().Equal("%PDF"u8.ToArray());

        var csv = await admin.GetStringAsync($"/api/admin/wallets/{customerId}/statement?format=csv");
        csv.Should().Contain("Opening balance,0.00").And.Contain("Closing balance,750.00").And.Contain("Recharge");
    }

    [Fact]
    public async Task UnknownOrNonCustomerId_Is404()
    {
        var admin = await AdminAsync();
        (await admin.GetAsync("/api/admin/wallets/99999999")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
