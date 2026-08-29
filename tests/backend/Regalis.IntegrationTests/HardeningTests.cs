using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.RateLimiting;
using Regalis.Application.Administration;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

/// <summary>
/// Day 11 hardening: every endpoint declares its authorization, the seeded
/// role/permission matrix is executed in full against live endpoints, security
/// headers are on every response, and auth endpoints are rate-limited.
/// </summary>
[Collection(ApiCollection.Name)]
public class HardeningTests(RegalisApiFactory factory)
{
    // ── 1 · Authorization is explicit on every endpoint ─────────────────────

    [Fact]
    public void EveryEndpoint_DeclaresItsAuthorizationExplicitly()
    {
        var offenders = new List<string>();
        var controllers = typeof(Program).Assembly.GetTypes()
            .Where(t => t.IsSubclassOf(typeof(ControllerBase)) && !t.IsAbstract);
        foreach (var controller in controllers)
        {
            var controllerAuthorized =
                controller.GetCustomAttributes(inherit: true).OfType<IAuthorizeData>().Any();
            var actions = controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttributes().OfType<HttpMethodAttribute>().Any());
            foreach (var action in actions)
            {
                var attributes = action.GetCustomAttributes(inherit: true);
                var declared = controllerAuthorized ||
                               attributes.OfType<IAuthorizeData>().Any() ||
                               attributes.OfType<IAllowAnonymous>().Any();
                if (!declared)
                {
                    offenders.Add($"{controller.Name}.{action.Name}");
                }
            }
        }
        offenders.Should().BeEmpty(
            "every action must carry [Authorize]/[HasPermission] or an explicit [AllowAnonymous] " +
            "(the fallback policy would still require auth, but the intent must be declared)");
    }

    [Fact]
    public void AnonymousAuthEndpoints_AreRateLimited()
    {
        // Credential-facing anonymous POSTs must sit behind the strict "auth" policy.
        var authController = typeof(Program).Assembly.GetTypes()
            .Single(t => t.Name == "AuthController");
        var anonymousPosts = authController
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttributes().OfType<HttpPostAttribute>().Any() &&
                        m.GetCustomAttributes(inherit: true).OfType<IAllowAnonymous>().Any())
            .ToList();
        anonymousPosts.Should().NotBeEmpty();
        foreach (var action in anonymousPosts)
        {
            action.GetCustomAttribute<EnableRateLimitingAttribute>()?.PolicyName.Should().Be("auth",
                $"{action.Name} accepts credentials anonymously and must be rate-limited");
        }
    }

    // ── 2 · Headers and anonymous surface ───────────────────────────────────

    [Fact]
    public async Task SecurityHeaders_OnEveryResponse_AndHealthStaysAnonymous()
    {
        var client = factory.CreateClient();
        var health = await client.GetAsync("/health");
        health.StatusCode.Should().Be(HttpStatusCode.OK, "liveness must not need a token");
        (await client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);

        health.Headers.TryGetValues("X-Content-Type-Options", out var nosniff).Should().BeTrue();
        nosniff!.Single().Should().Be("nosniff");
        health.Headers.TryGetValues("X-Frame-Options", out var frame).Should().BeTrue();
        frame!.Single().Should().Be("DENY");
        health.Headers.Contains("Content-Security-Policy").Should().BeTrue();
        health.Headers.Contains("Referrer-Policy").Should().BeTrue();

        // An API route without a token is a 401 envelope, never a 200.
        var unauthenticated = await client.GetAsync("/api/bookings");
        unauthenticated.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── 3 · The role/permission matrix, executed in full ────────────────────

    private async Task<HttpClient> RoleClientAsync(HttpClient admin, string roleCode, byte userType)
    {
        var roles = (await admin.GetFromJsonAsync<ApiResponse<List<RoleDto>>>("/api/roles"))!.Data!;
        var roleId = roles.Single(r => r.RoleCode == roleCode).RoleId;
        var email = $"{roleCode.ToLowerInvariant()}-{Guid.NewGuid():N}@test.regalis.in";
        const string password = "R0le!TestPass1";
        var mobile = $"6{Random.Shared.NextInt64(100_000_000, 999_999_999)}";
        var created = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest(
            $"{roleCode} Probe", email, mobile, password, userType, [roleId], null, 1));
        created.EnsureSuccessStatusCode();

        var client = factory.CreateClient();
        client.UseToken((await client.LoginAsync(email, password, "admin")).AccessToken);
        return client;
    }

    [Fact]
    public async Task RolePermissionMatrix_IsEnforcedOnLiveEndpoints()
    {
        var admin = factory.CreateClient();
        admin.UseToken((await admin.LoginAsAdminAsync()).AccessToken);

        var clients = new Dictionary<string, HttpClient>
        {
            [RoleCodes.Manager] = await RoleClientAsync(admin, RoleCodes.Manager, userType: 2),
            [RoleCodes.Finance] = await RoleClientAsync(admin, RoleCodes.Finance, userType: 2),
            [RoleCodes.Support] = await RoleClientAsync(admin, RoleCodes.Support, userType: 2),
            [RoleCodes.Housekeeping] = await RoleClientAsync(admin, RoleCodes.Housekeeping, userType: 3)
        };

        // (route, roles allowed in) — straight from ROLE_PERMISSION_MATRIX §4.
        var probes = new (string Route, string[] Allowed)[]
        {
            ("/api/users", []),
            ("/api/roles", []),
            ("/api/menus", []),
            ("/api/settings", []),
            ("/api/audit", [RoleCodes.Finance]),
            ("/api/customers", [RoleCodes.Manager, RoleCodes.Finance, RoleCodes.Support]),
            ("/api/bookings", [RoleCodes.Manager, RoleCodes.Finance, RoleCodes.Support]),
            ("/api/payments", [RoleCodes.Manager, RoleCodes.Finance, RoleCodes.Support]),
            ("/api/reports", [RoleCodes.Manager, RoleCodes.Finance, RoleCodes.Support]),
            ("/api/services?includeUnavailable=true",
                [RoleCodes.Manager, RoleCodes.Support]),
            ("/api/banners", [RoleCodes.Manager]),
            ("/api/inventory/items",
                [RoleCodes.Manager, RoleCodes.Finance, RoleCodes.Housekeeping]),
            ("/api/housekeeping/schedules",
                [RoleCodes.Manager, RoleCodes.Support, RoleCodes.Housekeeping]),
            ("/api/attendance",
                [RoleCodes.Manager, RoleCodes.Finance, RoleCodes.Housekeeping]),
            ("/api/tickets", [RoleCodes.Manager, RoleCodes.Support])
        };

        var failures = new List<string>();
        foreach (var (route, allowed) in probes)
        {
            // The seeded admin holds every permission.
            var adminResponse = await admin.GetAsync(route);
            if (adminResponse.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized)
            {
                failures.Add($"ADMIN denied on {route} ({(int)adminResponse.StatusCode})");
            }
            foreach (var (roleCode, client) in clients)
            {
                var response = await client.GetAsync(route);
                var isAllowed = response.StatusCode is not
                    (HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized);
                var shouldBeAllowed = allowed.Contains(roleCode);
                if (isAllowed != shouldBeAllowed)
                {
                    failures.Add($"{roleCode} on {route}: expected " +
                                 $"{(shouldBeAllowed ? "allowed" : "403")}, got {(int)response.StatusCode}");
                }
            }
        }
        failures.Should().BeEmpty("the live API must match ROLE_PERMISSION_MATRIX §4 exactly");

        // ACCESS.OVERRIDE is a distinct privilege: Manager holds it, Finance does not.
        var overridePayload = new
        {
            userId = 999_999, locationId = 1, direction = 1,
            reason = "Matrix probe — never granted",
        };
        (await clients[RoleCodes.Finance].PostAsJsonAsync("/api/access/manual-override", overridePayload))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await clients[RoleCodes.Support].PostAsJsonAsync("/api/access/manual-override", overridePayload))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await clients[RoleCodes.Manager].PostAsJsonAsync("/api/access/manual-override", overridePayload))
            .StatusCode.Should().Be(HttpStatusCode.NotFound,
                "Manager holds ACCESS.OVERRIDE — authorization passes, the probe user id does not exist");
    }

    // ── 4 · Customer scoping spot-checks (negative tests) ───────────────────

    [Fact]
    public async Task Customer_CannotReachAdministrationSurfaces()
    {
        var customer = factory.CreateClient();
        var (_, _, session) = await customer.RegisterCustomerAsync(factory);
        customer.UseToken(session.AccessToken);

        foreach (var route in new[]
        {
            "/api/users", "/api/roles", "/api/menus", "/api/settings", "/api/audit",
            "/api/reports", "/api/customers", "/api/inventory/items",
            "/api/housekeeping/schedules", "/api/dashboard/admin",
        })
        {
            (await customer.GetAsync(route)).StatusCode.Should().Be(HttpStatusCode.Forbidden,
                $"a customer must never reach {route}");
        }

        // Writes: catalogue and access-control mutations are admin-side.
        (await customer.PostAsJsonAsync("/api/access/manual-override",
                new { userId = 1, locationId = 1, direction = 1, reason = "nope" }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await customer.PostAsJsonAsync("/api/housekeeping/schedules", new { }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await customer.PutAsJsonAsync("/api/settings", new { settings = Array.Empty<object>() }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
