using Regalis.Domain.Entities.Identity;

namespace Regalis.Application.Abstractions;

/// <summary>Result of creating an access token.</summary>
public record AccessTokenResult(string Token, DateTime ExpiresUtc);

/// <summary>Result of creating a refresh token: the raw value goes to the client once;
/// only the SHA-256 hash is persisted.</summary>
public record RefreshTokenResult(string RawToken, string TokenHash, DateTime ExpiresUtc);

/// <summary>Issues JWT access tokens and opaque refresh tokens (ARCHITECTURE §4).</summary>
public interface ITokenService
{
    /// <summary>15-minute JWT with sub, email, user_type, name, role (multi),
    /// perm (multi), loc and jti claims.</summary>
    AccessTokenResult CreateAccessToken(
        User user,
        IReadOnlyCollection<string> roles,
        IReadOnlyCollection<string> permissions);

    /// <summary>7-day, 256-bit random refresh token. Raw value returned once, hash stored.</summary>
    RefreshTokenResult CreateRefreshToken();

    /// <summary>SHA-256 hex hash used to look a presented refresh token up.</summary>
    string HashRefreshToken(string rawToken);
}
