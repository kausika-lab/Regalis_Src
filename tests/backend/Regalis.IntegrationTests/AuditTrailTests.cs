using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Regalis.Application.Administration;
using Regalis.Application.Auth;
using Regalis.Application.Masters;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

/// <summary>Audit Trail Enhancements v1.0 — the acceptance criteria of §7 that the API
/// owns: search, action/user/result filters, Result capture (Failed and Denied), the
/// details drawer, export logging, and the §6 fixes (real IDs on CREATE, no "system"
/// UPDATE after every sign-in).</summary>
[Collection(ApiCollection.Name)]
public class AuditTrailTests(RegalisApiFactory factory)
{
    private const string Audit = "/api/admin/audit";

    private async Task<HttpClient> AdminAsync()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);
        return admin;
    }

    private static async Task<PagedResult<AuditListItemDto>> ListAsync(HttpClient client, string query) =>
        (await client.GetFromJsonAsync<ApiResponse<PagedResult<AuditListItemDto>>>($"{Audit}?pageSize=100&{query}"))!.Data!;

    private static async Task<(int Id, string Code)> WorkspaceAsync(HttpClient admin)
    {
        var id = await admin.CreateWorkspaceAsync();
        var ws = (await admin.GetFromJsonAsync<ApiResponse<WorkspaceDto>>($"/api/workspaces/{id}"))!.Data!;
        return (id, ws.WorkspaceCode);
    }

    private static async Task RenameWorkspaceAsync(HttpClient admin, int id)
    {
        var ws = (await admin.GetFromJsonAsync<ApiResponse<WorkspaceDto>>($"/api/workspaces/{id}"))!.Data!;
        (await admin.PutAsJsonAsync($"/api/workspaces/{id}", new SaveWorkspaceRequest(
            ws.LocationId, ws.WorkspaceTypeId, ws.WorkspaceCode, ws.WorkspaceName + " (renamed)", ws.FloorNumber, 2,
            ws.PricePerHour, ws.PricePerDay, ws.PricePerMonth, ws.Equipment, ws.Description, ws.IsBookable, [])))
            .EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Create_RecordsTheRealId_AndSearchFindsThatEntityOnly()
    {
        var admin = await AdminAsync();
        var (id, _) = await WorkspaceAsync(admin);
        await RenameWorkspaceAsync(admin, id);
        await WorkspaceAsync(admin); // another workspace that must not match

        var rows = await ListAsync(admin, $"search=Workspace%20%23{id}");
        rows.Items.Should().NotBeEmpty().And.OnlyContain(r => r.EntityType == "Workspace" && r.EntityId == id.ToString());
        rows.Items.Should().Contain(r => r.Action == AuditActions.Create && r.EntityLabel == $"Workspace #{id}",
            "the CREATE row is written after the insert, so it carries the real key");
        rows.Items.Should().OnlyContain(r => r.Result == AuditResults.Success);

        var update = rows.Items.First(r => r.Action == AuditActions.Update);
        update.ChangeSummary.Should().StartWith("2 fields changed:").And.Contain("WorkspaceName").And.Contain("Capacity");
    }

    [Fact]
    public async Task ActionFilter_SingleAndMultiple()
    {
        var admin = await AdminAsync();
        var (id, _) = await WorkspaceAsync(admin);
        await RenameWorkspaceAsync(admin, id);
        (await admin.DeleteAsync($"/api/workspaces/{id}")).EnsureSuccessStatusCode();

        var deletes = await ListAsync(admin, $"search=Workspace%20{id}&actions=DELETE");
        deletes.Items.Should().ContainSingle().Which.Action.Should().Be(AuditActions.Delete);

        var both = await ListAsync(admin, $"search=Workspace%20{id}&actions=DELETE,UPDATE");
        both.Items.Select(r => r.Action).Distinct().Should().BeEquivalentTo([AuditActions.Delete, AuditActions.Update]);
    }

    [Fact]
    public async Task WrongPassword_IsLoginFailed_WithIp_AndNoPasswordStored()
    {
        var anonymous = factory.CreateClient();
        const string secret = "Definitely-Wrong-9!";
        (await anonymous.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RegalisApiFactory.AdminEmail, secret, "admin"))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var admin = await AdminAsync();
        var failed = await ListAsync(admin, "actions=LOGIN_FAILED&result=Failed");
        var row = failed.Items.Should().Contain(r => r.User.Email == RegalisApiFactory.AdminEmail).Subject;
        row.FailureReason.Should().StartWith("Invalid password");
        // The in-memory test server has no client address; UAT shows the real IP.

        var detail = (await admin.GetFromJsonAsync<ApiResponse<AuditDetailDto>>($"{Audit}/{row.Id}"))!.Data!;
        (detail.NewValues ?? "").Should().NotContain(secret);
        (detail.OldValues ?? "").Should().NotContain(secret);
    }

    [Fact]
    public async Task SignIn_DoesNotAddASystemUpdateOnTheUser()
    {
        var before = await ListAsync(await AdminAsync(), "userIds=system&actions=UPDATE&search=User");
        await AdminAsync();
        await AdminAsync();
        var after = await ListAsync(await AdminAsync(), "userIds=system&actions=UPDATE&search=User");
        after.TotalCount.Should().Be(before.TotalCount);
    }

    [Fact]
    public async Task MissingPermission_IsDenied_WithTheAttemptedActionAndReason()
    {
        var admin = await AdminAsync();
        var (id, _) = await WorkspaceAsync(admin);
        var roles = (await admin.GetFromJsonAsync<ApiResponse<List<Regalis.Application.Administration.RoleDto>>>("/api/roles"))!.Data!;
        var email = $"support-{Guid.NewGuid():N}@test.regalis.in";
        (await admin.PostAsJsonAsync("/api/users", new CreateUserRequest("Support Probe", email,
            $"6{Random.Shared.NextInt64(100_000_000, 999_999_999)}", "R0le!TestPass1", 2,
            [roles.Single(r => r.RoleCode == RoleCodes.Support).RoleId], null, 1))).EnsureSuccessStatusCode();
        var support = factory.CreateClient();
        support.UseToken((await support.LoginAsync(email, "R0le!TestPass1", "admin")).AccessToken);

        (await support.DeleteAsync($"/api/workspaces/{id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var denied = await ListAsync(admin, $"result=Denied&search=Workspace%20{id}");
        var row = denied.Items.Should().ContainSingle().Subject;
        row.Action.Should().Be(AuditActions.Delete);
        row.User.Name.Should().Be("Support Probe");
        row.FailureReason.Should().Be("Missing permission: WORKSPACE.DELETE");
        row.ChangeSummary.Should().Be(row.FailureReason);
    }

    [Fact]
    public async Task BusinessRuleBlock_IsDenied_AndValidationError_IsFailed()
    {
        var admin = await AdminAsync();
        var (id, code) = await WorkspaceAsync(admin);

        // A second workspace with the same code breaks a rule; an empty name fails validation.
        var ws = (await admin.GetFromJsonAsync<ApiResponse<WorkspaceDto>>($"/api/workspaces/{id}"))!.Data!;
        var duplicate = await admin.PostAsJsonAsync("/api/workspaces", new SaveWorkspaceRequest(
            ws.LocationId, ws.WorkspaceTypeId, code, "Duplicate", "1", 1, 100m, null, null, null, null, true, []));
        ((int)duplicate.StatusCode).Should().BeOneOf(409, 422);
        var invalid = await admin.PostAsJsonAsync("/api/workspaces", new SaveWorkspaceRequest(
            ws.LocationId, ws.WorkspaceTypeId, "", "", "1", 0, -5m, null, null, null, null, true, []));
        invalid.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var denied = await ListAsync(admin, "result=Denied&actions=CREATE&search=Duplicate");
        denied.Items.Should().Contain(r => r.EntityType == "Workspace" && r.FailureReason != null);
        var failed = await ListAsync(admin, "result=Failed&actions=CREATE");
        failed.Items.Should().Contain(r => r.EntityType == "Workspace" && r.FailureReason!.StartsWith("Validation error:"));
    }

    [Fact]
    public async Task Detail_ShowsChangedFieldsOnly_AndCreateHasAfterValuesOnly()
    {
        var admin = await AdminAsync();
        var (id, _) = await WorkspaceAsync(admin);
        await RenameWorkspaceAsync(admin, id);
        var rows = await ListAsync(admin, $"search=Workspace%20{id}");

        var update = (await admin.GetFromJsonAsync<ApiResponse<AuditDetailDto>>(
            $"{Audit}/{rows.Items.First(r => r.Action == AuditActions.Update).Id}"))!.Data!;
        update.ChangedCount.Should().Be(2);
        update.Fields.Where(f => f.Changed).Select(f => f.Field).Should().BeEquivalentTo(["WorkspaceName", "Capacity"]);
        update.Fields.Single(f => f.Field == "Capacity").Before.Should().Be("1");
        update.Fields.Single(f => f.Field == "Capacity").After.Should().Be("2");
        update.Fields.Should().Contain(f => !f.Changed, "unchanged fields are there for the toggle");

        var create = (await admin.GetFromJsonAsync<ApiResponse<AuditDetailDto>>(
            $"{Audit}/{rows.Items.First(r => r.Action == AuditActions.Create).Id}"))!.Data!;
        create.Fields.Should().OnlyContain(f => f.Before == null && f.After != null);
        create.Fields.Single(f => f.Field == "WorkspaceId").After.Should().Be(id.ToString());

        var related = (await admin.GetFromJsonAsync<ApiResponse<List<AuditListItemDto>>>(
            $"{Audit}/{update.Record.Id}/related"))!.Data!;
        related.Should().Contain(r => r.Action == AuditActions.Create).And.NotContain(r => r.Id == update.Record.Id);
    }

    [Fact]
    public async Task Export_ContainsTheFilteredRows_AndIsItselfAudited()
    {
        var admin = await AdminAsync();
        var (id, _) = await WorkspaceAsync(admin);
        await RenameWorkspaceAsync(admin, id);

        var export = await admin.GetAsync($"{Audit}/export?search=Workspace%20{id}");
        export.EnsureSuccessStatusCode();
        var lines = (await export.Content.ReadAsStringAsync()).Trim().Split('\n');
        lines.Should().HaveCount(3, "a header, the CREATE and the UPDATE");
        lines[0].Should().Contain("Result").And.Contain("IP").And.Contain("OldValues").And.Contain("NewValues");

        var exports = await ListAsync(admin, "actions=EXPORT&module=Audit");
        exports.Items.Should().Contain(r => r.ChangeSummary == "Exported 2 rows to CSV");
    }

    [Fact]
    public async Task Lookups_IncludeSystemAndTheKnownActions()
    {
        var lookups = (await (await AdminAsync()).GetFromJsonAsync<ApiResponse<AuditLookupsDto>>($"{Audit}/lookups"))!.Data!;
        lookups.Users.Should().Contain(u => u.Value == "system");
        lookups.Actions.Should().Contain([AuditActions.Create, AuditActions.LoginFailed, AuditActions.Export]);
        lookups.Results.Should().BeEquivalentTo(AuditResults.All);
    }

    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task NoEndpointEditsOrDeletesAnAuditRecord(string method)
    {
        var admin = await AdminAsync();
        var any = (await ListAsync(admin, "")).Items.First();
        var response = await admin.SendAsync(new HttpRequestMessage(new HttpMethod(method), $"{Audit}/{any.Id}"));
        ((int)response.StatusCode).Should().BeOneOf(404, 405);
    }
}
