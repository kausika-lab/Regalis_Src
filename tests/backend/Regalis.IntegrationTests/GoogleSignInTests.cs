using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regalis.Application.Abstractions;
using Regalis.Application.Auth;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

/// <summary>
/// POST /api/auth/google, end to end against the real host and database.
///
/// The one thing that cannot be exercised for real is Google itself: minting a token
/// Google would sign is not possible from a test. <see cref="IGoogleTokenVerifier"/> is
/// therefore replaced with a stub, which is the seam that exists precisely so the
/// branches either side of verification can be tested. Everything else — routing,
/// anonymity, rate-limit policy, account creation, role and wallet, the JWT and its
/// claims — is the production code path.
/// </summary>
[Collection(ApiCollection.Name)]
public class GoogleSignInTests(RegalisApiFactory factory)
{
    /// <summary>Returns whatever the test tells it to, without contacting Google.</summary>
    private sealed class StubVerifier : IGoogleTokenVerifier
    {
        public bool IsConfigured { get; set; } = true;
        public GoogleIdentity? Next { get; set; }

        public Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct = default) =>
            // "bad-token" stands in for anything Google would reject: expired, tampered,
            // or minted for a different client id.
            Task.FromResult(idToken == "bad-token" ? null : Next);
    }

    private (HttpClient Client, StubVerifier Verifier) CreateClient(
        GoogleIdentity? identity, bool configured = true)
    {
        var verifier = new StubVerifier { Next = identity, IsConfigured = configured };
        var client = factory
            .WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            {
                services.RemoveAll<IGoogleTokenVerifier>();
                services.AddSingleton<IGoogleTokenVerifier>(verifier);
            }))
            .CreateClient();
        return (client, verifier);
    }

    private static GoogleIdentity Identity(string email, bool verified = true, string? name = "Google Member") =>
        new($"google-sub-{Guid.NewGuid():N}", email, verified, name);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string token = "any-token") =>
        await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(token));

    // ── The bug this endpoint was reported for ──────────────────────────────

    [Fact]
    public async Task Endpoint_IsRoutedAndAnonymous_NotTheGenericUnmatchedRoute401()
    {
        // Before this endpoint existed, every /api/auth/google call returned the same
        // AUTHENTICATION_FAILED 401 that any unmatched route returns, which read as
        // "the endpoint is protected" and sent people looking for a security-config bug.
        // Reaching a *validation* error proves the route exists and lets anonymous in.
        var (client, _) = CreateClient(identity: null);

        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest(""));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().Contain(e => e.Code == ErrorCodes.ValidationError);
    }

    // ── Token verification ──────────────────────────────────────────────────

    [Fact]
    public async Task UnverifiableToken_Is401AndCreatesNothing()
    {
        var (client, _) = CreateClient(identity: null);

        var response = await PostAsync(client, "bad-token");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Success.Should().BeFalse();
        envelope.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.AuthenticationFailed);
    }

    [Fact]
    public async Task UnverifiedGoogleEmail_IsRejected()
    {
        // The load-bearing check. Without it, creating a Google account that claims an
        // address is enough to take over the Regalis account holding it.
        var existing = await factory.CreateClient().RegisterCustomerAsync(factory);
        var (client, _) = CreateClient(Identity(existing.Email, verified: false));

        var response = await PostAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // And the existing account is untouched: the password still works.
        var stillMine = await factory.CreateClient()
            .LoginAsync(existing.Email, existing.Password, "customer");
        stillMine.AccessToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task NotConfigured_RefusesCleanly()
    {
        var (client, _) = CreateClient(Identity($"noconf-{Guid.NewGuid():N}@test.regalis.in"),
            configured: false);

        var response = await PostAsync(client);

        // BusinessRuleException, which the middleware maps to 422 — the deployment is
        // missing a client id, so there is nothing the caller can fix by retrying.
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Success.Should().BeFalse();
        envelope.Message.Should().Contain("not configured");
    }

    // ── First sign-in creates a usable member ───────────────────────────────

    [Fact]
    public async Task FirstSignIn_CreatesCustomerWithRoleWalletAndPermissions()
    {
        var email = $"gnew-{Guid.NewGuid():N}@test.regalis.in";
        var (client, _) = CreateClient(Identity(email, name: "Ada Lovelace"));

        var response = await PostAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>())!.Data!;

        session.User.Email.Should().Be(email);
        session.User.FullName.Should().Be("Ada Lovelace");
        session.AccessToken.Should().NotBeNullOrEmpty();
        session.RefreshToken.Should().NotBeNullOrEmpty();

        // A bare MST_User row would authenticate and then do nothing: no role means no
        // perm claims, an empty sidebar and 403 everywhere. Assert the whole account.
        session.Roles.Should().Contain(RoleCodes.Customer);
        session.Permissions.Should().NotBeEmpty();

        // The wallet has to exist too, or the first booking fails on a missing wallet.
        client.UseToken(session.AccessToken);
        var wallet = await client.GetAsync("/api/wallet/me");
        wallet.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SecondSignIn_ReusesTheSameAccount()
    {
        var email = $"gtwice-{Guid.NewGuid():N}@test.regalis.in";
        var identity = Identity(email);
        var (client, _) = CreateClient(identity);

        var first = (await (await PostAsync(client))
            .Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>())!.Data!;
        var second = (await (await PostAsync(client))
            .Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>())!.Data!;

        second.User.UserId.Should().Be(first.User.UserId);
    }

    [Fact]
    public async Task TwoGoogleMembers_BothSignUp_DespiteNeitherHavingAMobileNumber()
    {
        // MST_User.MobileNumber is uniquely indexed and Google supplies none, so both
        // rows carry the empty string. The index filter has to exclude it or the second
        // member cannot be created at all.
        var (clientA, _) = CreateClient(Identity($"gm1-{Guid.NewGuid():N}@test.regalis.in"));
        var (clientB, _) = CreateClient(Identity($"gm2-{Guid.NewGuid():N}@test.regalis.in"));

        (await PostAsync(clientA)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await PostAsync(clientB)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Linking an existing password account ────────────────────────────────

    [Fact]
    public async Task MatchingVerifiedEmail_LinksToTheExistingAccount_WithoutDuplicating()
    {
        var existing = await factory.CreateClient().RegisterCustomerAsync(factory);
        var (client, _) = CreateClient(Identity(existing.Email));

        var response = await PostAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var session = (await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>())!.Data!;

        // One member, not two.
        session.User.UserId.Should().Be(existing.Session.User.UserId);

        // Linking must not cost them their password.
        var byPassword = await factory.CreateClient()
            .LoginAsync(existing.Email, existing.Password, "customer");
        byPassword.User.UserId.Should().Be(existing.Session.User.UserId);
    }

    [Fact]
    public async Task DifferentGoogleAccountOnALinkedEmail_IsRejected()
    {
        var email = $"glinked-{Guid.NewGuid():N}@test.regalis.in";
        var (first, _) = CreateClient(Identity(email));
        (await PostAsync(first)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Same address, different Google subject — re-pointing the account would hand it
        // to whoever holds the newer Google account.
        var (second, _) = CreateClient(Identity(email));
        var response = await PostAsync(second);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Doors that stay shut ────────────────────────────────────────────────

    [Fact]
    public async Task AdminEmail_CannotBeEnteredThroughGoogle()
    {
        // Staff sign in with a password. A Google token naming an admin address must not
        // open the admin account, however verified that address is.
        var (client, _) = CreateClient(Identity(RegalisApiFactory.AdminEmail));

        var response = await PostAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Success.Should().BeFalse();
    }

    [Fact]
    public async Task DeactivatedAccount_IsNotRevivedByGoogle()
    {
        var victim = await factory.CreateClient().RegisterCustomerAsync(factory);

        var admin = factory.CreateClient();
        var adminSession = await admin.LoginAsAdminAsync();
        admin.UseToken(adminSession.AccessToken);
        var deactivate = await admin.PatchAsync(
            $"/api/users/{victim.Session.User.UserId}/deactivate", null);
        deactivate.EnsureSuccessStatusCode();

        var (client, _) = CreateClient(Identity(victim.Email));
        var response = await PostAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().Contain(e => e.Code == ErrorCodes.AccountInactive);
    }

    // ── Password login alongside a Google-only account ──────────────────────

    [Fact]
    public async Task GoogleOnlyAccount_PasswordLogin_SaysSoInsteadOfFailingBlankly()
    {
        var email = $"gnopass-{Guid.NewGuid():N}@test.regalis.in";
        var (client, _) = CreateClient(Identity(email));
        (await PostAsync(client)).StatusCode.Should().Be(HttpStatusCode.OK);

        var response = await factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, "AnyPassword!1", "customer"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Message.Should().Contain("Google");
    }
}
