using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Identity;
using Regalis.Domain.Enums;
using Regalis.Infrastructure.Security;

namespace Regalis.UnitTests.Security;

public class JwtTokenServiceTests
{
    private sealed class FixedClock(DateTime nowUtc) : IDateTimeProvider
    {
        public DateTime UtcNow { get; } = nowUtc;
    }

    private static readonly DateTime Now = new(2026, 9, 2, 10, 0, 0, DateTimeKind.Utc);

    private readonly JwtTokenService _service = new(
        Options.Create(new JwtOptions
        {
            Issuer = "RegalisWorkSpace",
            Audience = "RegalisWorkSpace.Client",
            SigningKey = "unit-test-signing-key-0123456789abcdef0123456789abcdef",
            AccessTokenMinutes = 15,
            RefreshTokenDays = 7
        }),
        new FixedClock(Now));

    private static readonly User TestUser = new()
    {
        UserId = 42,
        FullName = "Test User",
        Email = "test@example.com",
        UserType = UserType.Customer,
        DefaultLocationId = 1
    };

    [Fact]
    public void CreateAccessToken_CarriesTheDocumentedClaims()
    {
        var result = _service.CreateAccessToken(TestUser,
            ["CUSTOMER"], ["BOOKING.VIEW", "BOOKING.CREATE"]);

        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Token);
        token.Claims.Single(c => c.Type == "sub").Value.Should().Be("42");
        token.Claims.Single(c => c.Type == "email").Value.Should().Be("test@example.com");
        token.Claims.Single(c => c.Type == "name").Value.Should().Be("Test User");
        token.Claims.Single(c => c.Type == "user_type").Value.Should().Be("1");
        token.Claims.Single(c => c.Type == "loc").Value.Should().Be("1");
        token.Claims.Where(c => c.Type == "role").Select(c => c.Value).Should().Equal("CUSTOMER");
        token.Claims.Where(c => c.Type == "perm").Select(c => c.Value)
            .Should().BeEquivalentTo("BOOKING.VIEW", "BOOKING.CREATE");
        token.Claims.Should().Contain(c => c.Type == "jti");
    }

    [Fact]
    public void CreateAccessToken_ExpiresInFifteenMinutes()
    {
        var result = _service.CreateAccessToken(TestUser, [], []);
        result.ExpiresUtc.Should().Be(Now.AddMinutes(15));
    }

    [Fact]
    public void CreateRefreshToken_RawValueHashesToStoredHash()
    {
        var result = _service.CreateRefreshToken();

        result.RawToken.Should().NotBeNullOrEmpty();
        result.TokenHash.Should().Be(_service.HashRefreshToken(result.RawToken));
        result.ExpiresUtc.Should().Be(Now.AddDays(7));
    }

    [Fact]
    public void CreateRefreshToken_IsUnpredictable()
    {
        _service.CreateRefreshToken().RawToken
            .Should().NotBe(_service.CreateRefreshToken().RawToken);
    }
}
