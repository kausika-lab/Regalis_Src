using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Regalis.Application.Auth;
using Regalis.IntegrationTests.Infrastructure;

namespace Regalis.IntegrationTests;

/// <summary>
/// A server restart ends every session (Auth:EndSessionsOnRestart): the access token is
/// refused by the new process and its refresh token was revoked at startup, so the SPA
/// has to show the sign-in page. A second host on the same database stands in for the
/// restarted process.
/// </summary>
[Collection(ApiCollection.Name)]
public class SessionRestartTests(RegalisApiFactory factory)
{
    [Fact]
    public async Task ServerRestart_EndsEverySession_AndSigningInAgainWorks()
    {
        var before = factory.CreateClient();
        var session = await before.LoginAsAdminAsync();
        before.UseToken(session.AccessToken);
        (await before.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        await using var restarted = factory.WithWebHostBuilder(_ => { });
        var after = restarted.CreateClient();

        after.UseToken(session.AccessToken);
        (await after.GetAsync("/api/auth/me")).StatusCode
            .Should().Be(HttpStatusCode.Unauthorized, "the token was issued before the restart");

        (await after.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(session.RefreshToken)))
            .StatusCode.Should().Be(HttpStatusCode.Unauthorized, "refresh tokens are revoked at startup");

        var again = await after.LoginAsAdminAsync();
        after.UseToken(again.AccessToken);
        (await after.GetAsync("/api/auth/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        var renewed = await after.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(again.RefreshToken));
        renewed.StatusCode.Should().Be(HttpStatusCode.OK, "a session begun after the restart renews normally");
    }
}
