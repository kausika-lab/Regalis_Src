using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regalis.Application.Abstractions;
using Regalis.Application.Auth;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

/// <summary>
/// Onboarding: GET/POST /api/profile/onboarding, and the gate that stops an incomplete
/// member booking or buying.
///
/// The gate is the part worth testing hardest. The SPA redirecting is a convenience; a
/// redirect is not a control, and anyone can call the API directly.
/// </summary>
[Collection(ApiCollection.Name)]
public class OnboardingTests(RegalisApiFactory factory)
{
    private sealed class StubVerifier : IGoogleTokenVerifier
    {
        public bool IsConfigured => true;
        public GoogleIdentity? Next { get; set; }
        public Task<GoogleIdentity?> VerifyAsync(string idToken, CancellationToken ct = default) =>
            Task.FromResult(Next);
    }

    private static CompleteOnboardingRequest ValidRequest(string? mobile = null) => new(
        FullName: "Ada Lovelace",
        MobileNumber: mobile ?? $"9{Random.Shared.NextInt64(100_000_000, 999_999_999)}",
        CompanyName: "Analytical Engines Pvt Ltd",
        AddressLine1: "12 Anna Salai",
        AddressLine2: "Teynampet",
        City: "Chennai",
        State: "Tamil Nadu",
        Country: "India",
        PostalCode: "600018",
        DateOfBirth: new DateOnly(1992, 3, 14),
        Gender: 2,
        Gstin: "33AABCT1234C1ZV");

    /// <summary>A member created the way Google creates one: name + email, nothing else.</summary>
    private async Task<HttpClient> GoogleMemberAsync()
    {
        var verifier = new StubVerifier
        {
            Next = new GoogleIdentity($"sub-{Guid.NewGuid():N}",
                $"onb-{Guid.NewGuid():N}@test.regalis.in", true, "Google Member")
        };
        var client = factory
            .WithWebHostBuilder(b => b.ConfigureServices(s =>
            {
                s.RemoveAll<IGoogleTokenVerifier>();
                s.AddSingleton<IGoogleTokenVerifier>(verifier);
            }))
            .CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/google", new GoogleLoginRequest("t"));
        response.EnsureSuccessStatusCode();
        var session = (await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponse>>())!.Data!;
        session.User.IsProfileComplete.Should().BeFalse("Google supplies no address or phone number");
        client.UseToken(session.AccessToken);
        return client;
    }

    // ── Status ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GoogleMember_StatusListsEverythingStillMissing()
    {
        var client = await GoogleMemberAsync();

        var status = (await client.GetFromJsonAsync<ApiResponse<OnboardingStatusDto>>(
            "/api/profile/onboarding"))!.Data!;

        status.IsComplete.Should().BeFalse();
        status.MissingFields.Should().Contain([
            "mobileNumber", "companyName", "addressLine1", "addressLine2",
            "city", "state", "country", "postalCode", "dateOfBirth", "gender", "gstin"
        ]);
        // Whatever Google did give us is echoed back so the form can pre-fill.
        status.Profile.FullName.Should().Be("Google Member");
        status.Profile.IsGoogleLinked.Should().BeTrue();
        status.Profile.HasPassword.Should().BeFalse();
    }

    [Fact]
    public async Task StatusIsReachableWhileIncomplete()
    {
        // If the gate covered this endpoint the SPA could never find out what to ask for.
        var client = await GoogleMemberAsync();
        var response = await client.GetAsync("/api/profile/onboarding");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── The gate ────────────────────────────────────────────────────────────

    [Fact]
    public async Task IncompleteMember_CannotHoldABooking()
    {
        var client = await GoogleMemberAsync();

        var response = await client.PostAsJsonAsync("/api/bookings", new
        {
            workspaceId = 1,
            startUtc = DateTime.UtcNow.AddDays(1),
            endUtc = DateTime.UtcNow.AddDays(1).AddHours(1)
        });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.ProfileIncomplete);
    }

    [Fact]
    public async Task IncompleteMember_CannotBuyAPass()
    {
        var client = await GoogleMemberAsync();

        var response = await client.PostAsJsonAsync("/api/passes/purchase",
            new { passPlanId = 1, paymentMethod = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.ProfileIncomplete);
    }

    [Fact]
    public async Task IncompleteMember_CanStillReadTheirOwnAccount()
    {
        // The gate is on spending, not on looking. Walling off everything would leave a
        // member unable to see why they are stuck.
        var client = await GoogleMemberAsync();

        (await client.GetAsync("/api/wallet/me")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/profile")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await client.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CompletingOnboarding_LiftsTheGate()
    {
        var client = await GoogleMemberAsync();

        var completed = await client.PostAsJsonAsync("/api/profile/onboarding", ValidRequest());
        completed.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = (await completed.Content.ReadFromJsonAsync<ApiResponse<ProfileDto>>())!.Data!;
        profile.IsProfileComplete.Should().BeTrue();
        profile.City.Should().Be("Chennai");
        profile.Gstin.Should().Be("33AABCT1234C1ZV");
        // The legacy single line is what TRN_Invoice snapshots, so it has to be filled in.
        profile.Address.Should().Contain("Anna Salai").And.Contain("600018");

        // No longer PROFILE_INCOMPLETE. The booking may still fail on its own merits —
        // a bad workspace id — but the gate is out of the way, which is the point.
        var booking = await client.PostAsJsonAsync("/api/bookings", new
        {
            workspaceId = 1,
            startUtc = DateTime.UtcNow.AddDays(1),
            endUtc = DateTime.UtcNow.AddDays(1).AddHours(1)
        });
        var envelope = await booking.Content.ReadFromJsonAsync<ApiResponse>();
        (envelope?.Errors ?? []).Should().NotContain(e => e.Code == ErrorCodes.ProfileIncomplete);
    }

    [Fact]
    public async Task CompletingOnboarding_ShowsUpOnTheNextLoginResponse()
    {
        var client = await GoogleMemberAsync();
        await client.PostAsJsonAsync("/api/profile/onboarding", ValidRequest());

        var me = (await client.GetFromJsonAsync<ApiResponse<MeResponse>>("/api/auth/me"))!.Data!;
        me.User.IsProfileComplete.Should().BeTrue();
    }

    // ── Validation ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("gstin", "NOT-A-GSTIN")]
    [InlineData("postalCode", "12")]
    [InlineData("mobileNumber", "123")]
    public async Task BadInput_IsRejected(string field, string value)
    {
        var client = await GoogleMemberAsync();
        var request = ValidRequest();
        request = field switch
        {
            "gstin" => request with { Gstin = value },
            "postalCode" => request with { PostalCode = value },
            _ => request with { MobileNumber = value }
        };

        var response = await client.PostAsJsonAsync("/api/profile/onboarding", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().Contain(e => e.Code == ErrorCodes.ValidationError);
    }

    [Fact]
    public async Task FutureDateOfBirth_IsRejected()
    {
        var client = await GoogleMemberAsync();
        var request = ValidRequest() with
        {
            DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(1))
        };

        var response = await client.PostAsJsonAsync("/api/profile/onboarding", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task LowerCaseGstin_IsAcceptedAndStoredUpperCase()
    {
        var client = await GoogleMemberAsync();
        var request = ValidRequest() with { Gstin = "33aabct1234c1zv" };

        var response = await client.PostAsJsonAsync("/api/profile/onboarding", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var profile = (await response.Content.ReadFromJsonAsync<ApiResponse<ProfileDto>>())!.Data!;
        profile.Gstin.Should().Be("33AABCT1234C1ZV");
    }

    [Fact]
    public async Task MobileNumberAlreadyOnAnotherAccount_IsRejected()
    {
        var taken = await factory.CreateClient().RegisterCustomerAsync(factory);
        var takenMobile = (await Authenticated(taken).GetFromJsonAsync<ApiResponse<ProfileDto>>(
            "/api/profile"))!.Data!.MobileNumber;

        var client = await GoogleMemberAsync();
        var response = await client.PostAsJsonAsync("/api/profile/onboarding",
            ValidRequest(mobile: takenMobile));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    // ── Who it applies to ───────────────────────────────────────────────────

    [Fact]
    public async Task Admin_IsNeverConsideredIncomplete()
    {
        // Staff are created by an administrator, not through a signup form, and have no
        // reason to hold a GSTIN or a date of birth.
        var client = factory.CreateClient();
        var session = await client.LoginAsAdminAsync();

        session.User.IsProfileComplete.Should().BeTrue();

        client.UseToken(session.AccessToken);
        var status = (await client.GetFromJsonAsync<ApiResponse<OnboardingStatusDto>>(
            "/api/profile/onboarding"))!.Data!;
        status.IsComplete.Should().BeTrue();
        status.MissingFields.Should().BeEmpty();
    }

    [Fact]
    public async Task Admin_CannotPostOnboarding()
    {
        var client = factory.CreateClient();
        var session = await client.LoginAsAdminAsync();
        client.UseToken(session.AccessToken);

        var response = await client.PostAsJsonAsync("/api/profile/onboarding", ValidRequest());

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RegisteringWithTheFullForm_SkipsOnboardingEntirely()
    {
        // The SPA's registration form asks for all of this, and it used to be dropped on
        // the floor. Storing it means a password signup never sees the onboarding wall.
        var registered = await factory.CreateClient().RegisterCustomerAsync(factory);

        registered.Session.User.IsProfileComplete.Should().BeTrue();
    }

    [Fact]
    public async Task RegisteringWithoutTheProfileFields_StillOwesOnboarding()
    {
        var registered = await factory.CreateClient().RegisterIncompleteCustomerAsync(factory);

        registered.Session.User.IsProfileComplete.Should().BeFalse();
    }

    private HttpClient Authenticated((string Email, string Password, LoginResponse Session) who)
    {
        var client = factory.CreateClient();
        client.UseToken(who.Session.AccessToken);
        return client;
    }
}
