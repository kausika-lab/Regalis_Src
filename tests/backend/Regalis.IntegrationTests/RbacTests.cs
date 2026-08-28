using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regalis.Application.Auth;
using Regalis.Infrastructure.Persistence;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

[Collection(ApiCollection.Name)]
public class RbacTests(RegalisApiFactory factory)
{
    [Fact]
    public async Task Anonymous_ProtectedEndpoint_Returns401Envelope()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/api/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task CustomerWithValidToken_AdminEndpoint_Returns403_AndWritesAuditRow()
    {
        var client = factory.CreateClient();
        var (email, _, session) = await client.RegisterCustomerAsync(factory);
        client.UseToken(session.AccessToken);

        var response = await client.GetAsync("/api/users");
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // ROLE_PERMISSION_MATRIX §6: failure writes an AUTHORIZATION_FAILED audit row.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RegalisDbContext>();
        var userId = session.User.UserId;
        var auditRow = await db.AuditLogs
            .Where(a => a.UserId == userId && a.Action == AuditActions.AuthorizationFailed)
            .OrderByDescending(a => a.AuditLogId)
            .FirstOrDefaultAsync();
        auditRow.Should().NotBeNull($"a 403 for {email} must be audited");
        auditRow!.EntityId.Should().Be("USER.VIEW");
    }

    [Fact]
    public async Task Admin_CanDriveTheAdministrationEndpoints()
    {
        var client = factory.CreateClient();
        var session = await client.LoginAsAdminAsync();
        client.UseToken(session.AccessToken);

        (await client.GetAsync("/api/users")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/roles")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/permissions")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/menus?portalType=2")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task MenusMy_CustomerReceivesTheCustomerTreeOnly()
    {
        var client = factory.CreateClient();
        var (_, _, session) = await client.RegisterCustomerAsync(factory);
        client.UseToken(session.AccessToken);

        var envelope = await client.GetFromJsonAsync<ApiResponse<List<MenuNodeDto>>>("/api/menus/my");
        var menus = envelope!.Data!;

        menus.Should().NotBeEmpty();
        menus.Select(m => m.DisplayName).Should().Contain(["Dashboard", "Book Workspace", "Wallet"]);
        menus.Select(m => m.Route).Should().NotContain(r => r != null && r.StartsWith("/admin"));

        var support = menus.Single(m => m.DisplayName == "Support");
        support.Children.Select(c => c.DisplayName).Should().Contain(["Raise Ticket", "All Tickets"]);
    }

    [Fact]
    public async Task RolePermissionMatrix_ReflectsGrants()
    {
        var client = factory.CreateClient();
        var session = await client.LoginAsAdminAsync();
        client.UseToken(session.AccessToken);

        var roles = (await client.GetFromJsonAsync<ApiResponse<List<Administration.RoleView>>>(
            "/api/roles"))!.Data!;
        var housekeeping = roles.Single(r => r.RoleCode == "HOUSEKEEPING");

        var matrix = (await client.GetFromJsonAsync<ApiResponse<List<Administration.MatrixRow>>>(
            $"/api/roles/{housekeeping.RoleId}/permissions"))!.Data!;

        matrix.Where(m => m.IsGranted).Select(m => m.PermissionCode)
            .Should().BeEquivalentTo(
                "WORKSPACE.VIEW", "CALENDAR.VIEW", "ACCESS.VIEW", "ATTENDANCE.VIEW",
                "INVENTORY.VIEW", "HOUSEKEEPING.VIEW", "NOTIFICATION.VIEW");
    }
}

/// <summary>Thin read models for envelope deserialization in tests.</summary>
internal static class Administration
{
    internal record RoleView(int RoleId, string RoleCode, string RoleName);

    internal record MatrixRow(int PermissionId, string PermissionCode, bool IsGranted);
}
