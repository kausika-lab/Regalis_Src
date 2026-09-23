using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Regalis.Application.Finance;
using Regalis.Application.Masters;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

[Collection(ApiCollection.Name)]
public class MastersAndWalletTests(RegalisApiFactory factory)
{
    [Fact]
    public async Task Admin_CanCreateUpdateAndListLocations()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);

        var code = $"LOC-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var create = await admin.PostAsJsonAsync("/api/locations", new SaveLocationRequest(
            code, "Regalis Annexe", "12 Mount Road", null, "Chennai", "Tamil Nadu", "600002",
            "04412345678", "annexe@regalis.example", "09:00", "20:00",
            ["MON", "TUE", "WED", "THU", "FRI"], null, null, null, false));
        create.StatusCode.Should().Be(HttpStatusCode.Created);
        var location = (await create.Content.ReadFromJsonAsync<ApiResponse<LocationDto>>())!.Data!;
        location.WorkingHoursStart.Should().Be("09:00");

        var list = (await admin.GetFromJsonAsync<ApiResponse<PagedResult<LocationDto>>>(
            "/api/locations?page=1&pageSize=50"))!.Data!;
        list.Items.Should().Contain(l => l.LocationCode == code);

        var duplicate = await admin.PostAsJsonAsync("/api/locations", new SaveLocationRequest(
            code, "Copy", null, null, null, null, null, null, null, "09:00", "20:00",
            ["MON"], null, null, null, false));
        duplicate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Workspace_DuplicateCodePerLocation_IsRejected()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        var types = (await admin.GetFromJsonAsync<ApiResponse<List<WorkspaceTypeDto>>>(
            "/api/workspace-types"))!.Data!;
        var deskId = types.Single(t => t.TypeCode == "DESK").WorkspaceTypeId;

        var code = $"D-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        SaveWorkspaceRequest Request() => new(1, deskId, code, "Dup Desk", null, 1,
            300m, null, null, null, null, true, []);

        (await admin.PostAsJsonAsync("/api/workspaces", Request()))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var duplicate = await admin.PostAsJsonAsync("/api/workspaces", Request());
        duplicate.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var envelope = await duplicate.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.DuplicateRecord);
    }

    [Fact]
    public async Task Customer_CanReadMastersButNotWriteThem()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        (await customer.GetAsync("/api/locations")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await customer.GetAsync("/api/workspace-types")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await customer.GetAsync("/api/workspaces")).StatusCode.Should().Be(HttpStatusCode.OK);

        var forbidden = await customer.PostAsJsonAsync("/api/workspaces", new SaveWorkspaceRequest(
            1, 1, "HAX-001", "Nope", null, 1, 1m, null, null, null, null, true, []));
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Wallet_NewCustomer_HasZeroBalanceAndEmptyLedger()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        var wallet = (await customer.GetFromJsonAsync<ApiResponse<WalletDto>>("/api/wallet/me"))!.Data!;
        wallet.Balance.Should().Be(0m);
        wallet.CustomerId.Should().Be(session.User.UserId);

        var transactions = (await customer.GetFromJsonAsync<ApiResponse<PagedResult<WalletTransactionDto>>>(
            "/api/wallet/me/transactions"))!.Data!;
        transactions.TotalCount.Should().Be(0);
    }
}
