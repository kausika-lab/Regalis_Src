using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Regalis.Application.Auth;
using Regalis.IntegrationTests.Infrastructure;
using Regalis.Shared.Api;
using Regalis.Shared.Constants;

namespace Regalis.IntegrationTests;

[Collection(ApiCollection.Name)]
public class AuthFlowTests(RegalisApiFactory factory)
{
    [Fact]
    public async Task AdminLogin_ReturnsRolesAndFullPermissionSet()
    {
        var client = factory.CreateClient();
        var session = await client.LoginAsAdminAsync();

        session.Roles.Should().Contain("ADMIN");
        session.Permissions.Should().Contain([
            "USER.VIEW", "ROLE.EDIT", "BOOKING.CREATE", "ACCESS.OVERRIDE", "REPORT.EXPORT"
        ]);
        session.AccessToken.Should().NotBeNullOrEmpty();
        session.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401Envelope()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RegalisApiFactory.AdminEmail, "Wrong!Password1", "admin"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Success.Should().BeFalse();
        envelope.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.AuthenticationFailed);
    }

    [Fact]
    public async Task Login_WrongPortal_IsRejectedWithoutAccountEnumeration()
    {
        var client = factory.CreateClient();
        // The admin exists — but not on the customer portal. Same generic 401.
        var response = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(RegalisApiFactory.AdminEmail, RegalisApiFactory.AdminPassword, "customer"));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task RegisterVerifyLogin_FullFlow_Succeeds()
    {
        var client = factory.CreateClient();
        var (_, _, session) = await client.RegisterCustomerAsync(factory);

        session.Roles.Should().Equal("CUSTOMER");
        session.Permissions.Should().Contain("BOOKING.CREATE");
        session.Permissions.Should().NotContain("USER.VIEW");
    }

    [Fact]
    public async Task Register_DuplicateVerifiedEmail_Returns422DuplicateRecord()
    {
        var client = factory.CreateClient();
        var (email, _, _) = await client.RegisterCustomerAsync(factory);

        var response = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Imposter", email, "9123456780", "Other!Pass1", "Other!Pass1", null));

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var envelope = await response.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.DuplicateRecord);
    }

    [Fact]
    public async Task Lockout_FiveWrongPasswords_LocksTheAccount()
    {
        var client = factory.CreateClient();
        var (email, password, _) = await client.RegisterCustomerAsync(factory);

        for (var i = 0; i < 5; i++)
        {
            var wrong = await client.PostAsJsonAsync("/api/auth/login",
                new LoginRequest(email, "Wrong!Password1", "customer"));
            wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }

        // The CORRECT password is now refused: the account is locked.
        var locked = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, password, "customer"));
        locked.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var envelope = await locked.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.AccountLocked);
    }

    [Fact]
    public async Task Refresh_Rotates_AndReuseRevokesTheWholeFamily()
    {
        var client = factory.CreateClient();
        var (_, _, session) = await client.RegisterCustomerAsync(factory);

        // First refresh succeeds and rotates.
        var first = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(session.RefreshToken));
        first.EnsureSuccessStatusCode();
        var rotated = (await first.Content.ReadFromJsonAsync<ApiResponse<RefreshResponse>>())!.Data!;
        rotated.RefreshToken.Should().NotBe(session.RefreshToken);

        // Replaying the consumed token is theft: 401 TOKEN_REVOKED…
        var replay = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(session.RefreshToken));
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var replayEnvelope = await replay.Content.ReadFromJsonAsync<ApiResponse>();
        replayEnvelope!.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.TokenRevoked);

        // …and the rotated child dies with the family.
        var child = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(rotated.RefreshToken));
        child.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task VerifyOtp_FiveWrongCodes_ThenRejectsEvenTheRightOne()
    {
        var client = factory.CreateClient();
        var email = $"otp-{Guid.NewGuid():N}@test.regalis.in";
        var register = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Otp Tester", email, $"8{Random.Shared.NextInt64(100_000_000, 999_999_999)}",
                "Otp!Tester1", "Otp!Tester1", null));
        register.EnsureSuccessStatusCode();
        var rightCode = factory.OtpSender.GetCode(email, Domain.Enums.OtpPurpose.Registration);
        var wrongCode = rightCode == "000000" ? "000001" : "000000";

        for (var i = 0; i < 5; i++)
        {
            var wrong = await client.PostAsJsonAsync("/api/auth/verify-otp",
                new VerifyOtpRequest(email, "registration", wrongCode));
            wrong.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        }

        var exhausted = await client.PostAsJsonAsync("/api/auth/verify-otp",
            new VerifyOtpRequest(email, "registration", rightCode));
        exhausted.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var envelope = await exhausted.Content.ReadFromJsonAsync<ApiResponse>();
        envelope!.Errors.Should().ContainSingle(e => e.Code == ErrorCodes.OtpMaxAttempts);
    }

    [Fact]
    public async Task ForgotPassword_UnknownEmail_StillReturns200()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest("nobody@nowhere.example"));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ForgotPassword_ResetFlow_ChangesPasswordAndRevokesSessions()
    {
        var client = factory.CreateClient();
        var (email, _, session) = await client.RegisterCustomerAsync(factory);

        var forgot = await client.PostAsJsonAsync("/api/auth/forgot-password",
            new ForgotPasswordRequest(email));
        forgot.EnsureSuccessStatusCode();

        var code = factory.OtpSender.GetCode(email, Domain.Enums.OtpPurpose.ForgotPassword);
        var verify = await client.PostAsJsonAsync("/api/auth/verify-otp",
            new VerifyOtpRequest(email, "forgot-password", code));
        verify.EnsureSuccessStatusCode();
        var resetToken = (await verify.Content.ReadFromJsonAsync<ApiResponse<VerifyOtpResponse>>())!
            .Data!.ResetToken!;

        var reset = await client.PostAsJsonAsync("/api/auth/reset-password",
            new ResetPasswordRequest(resetToken, "N3w!Password9", "N3w!Password9"));
        reset.EnsureSuccessStatusCode();

        // New password works; the pre-reset refresh token is revoked.
        await client.LoginAsync(email, "N3w!Password9", "customer");
        var oldRefresh = await client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(session.RefreshToken));
        oldRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
