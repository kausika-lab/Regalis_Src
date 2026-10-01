using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Regalis.Application.Abstractions;
using Regalis.Domain.Entities.Identity;

namespace Regalis.Infrastructure.Security;

/// <summary>Issues 15-minute JWT access tokens and 256-bit opaque refresh tokens.
/// Refresh tokens are stored hashed (SHA-256) — the raw value is returned exactly once.</summary>
public class JwtTokenService(IOptions<JwtOptions> options, IDateTimeProvider clock, IServerInstance server)
    : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public AccessTokenResult CreateAccessToken(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions)
    {
        var nowUtc = clock.UtcNow;
        var expiresUtc = nowUtc.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new("name", user.FullName),
            new("user_type", ((byte)user.UserType).ToString()),
            // The issuing process: a token from before a server restart is refused.
            new(ServerInstance.ClaimType, server.BootId)
        };
        if (user.DefaultLocationId is not null)
        {
            claims.Add(new Claim("loc", user.DefaultLocationId.Value.ToString()));
        }
        claims.AddRange(roles.Select(r => new Claim("role", r)));
        claims.AddRange(permissions.Select(p => new Claim("perm", p)));

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: nowUtc,
            expires: expiresUtc,
            signingCredentials: credentials);

        return new AccessTokenResult(new JwtSecurityTokenHandler().WriteToken(token), expiresUtc);
    }

    public RefreshTokenResult CreateRefreshToken()
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        return new RefreshTokenResult(raw, HashRefreshToken(raw),
            clock.UtcNow.AddDays(_options.RefreshTokenDays));
    }

    public string HashRefreshToken(string rawToken) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
}
