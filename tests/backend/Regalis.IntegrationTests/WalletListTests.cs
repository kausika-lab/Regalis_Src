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

/// <summary>GET /api/admin/wallets — every figure comes from the ledger.</summary>
[Collection(ApiCollection.Name)]
public class WalletListTests(RegalisApiFactory factory)
{
    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private async Task<(long CustomerId, string Email)> CustomerWithLedgerAsync(decimal credit, decimal debit)
    {
        var client = factory.CreateClient();
        var (email, _, session) = await client.RegisterCustomerAsync(factory);
        using var scope = factory.Services.CreateScope();
        var wallets = scope.ServiceProvider.GetRequiredService<IWalletService>();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        if (credit > 0)
        {
            await wallets.CreditAsync(session.User.UserId, credit, WalletCategory.Recharge, "Test recharge");
            await db.SaveChangesAsync();
        }
        if (debit > 0)
        {
            await wallets.DebitAsync(session.User.UserId, debit, WalletCategory.Booking, "Test booking");
            await db.SaveChangesAsync();
        }
        return (session.User.UserId, email);
    }

    private static async Task<WalletListResultDto> ListAsync(HttpClient admin, string query)
    {
        var response = await admin.GetAsync($"/api/admin/wallets?{query}");
        response.IsSuccessStatusCode.Should().BeTrue(await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<ApiResponse<WalletListResultDto>>())!.Data!;
    }

    [Fact]
    public async Task Totals_AreSummedFromTheLedger_AndBalanceIsCreditMinusDebit()
    {
        var admin = await AdminAsync();
        var (customerId, email) = await CustomerWithLedgerAsync(2500m, 1520m);

        var result = await ListAsync(admin, $"search={Uri.EscapeDataString(email)}");

        var row = result.Wallets.Items.Should().ContainSingle().Subject;
        row.CustomerId.Should().Be(customerId);
        row.TotalCredit.Should().Be(2500m);
        row.TotalDebit.Should().Be(1520m);
        row.Balance.Should().Be(980m);
        row.IsLowBalance.Should().BeFalse();
        row.LastActivityUtc.Should().NotBeNull();
        result.FilteredTotals.Should().Be(new WalletTotalsDto(1, 2500m, 1520m, 980m));

        // The summary cards cover every wallet and must agree with the ledger as a whole.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var credits = await db.WalletTransactions.Where(t => t.Direction == WalletDirection.Credit).SumAsync(t => t.Amount);
        var debits = await db.WalletTransactions.Where(t => t.Direction == WalletDirection.Debit).SumAsync(t => t.Amount);
        result.Summary.TotalRecharged.Should().Be(credits);
        result.Summary.TotalSpent.Should().Be(debits);
        result.Summary.BalanceHeld.Should().Be(credits - debits);
        result.Summary.LowBalanceThreshold.Should().Be(100m);
    }

    [Fact]
    public async Task LowFilter_UsesTheThresholdSetting_AndIncludesZeroBalances()
    {
        var admin = await AdminAsync();
        var (lowId, lowEmail) = await CustomerWithLedgerAsync(3000m, 2906m);   // 94
        var (zeroId, zeroEmail) = await CustomerWithLedgerAsync(1500m, 1500m); // 0
        var (richId, richEmail) = await CustomerWithLedgerAsync(5000m, 0m);    // 5000

        // Other tests create many empty wallets, so each check is scoped to its own customer.
        async Task<List<long>> IdsAsync(string filter, string email) =>
            (await ListAsync(admin, $"filter={filter}&search={Uri.EscapeDataString(email)}"))
                .Wallets.Items.Select(i => i.CustomerId).ToList();

        (await IdsAsync("low", lowEmail)).Should().Equal(lowId);
        (await IdsAsync("low", zeroEmail)).Should().Equal(zeroId);
        (await IdsAsync("low", richEmail)).Should().BeEmpty();
        (await IdsAsync("zero", zeroEmail)).Should().Equal(zeroId);
        (await IdsAsync("zero", lowEmail)).Should().BeEmpty();
        (await IdsAsync("with-balance", richEmail)).Should().Equal(richId);
        (await IdsAsync("with-balance", zeroEmail)).Should().BeEmpty();

        var low = await ListAsync(admin, "filter=low&pageSize=100");
        low.Wallets.Items.Should().OnlyContain(i => i.IsLowBalance && i.Balance < 100m);

        var zero = await ListAsync(admin, "filter=zero&pageSize=100");
        zero.Wallets.Items.Should().OnlyContain(i => i.Balance == 0m);

        var withBalance = await ListAsync(admin, "filter=with-balance&pageSize=100");
        withBalance.Wallets.Items.Should().OnlyContain(i => i.Balance > 0m);
    }

    [Fact]
    public async Task Sorting_AndServerSidePaging_Work()
    {
        var admin = await AdminAsync();
        await CustomerWithLedgerAsync(700m, 0m);
        await CustomerWithLedgerAsync(300m, 50m);

        var byBalance = await ListAsync(admin, "sort=balance-desc&pageSize=100");
        byBalance.Wallets.Items.Select(i => i.Balance).Should().BeInDescendingOrder();

        var bySpent = await ListAsync(admin, "sort=spent&pageSize=100");
        bySpent.Wallets.Items.Select(i => i.TotalDebit).Should().BeInDescendingOrder();

        var page1 = await ListAsync(admin, "sort=name&page=1&pageSize=1");
        page1.Wallets.Items.Should().HaveCount(1);
        page1.Wallets.TotalCount.Should().Be(page1.FilteredTotals.CustomerCount);
        page1.Wallets.TotalCount.Should().BeGreaterThan(1);
    }

    [Fact]
    public async Task Export_ReturnsCsvWithATotalsLine()
    {
        var admin = await AdminAsync();
        var (_, email) = await CustomerWithLedgerAsync(1000m, 250m);

        var csv = await admin.GetStringAsync($"/api/admin/wallets/export?search={Uri.EscapeDataString(email)}");

        csv.Should().Contain("Customer,Email,Mobile,Total incoming,Total spent,Current balance");
        csv.Should().Contain("1000.00,250.00,750.00");
        csv.Should().Contain("Totals (1 customers)");
    }
}
