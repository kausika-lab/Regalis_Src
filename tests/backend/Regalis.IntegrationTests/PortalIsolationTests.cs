using System.Net;
using FluentAssertions;
using Regalis.IntegrationTests.Infrastructure;

namespace Regalis.IntegrationTests;

/// <summary>A customer's token must never reach admin routes that read across customers,
/// even when a role misconfiguration hands customers the matching permission codes
/// (the 2026-09-28 finding: customers could read other customers' wallets and quotations).</summary>
[Collection(ApiCollection.Name)]
public class PortalIsolationTests(RegalisApiFactory factory)
{
    [Theory]
    [InlineData("/api/admin/wallets")]
    [InlineData("/api/admin/wallets/1")]
    [InlineData("/api/admin/wallets/export")]
    [InlineData("/api/wallet/1")]
    [InlineData("/api/wallet/1/transactions")]
    [InlineData("/api/quotations")]
    [InlineData("/api/quotations/1")]
    [InlineData("/api/quotations/summary")]
    [InlineData("/api/quotations/status-counts")]
    public async Task CustomerToken_IsRefusedOnAdminReads(string url)
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        (await customer.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData("/api/wallet/me")]
    [InlineData("/api/quotations/my")]
    public async Task CustomerToken_StillReachesItsOwnData(string url)
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        (await customer.GetAsync(url)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminToken_StillReachesTheAdminReads()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);

        (await admin.GetAsync("/api/admin/wallets")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await admin.GetAsync("/api/quotations")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
